using System.Data.Common;
using System.Text.Json;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// Entrega los eventos pendientes de cada outbox dado de alta a sus handlers (AD-3). Una vuelta recorre todos los
/// outbox; el <see cref="ServicioDeEntregaDeEventos"/> la repite.
/// <para>
/// Cada evento se entrega en su propia transacción del contexto del módulo que lo publicó: aparta la fila con
/// <c>FOR UPDATE SKIP LOCKED</c>, llama a los handlers y la marca. Dos instancias sobre la misma base nunca
/// entregan el mismo evento a la vez, y un evento que no terminó de entregarse (la web se cayó) sigue
/// <c>Pendiente</c>. La fila queda apartada mientras corren sus handlers.
/// </para>
/// </summary>
public sealed class EntregaDeEventos(
    IServiceScopeFactory ambitos,
    IEnumerable<OutboxRegistrado> outboxes,
    CatalogoDeEventos catalogo,
    IClock reloj,
    ILogger<EntregaDeEventos> registro)
{
    /// <summary>
    /// Da una vuelta: entrega todo lo que hoy puede entregarse de cada outbox. Devuelve cuántos eventos intentó.
    /// Nunca lanza por un outbox que no se puede leer (por ejemplo, la base sin migrar): lo registra y sigue con
    /// el siguiente (AD-25). Solo lanza si se cancela.
    /// </summary>
    public async Task<int> DarUnaVueltaAsync(CancellationToken cancellationToken)
    {
        var intentados = 0;

        foreach (var outbox in outboxes)
        {
            try
            {
                while (await EntregarElSiguienteAsync(outbox, cancellationToken))
                {
                    intentados++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                registro.LogError(error, "No se pudo leer o actualizar el outbox de {Contexto}; se intenta en la siguiente vuelta", outbox.Contexto.Name);
            }
        }

        return intentados;
    }

    // Aparta un evento, lo entrega y lo marca, todo en una transacción. Falso si no hay ninguno que hoy pueda
    // entregarse.
    private async Task<bool> EntregarElSiguienteAsync(OutboxRegistrado outbox, CancellationToken cancellationToken)
    {
        await using var ambito = ambitos.CreateAsyncScope();
        var db = (DbContext)ambito.ServiceProvider.GetRequiredService(outbox.Contexto);
        var esquema = db.Model.GetDefaultSchema()
            ?? throw new InvalidOperationException($"El contexto {outbox.Contexto.Name} no tiene esquema por defecto: no se sabe dónde está su outbox.");
        var tabla = $"\"{esquema}\".\"{Outbox.Tabla}\"";

        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        var conexion = db.Database.GetDbConnection();
        var tx = transaccion.GetDbTransaction();

        var evento = await ApartarAsync(conexion, tx, tabla, cancellationToken);
        if (evento is null)
        {
            return false;
        }

        var entregado = await EntregarAsync(evento, cancellationToken);
        var ahora = reloj.UtcNow.ToUniversalTime();

        // Los handlers ya corrieron: lo que pasó se anota aunque la web se esté apagando. Si se cancelara aquí, el
        // evento seguiría Pendiente y se entregaría otra vez sin necesidad.
        var sinCancelar = CancellationToken.None;
        var quedoFallido = false;
        var intentos = evento.Intentos;

        if (entregado)
        {
            await EjecutarAsync(conexion, tx,
                $"update {tabla} set estado = @estado, entregado_en = @ahora where id = @id",
                sinCancelar, ("estado", Outbox.Entregado), ("ahora", ahora), ("id", evento.Id));
        }
        else
        {
            intentos++;
            if (CalendarioDeReintentos.EsperaTras(intentos) is { } espera)
            {
                await EjecutarAsync(conexion, tx,
                    $"update {tabla} set intentos = @intentos, proximo_intento_en = @proximo where id = @id",
                    sinCancelar, ("intentos", intentos), ("proximo", ahora + espera), ("id", evento.Id));
            }
            else
            {
                await EjecutarAsync(conexion, tx,
                    $"update {tabla} set estado = @estado, intentos = @intentos where id = @id",
                    sinCancelar, ("estado", Outbox.Fallido), ("intentos", intentos), ("id", evento.Id));
                quedoFallido = true;
            }
        }

        await transaccion.CommitAsync(sinCancelar);

        if (quedoFallido)
        {
            registro.LogError(
                "El evento {EventoId} ({Tipo}) del outbox {Esquema} quedó Fallido tras {Intentos} intentos; no se intenta más",
                evento.Id, evento.Tipo, esquema, intentos);
        }

        return true;
    }

    // El evento pendiente más antiguo que ya puede intentarse y que ninguna otra instancia tiene apartado. Uno no
    // puede intentarse mientras haya otro anterior con su misma clave todavía Pendiente; uno Fallido no detiene a
    // los que siguen.
    private async Task<EventoApartado?> ApartarAsync(DbConnection conexion, DbTransaction tx, string tabla, CancellationToken cancellationToken)
    {
        await using var comando = Comando(conexion, tx,
            $"""
            select o.id, o.tipo, o.contenido::text, o.intentos
            from {tabla} o
            where o.estado = @pendiente
              and o.proximo_intento_en <= @ahora
              and not exists (
                  select 1 from {tabla} anterior
                  where anterior.clave_de_orden = o.clave_de_orden
                    and anterior.estado = @pendiente
                    and (anterior.ocurrido_en < o.ocurrido_en
                         or (anterior.ocurrido_en = o.ocurrido_en and anterior.id < o.id)))
            order by o.ocurrido_en, o.id
            limit 1
            for update of o skip locked
            """,
            ("pendiente", Outbox.Pendiente), ("ahora", reloj.UtcNow.ToUniversalTime()));

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        return await lector.ReadAsync(cancellationToken)
            ? new EventoApartado(lector.GetGuid(0), lector.GetString(1), lector.GetString(2), lector.GetInt32(3))
            : null;
    }

    // Llama a todos los handlers del evento, cada uno en su propio ámbito: no comparten contexto entre ellos ni
    // con la transacción que tiene apartada la fila. Verdadero si ninguno lanzó. Si uno lanza, los demás lo
    // reciben igual; en el reintento lo reciben todos otra vez.
    private async Task<bool> EntregarAsync(EventoApartado evento, CancellationToken cancellationToken)
    {
        if (!catalogo.TryBuscar(evento.Tipo, out var tipo, out var handlers))
        {
            registro.LogWarning("El evento {EventoId} ({Tipo}) no tiene ningún handler registrado; queda Entregado", evento.Id, evento.Tipo);
            return true;
        }

        var despacho = (Despacho)Activator.CreateInstance(typeof(Despacho<>).MakeGenericType(tipo))!;
        var todos = true;

        for (var indice = 0; indice < handlers; indice++)
        {
            try
            {
                await using var ambito = ambitos.CreateAsyncScope();
                await despacho.LlamarAsync(ambito.ServiceProvider, indice, evento.Contenido, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // La web se está apagando: no es una entrega fallida. La fila sigue como estaba.
                throw;
            }
            catch (Exception error)
            {
                todos = false;
                registro.LogWarning(error,
                    "Un handler del evento {EventoId} ({Tipo}) falló en el intento {Intento} de {Maximo}",
                    evento.Id, evento.Tipo, evento.Intentos + 1, CalendarioDeReintentos.IntentosMaximos);
            }
        }

        return todos;
    }

    private static async Task EjecutarAsync(
        DbConnection conexion, DbTransaction tx, string sql, CancellationToken cancellationToken, params (string Nombre, object Valor)[] parametros)
    {
        await using var comando = Comando(conexion, tx, sql, parametros);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DbCommand Comando(DbConnection conexion, DbTransaction tx, string sql, params (string Nombre, object Valor)[] parametros)
    {
        var comando = conexion.CreateCommand();
        comando.Transaction = tx;
        comando.CommandText = sql;

        foreach (var (nombre, valor) in parametros)
        {
            var parametro = comando.CreateParameter();
            parametro.ParameterName = nombre;
            parametro.Value = valor;
            comando.Parameters.Add(parametro);
        }

        return comando;
    }

    private sealed record EventoApartado(Guid Id, string Tipo, string Contenido, int Intentos);

    // El paso de "un tipo que solo se conoce al leer la fila" a IEventHandler<TEvento>.
    private abstract class Despacho
    {
        public abstract Task LlamarAsync(IServiceProvider servicios, int indice, string contenido, CancellationToken cancellationToken);
    }

    private sealed class Despacho<TEvento> : Despacho
    {
        public override Task LlamarAsync(IServiceProvider servicios, int indice, string contenido, CancellationToken cancellationToken)
        {
            var evento = JsonSerializer.Deserialize<TEvento>(contenido)
                ?? throw new InvalidOperationException($"El contenido del evento no se pudo leer como {typeof(TEvento).Name}.");

            return servicios.GetServices<IEventHandler<TEvento>>().ElementAt(indice).HandleAsync(evento, cancellationToken);
        }
    }
}
