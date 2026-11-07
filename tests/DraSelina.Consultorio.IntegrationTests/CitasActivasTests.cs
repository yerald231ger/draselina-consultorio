using System.Data.Common;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// <see cref="CitasActivas.EntreAsync"/> contra PostgreSQL real: qué Citas traslapan un periodo y por dónde las busca
/// la base. Cada prueba usa su propia base migrada.
/// </summary>
public sealed class CitasActivasTests(Arnes arnes)
{
    private static readonly DateOnly Martes = new(2026, 11, 3);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Trae_las_Citas_no_canceladas_que_traslapan_el_periodo_semiabierto()
    {
        var cadena = await arnes.CrearBaseMigradaAsync(Cancelacion);
        await InsertarCitaAsync(cadena, new(15, 0), new(16, 0), "Agendada");
        await InsertarCitaAsync(cadena, new(16, 0), new(16, 30), "NoSePresento");
        await InsertarCitaAsync(cadena, new(16, 30), new(17, 0), "Cancelada");
        await InsertarCitaAsync(cadena, new(17, 0), new(18, 0), "Agendada");
        await InsertarCitaAsync(cadena, new(18, 0), new(18, 30), "Agendada");

        await using var db = Contexto(cadena);
        var citas = new CitasActivas(db);

        // De 15:30 a 17:30: la de las 15:00 empieza antes y entra, la de las 17:00 termina después; la cancelada no
        // cuenta, y la de las 18:00 queda fuera.
        Assert.Equal(
            [new TimeOnly(15, 0), new TimeOnly(16, 0), new TimeOnly(17, 0)],
            await IniciosAsync(citas, new(15, 30), new(17, 30)));

        // De 16:00 a 18:00: la que termina justo en "desde" y la que empieza justo en "hasta" no ocupan nada.
        Assert.Equal([new TimeOnly(16, 0), new TimeOnly(17, 0)], await IniciosAsync(citas, new(16, 0), new(18, 0)));
    }

    private static async Task<TimeOnly[]> IniciosAsync(CitasActivas citas, TimeOnly desde, TimeOnly hasta)
    {
        var rangos = await citas.EntreAsync(HoraLocal.AUtc(Martes, desde), HoraLocal.AUtc(Martes, hasta), Cancelacion);
        return [.. rangos.Select(r => HoraLocal.DesdeUtc(r.Inicio).Hora).Order()];
    }

    [Fact]
    public async Task Un_periodo_vacio_no_trae_nada()
    {
        var cadena = await arnes.CrearBaseMigradaAsync(Cancelacion);
        await InsertarCitaAsync(cadena, new(16, 0), new(17, 0), "Agendada");

        await using var db = Contexto(cadena);
        var instante = HoraLocal.AUtc(Martes, new(16, 30));

        Assert.Empty(await new CitasActivas(db).EntreAsync(instante, instante, Cancelacion));
        Assert.Empty(await new CitasActivas(db).EntreAsync(instante, instante.AddMinutes(-10), Cancelacion));
    }

    [Fact]
    public async Task La_consulta_va_por_el_indice_de_la_restriccion_sin_traslape()
    {
        var cadena = await arnes.CrearBaseMigradaAsync(Cancelacion);
        await InsertarCitaAsync(cadena, new(16, 0), new(17, 0), "Agendada");

        var captura = new CapturaDeComando();
        await using (var db = Contexto(cadena, captura))
        {
            await new CitasActivas(db).EntreAsync(
                HoraLocal.AUtc(Martes, TimeOnly.MinValue), HoraLocal.AUtc(Martes.AddDays(60), TimeOnly.MinValue), Cancelacion);
        }

        var comando = Assert.Single(captura.Comandos);

        // Con pocas filas PostgreSQL prefiere recorrer la tabla; sin esa opción se ve si la consulta puede ir por el
        // índice. Una consulta que no pudiera usarlo seguiría con "Seq Scan".
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(Cancelacion);
        await using (var apagar = new NpgsqlCommand("set enable_seqscan = off", conexion))
        {
            await apagar.ExecuteNonQueryAsync(Cancelacion);
        }

        await using var explicar = new NpgsqlCommand("explain " + comando.Texto, conexion);
        explicar.Parameters.AddRange(comando.Parametros);
        var plan = new List<string>();
        await using (var lector = await explicar.ExecuteReaderAsync(Cancelacion))
        {
            while (await lector.ReadAsync(Cancelacion))
            {
                plan.Add(lector.GetString(0));
            }
        }

        var texto = string.Join('\n', plan);
        Assert.Contains(AgendaDbContext.RestriccionSinTraslape, texto);
        Assert.DoesNotContain("Seq Scan", texto);
    }

    private static AgendaDbContext Contexto(string cadena, IInterceptor? interceptor = null)
    {
        var opciones = new DbContextOptionsBuilder<AgendaDbContext>();
        AgendaDbContext.Configurar(opciones, cadena);
        if (interceptor is not null)
        {
            opciones.AddInterceptors(interceptor);
        }

        return new AgendaDbContext(opciones.Options);
    }

    private static async Task InsertarCitaAsync(string cadena, TimeOnly inicio, TimeOnly fin, string estado)
    {
        var cancelacion = estado == "Cancelada" ? "'paciente', now()" : "null, null";
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var comando = new NpgsqlCommand(
            $"""
            insert into agenda.cita (id, paciente_id, tipo_de_visita_id, inicio, fin, estado, motivo_de_cancelacion,
                cancelada_en, origen, consentimiento_en, consentimiento_version_aviso, consentimiento_canal, creada_en,
                token_hash)
            values (gen_random_uuid(), gen_random_uuid(), '{TiposDeVisitaSembrados.PrimeraConsulta}',
                '{HoraLocal.AUtc(Martes, inicio):O}', '{HoraLocal.AUtc(Martes, fin):O}', '{estado}', {cancelacion},
                'consultorio', now(), '2026-11-borrador', 'externo', now(), sha256(uuid_send(gen_random_uuid())))
            """,
            conexion);
        await comando.ExecuteNonQueryAsync(Cancelacion);
    }

    /// <summary>Lo que el contexto manda a la base al leer: el texto del comando y una copia de sus parámetros.</summary>
    private sealed class CapturaDeComando : DbCommandInterceptor
    {
        public List<(string Texto, NpgsqlParameter[] Parametros)> Comandos { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Comandos.Add((command.CommandText, [.. command.Parameters.Cast<NpgsqlParameter>().Select(p => p.Clone())]));
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
