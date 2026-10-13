using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// El puerto por el que un handler de prueba publica un evento, con la misma forma que el de un módulo
/// (<c>IOutboxDeAgenda</c>): el evento se guarda en <c>pruebas.outbox</c> dentro de la transacción abierta.
/// </summary>
public interface IOutboxDePruebas
{
    Task PublicarAsync<TEvento>(
        TEvento evento,
        Guid eventoId,
        Guid claveDeOrden,
        DateTimeOffset ocurridoEn,
        CancellationToken cancellationToken)
        where TEvento : notnull;
}

/// <summary>Una fila de <c>pruebas.outbox</c>, con las mismas columnas que <c>agenda.outbox</c>.</summary>
public sealed class FilaDeOutboxDePruebas
{
    public Guid Id { get; set; }

    public string Tipo { get; set; } = "";

    public string Contenido { get; set; } = "";

    public DateTimeOffset OcurridoEn { get; set; }

    public Guid ClaveDeOrden { get; set; }

    public string Estado { get; set; } = "Pendiente";

    public int Intentos { get; set; }

    public DateTimeOffset ProximoIntentoEn { get; set; }

    public DateTimeOffset? EntregadoEn { get; set; }
}

/// <summary>El outbox de los tipos de prueba sobre <see cref="PruebasDbContext"/>, igual que el de un módulo.</summary>
public sealed class OutboxDePruebas(PruebasDbContext db) : IOutboxDePruebas
{
    public async Task PublicarAsync<TEvento>(
        TEvento evento,
        Guid eventoId,
        Guid claveDeOrden,
        DateTimeOffset ocurridoEn,
        CancellationToken cancellationToken)
        where TEvento : notnull
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException($"Publicar '{typeof(TEvento).Name}' exige la transacción abierta del handler.");
        }

        // El tipo real del evento, no el de la variable de quien llama: con un evento guardado como object o como
        // una interfaz, el nombre y el contenido serían los de ese tipo y ningún handler lo recibiría.
        var tipo = evento.GetType().Name;
        var contenido = JsonSerializer.Serialize(evento, evento.GetType());
        var ocurrido = ocurridoEn.ToUniversalTime();

        await db.Database.ExecuteSqlAsync(
            $"""
            insert into pruebas.outbox
                (id, tipo, contenido, ocurrido_en, clave_de_orden, estado, intentos, proximo_intento_en, entregado_en)
            values
                ({eventoId}, {tipo}, {contenido}::jsonb, {ocurrido}, {claveDeOrden}, 'Pendiente', 0, {ocurrido}, null)
            """,
            cancellationToken);
    }
}
