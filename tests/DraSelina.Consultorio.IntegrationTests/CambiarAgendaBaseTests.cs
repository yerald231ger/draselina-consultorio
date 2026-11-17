using System.Security.Claims;
using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// <see cref="CambiarAgendaBase"/> y su vista previa <see cref="CambioDeAgendaBase"/> contra PostgreSQL real (CAP-10,
/// RN-5 a RN-7, entrada 6.4): la semana completa, solo los días que cambian, ninguna Cita cancelada, las fechas con
/// custom intactas, la bitácora por día y la siguiente reserva con la base nueva. Con el escenario y el reloj de
/// <see cref="ReservaEnLineaTests"/>: lunes 2 de noviembre de 2026 a las 12:00 local; base de lunes a viernes, 3:30 a
/// 6:00 pm; sábado y domingo sin atención.
/// </summary>
public sealed class CambiarAgendaBaseTests(Arnes arnes)
{
    private const string IdDelAdministrador = "cuenta-del-administrador";

    private static readonly DateOnly LunesSiguiente = new(2026, 11, 9);

    private static readonly DateOnly Miercoles = new(2026, 11, 4);

    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static readonly DayOfWeek[] DeLunesADomingo =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task La_vista_previa_lista_las_Citas_fuera_de_los_dias_que_cambian_y_las_fechas_con_custom()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await SembrarAsync(escenario);

        var vista = await VistaAsync(escenario, CortoLunesMiercolesViernes());

        var cita = Assert.Single(vista.Citas);
        Assert.Equal((LunesSiguiente, new TimeOnly(17, 30), new TimeOnly(18, 0)), (cita.Fecha, cita.Inicio, cita.Fin));
        Assert.True(cita.DelConsultorio);
        Assert.Equal([Miercoles], vista.FechasConCustom);
    }

    [Fact]
    public async Task Guardar_cambia_solo_esos_dias_sin_cancelar_y_deja_una_bitacora_por_dia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await SembrarAsync(escenario);

        var resultado = await CambiarAsync(escenario, Semana(CortoLunesMiercolesViernes()));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], resultado.Valor.DiasCambiados);
        Assert.Equal([Miercoles], resultado.Valor.FechasConCustom);
        Assert.All(await escenario.CitasAsync(), c => Assert.Equal("Agendada", c.Estado));
        Assert.Equal(1L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom"));

        var bitacora = await escenario.FilasAsync(
            "select actor_tipo, actor_id, entidad, entidad_id, accion, detalle::text from agenda.bitacora order by entidad_id",
            l => (Actor: l.GetString(0), Id: l.GetString(1), Entidad: l.GetString(2), Dia: l.GetString(3), Accion: l.GetString(4), Detalle: l.GetString(5)));
        Assert.Equal(["1", "3", "5"], bitacora.Select(b => b.Dia));
        Assert.All(bitacora, b => Assert.Equal(("Administrador", IdDelAdministrador, "AgendaBase", "Cambiada"), (b.Actor, b.Id, b.Entidad, b.Accion)));
        using var detalle = JsonDocument.Parse(bitacora[0].Detalle);
        Assert.Equal("18:00", detalle.RootElement.GetProperty("antes")[0].GetProperty("fin").GetString());
        Assert.Equal("17:00", detalle.RootElement.GetProperty("despues")[0].GetProperty("fin").GetString());

        // La siguiente reserva usa la base nueva: el lunes termina a las 4:30; el martes sigue hasta las 5:30.
        var dias = await HorariosAsync(escenario);
        Assert.Equal(new TimeOnly(16, 30), dias.Single(d => d.Fecha == LunesSiguiente).Horas[^1]);
        Assert.Equal(new TimeOnly(17, 30), dias.Single(d => d.Fecha == Manana).Horas[^1]);
    }

    [Fact]
    public async Task La_misma_semana_no_cambia_nada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await CambiarAsync(escenario, Semana([]));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Empty(resultado.Valor.DiasCambiados);
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"));
    }

    [Fact]
    public async Task Un_dia_encimado_o_corto_no_guarda_ninguno()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var encimado = Semana(
        [
            new(DayOfWeek.Monday, [Tramo(15, 30, 17, 0)]),
            new(DayOfWeek.Wednesday, [Tramo(15, 30, 17, 0), Tramo(16, 30, 18, 0)]),
        ]);
        var corto = Semana([new(DayOfWeek.Thursday, [Tramo(15, 30, 15, 45)])]);

        var conEncimado = await CambiarAsync(escenario, encimado);
        var conCorto = await CambiarAsync(escenario, corto);

        Assert.Equal([CambiarAgendaBase.MensajeDeTramosEncimados], conEncimado.Errores[CambiarAgendaBase.PropiedadDelDia(2)]);
        Assert.Equal([CambiarAgendaBase.MensajeDeTramoCorto], conCorto.Errores[CambiarAgendaBase.PropiedadDelDia(3)]);
        Assert.Equal(5L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_base_tramo where fin = '18:00'"));
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"));
    }

    [Fact]
    public async Task Sin_los_siete_dias_una_vez_cada_uno_es_invalido()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var seis = Semana([]);
        seis.Dias.RemoveAt(6);
        var lunesDosVeces = Semana([]);
        lunesDosVeces.Dias[6].Dia = DayOfWeek.Monday;

        Assert.Equal([CambiarAgendaBase.MensajeDeSemana], (await CambiarAsync(escenario, seis)).Errores[nameof(CambiarAgendaBase.Dias)]);
        Assert.Equal(FormaDeResultado.Invalido, (await CambiarAsync(escenario, lunesDosVeces)).Forma);
    }

    [Fact]
    public async Task Sin_sesion_vuelve_no_autorizado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);

        var cambiar = await ambito.ServiceProvider.GetRequiredService<ICommandHandler<CambiarAgendaBase, AgendaBaseCambiada>>()
            .HandleAsync(Semana(CortoLunesMiercolesViernes()), Cancelacion);
        var vista = await ambito.ServiceProvider.GetRequiredService<IQueryHandler<CambioDeAgendaBase, CambioDeBaseRevisado>>()
            .HandleAsync(new CambioDeAgendaBase(CortoLunesMiercolesViernes()), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, cambiar.Forma);
        Assert.Equal(FormaDeResultado.NoAutorizado, vista.Forma);
    }

    // Una Cita del lunes 9 a las 5:30 (queda fuera), una del sábado 7 fuera de horario (día que no cambia), y el
    // miércoles 4 con custom y una Cita a las 5:30 (la base no lo toca).
    private static async Task SembrarAsync(Escenario escenario)
    {
        await escenario.InsertarCitaDelConsultorioAsync(LunesSiguiente, new TimeOnly(17, 30), new TimeOnly(18, 0));
        await escenario.InsertarCitaDelConsultorioAsync(Sabado, new TimeOnly(10, 15), new TimeOnly(10, 45));
        await escenario.InsertarCustomAsync(Miercoles, "junta", (new TimeOnly(15, 30), new TimeOnly(18, 0)));
        await escenario.InsertarCitaDelConsultorioAsync(Miercoles, new TimeOnly(17, 30), new TimeOnly(18, 0));
    }

    private static List<DiaDeLaBase> CortoLunesMiercolesViernes() =>
        [.. new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }.Select(d => new DiaDeLaBase(d, [Tramo(15, 30, 17, 0)]))];

    private static TramoDeLaAgenda Tramo(int h1, int m1, int h2, int m2) => new(new TimeOnly(h1, m1), new TimeOnly(h2, m2));

    // La semana sembrada (lunes a viernes 3:30 a 6:00 pm, fin de semana sin atención) con esos días cambiados.
    private static CambiarAgendaBase Semana(IReadOnlyList<DiaDeLaBase> cambios) => new()
    {
        Dias =
        [
            .. DeLunesADomingo.Select(dia =>
            {
                var tramos = cambios.FirstOrDefault(c => c.Dia == dia)?.Tramos
                    ?? (dia is DayOfWeek.Saturday or DayOfWeek.Sunday ? [] : [Tramo(15, 30, 18, 0)]);
                return new DiaDeLaBaseAGuardar
                {
                    Dia = dia,
                    Tramos = [.. tramos.Select(t => new TramoAGuardar { Inicio = t.Inicio, Fin = t.Fin })],
                };
            }),
        ],
    };

    private static void ComoAdministrador(AsyncServiceScope ambito) =>
        Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, IdDelAdministrador), new Claim(ClaimTypes.Name, "administradora")],
            authenticationType: "Prueba")));

    private static async Task<Resultado<AgendaBaseCambiada>> CambiarAsync(Escenario escenario, CambiarAgendaBase comando)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        return await ambito.ServiceProvider.GetRequiredService<ICommandHandler<CambiarAgendaBase, AgendaBaseCambiada>>()
            .HandleAsync(comando, Cancelacion);
    }

    private static async Task<CambioDeBaseRevisado> VistaAsync(Escenario escenario, IReadOnlyList<DiaDeLaBase> dias)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        var resultado = await ambito.ServiceProvider.GetRequiredService<IQueryHandler<CambioDeAgendaBase, CambioDeBaseRevisado>>()
            .HandleAsync(new CambioDeAgendaBase(dias), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static async Task<IReadOnlyList<DiaConHorarios>> HorariosAsync(Escenario escenario)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>>()
            .HandleAsync(new HorariosLibres(TiposDeVisitaSembrados.PrimeraConsulta), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }
}
