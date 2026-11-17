using System.Security.Claims;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// El candado de toda la agenda de atención (entrada 6.10) contra PostgreSQL real: guardar la agenda sincroniza la base y
/// todas las custom desde hoy con lo que se leyó, así que cambiar la base, guardar y borrar una custom hacen fila y leen
/// después de tomarlo. Otra transacción toma el candado y cambia la agenda sin confirmar; el handler espera y, al
/// confirmarse la otra, no deshace su cambio. Reloj de <see cref="ReservaEnLineaTests"/>: lunes 2 de noviembre de 2026.
/// </summary>
public sealed class CandadoDeLaAgendaDeAtencionTests(Arnes arnes)
{
    /// <summary>La primera parte de la llave del candado (<c>CandadoDeLaAgendaDeAtencion</c>): "AGAT" en ASCII.</summary>
    private const int ClaseDeCandado = 0x41474154;

    private static readonly DateOnly Martes = new(2026, 11, 3);

    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Guardar_una_custom_mientras_cambia_la_base_no_regresa_la_base()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var otra = await OtraConElCandadoAsync(
            escenario, "update agenda.agenda_base_tramo set fin = '17:00' where dia_de_la_semana = 1");

        var guardar = ComoAdministradorAsync(escenario, (ICommandHandler<GuardarAgendaCustom, AgendaCustomGuardada> h) => h.HandleAsync(
            new GuardarAgendaCustom
            {
                Fecha = Sabado,
                Motivo = "sábado abierto",
                Tramos = [new TramoAGuardar { Inicio = new TimeOnly(10, 0), Fin = new TimeOnly(13, 0) }],
            },
            Cancelacion));

        Assert.True((await otra.ConfirmarDespuesDeEsperarAsync(guardar)).EsExito);
        Assert.Equal(new TimeOnly(17, 0), await escenario.EscalarAsync<TimeOnly>("select fin from agenda.agenda_base_tramo where dia_de_la_semana = 1"));
        Assert.Equal(1L, await escenario.EscalarAsync<long>($"select count(*) from agenda.agenda_custom where fecha = '{Sabado:yyyy-MM-dd}'"));
    }

    [Fact]
    public async Task Cambiar_la_base_mientras_se_guarda_una_custom_no_borra_la_custom()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var otra = await OtraConElCandadoAsync(
            escenario,
            $"insert into agenda.agenda_custom (fecha, motivo, creada_en, actualizada_en) values ('{Martes:yyyy-MM-dd}', 'cirugía', now(), now())");

        var cambiar = ComoAdministradorAsync(escenario, (ICommandHandler<CambiarAgendaBase, AgendaBaseCambiada> h) => h.HandleAsync(
            new CambiarAgendaBase
            {
                Dias =
                [
                    .. Enum.GetValues<DayOfWeek>().Select(d => new DiaDeLaBaseAGuardar
                    {
                        Dia = d,
                        Tramos = d is DayOfWeek.Saturday or DayOfWeek.Sunday
                            ? []
                            : [new TramoAGuardar { Inicio = new TimeOnly(15, 30), Fin = new TimeOnly(d == DayOfWeek.Monday ? 17 : 18, 0) }],
                    }),
                ],
            },
            Cancelacion));

        Assert.True((await otra.ConfirmarDespuesDeEsperarAsync(cambiar)).EsExito);
        Assert.Equal(1L, await escenario.EscalarAsync<long>($"select count(*) from agenda.agenda_custom where fecha = '{Martes:yyyy-MM-dd}'"));
        Assert.Equal(new TimeOnly(17, 0), await escenario.EscalarAsync<TimeOnly>("select fin from agenda.agenda_base_tramo where dia_de_la_semana = 1"));
    }

    [Fact]
    public async Task Borrar_una_custom_mientras_se_guarda_otra_no_borra_la_otra()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.EjecutarAsync(
            $"insert into agenda.agenda_custom (fecha, motivo, creada_en, actualizada_en) values ('{Sabado:yyyy-MM-dd}', 'sábado cerrado', now(), now())");
        await using var otra = await OtraConElCandadoAsync(
            escenario,
            $"insert into agenda.agenda_custom (fecha, motivo, creada_en, actualizada_en) values ('{Martes:yyyy-MM-dd}', 'cirugía', now(), now())");

        var borrar = ComoAdministradorAsync(escenario, (ICommandHandler<BorrarAgendaCustom, AgendaCustomBorrada> h) => h.HandleAsync(
            new BorrarAgendaCustom { Fecha = Sabado, Citas = [] },
            Cancelacion));

        Assert.True((await otra.ConfirmarDespuesDeEsperarAsync(borrar)).EsExito);
        Assert.Equal(
            [Martes],
            await escenario.FilasAsync("select fecha from agenda.agenda_custom order by fecha", l => l.GetFieldValue<DateOnly>(0)));
    }

    // Otra transacción toma el candado de la agenda de atención y deja un cambio sin confirmar.
    private static async Task<Otra> OtraConElCandadoAsync(Escenario escenario, string cambio)
    {
        var conexion = new NpgsqlConnection(escenario.Cadena);
        await conexion.OpenAsync(Cancelacion);
        var transaccion = await conexion.BeginTransactionAsync(Cancelacion);
        foreach (var sql in new[] { $"select pg_advisory_xact_lock({ClaseDeCandado}, 0)", cambio })
        {
            await using var comando = new NpgsqlCommand(sql, conexion, transaccion);
            await comando.ExecuteNonQueryAsync(Cancelacion);
        }

        return new Otra(conexion, transaccion);
    }

    private static async Task<T> ComoAdministradorAsync<THandler, T>(Escenario escenario, Func<THandler, Task<T>> llamar)
        where THandler : notnull
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "cuenta-del-administrador"), new Claim(ClaimTypes.Name, "administradora")],
            authenticationType: "Prueba")));
        return await llamar(ambito.ServiceProvider.GetRequiredService<THandler>());
    }

    private sealed class Otra(NpgsqlConnection conexion, NpgsqlTransaction transaccion) : IAsyncDisposable
    {
        // Mientras la otra no confirma, el handler hace fila; al confirmar, termina sobre lo que ella dejó.
        public async Task<Resultado<T>> ConfirmarDespuesDeEsperarAsync<T>(Task<Resultado<T>> handler)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), Cancelacion);
            Assert.False(handler.IsCompleted);
            await transaccion.CommitAsync(Cancelacion);
            var resultado = await handler;
            Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
            return resultado;
        }

        public async ValueTask DisposeAsync()
        {
            await transaccion.DisposeAsync();
            await conexion.DisposeAsync();
        }
    }
}
