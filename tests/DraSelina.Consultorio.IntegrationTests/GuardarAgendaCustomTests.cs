using System.Security.Claims;
using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// <see cref="GuardarAgendaCustom"/> y <see cref="MesDeAtencion"/> por sus handlers contra PostgreSQL real (CAP-11,
/// FR-13, entrada 6.1): registrar y cambiar la custom de una fecha, las Citas que quedan fuera (AD-6), las invariantes de
/// los Tramos (INV-A2, INV-A6), la bitácora del Administrador (RN-24) y su efecto en <see cref="HorariosLibres"/>. Con el
/// escenario y el reloj de <see cref="ReservaEnLineaTests"/>: lunes 2 de noviembre de 2026 a las 12:00 local; base de
/// lunes a viernes, 3:30 a 6:00 pm; tipos de 30 minutos.
/// </summary>
public sealed class GuardarAgendaCustomTests(Arnes arnes)
{
    private const string IdDelAdministrador = "cuenta-del-administrador";

    private static readonly DateOnly Hoy = new(2026, 11, 2);

    private static readonly DateOnly Miercoles = new(2026, 11, 4);

    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static readonly DateOnly SabadoSiguiente = new(2026, 11, 14);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Abrir_un_sabado_lo_ofrece_en_la_reserva_y_el_siguiente_sigue_cerrado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await GuardarAsync(escenario, Comando(Sabado, "sábado abierto", (10, 0, 13, 0)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(new AgendaCustomGuardada(Sabado, Registrada: true, SinCambios: false), resultado.Valor);

        var enLaAgenda = Assert.Single(await DiasDeLaAgendaAsync(escenario, Sabado));
        Assert.Equal(TipoDeDia.DiaAbierto, enLaAgenda.Tipo);
        Assert.Equal("sábado abierto", enLaAgenda.MotivoDeCustom);

        var dias = await HorariosAsync(escenario);
        var sabado = Assert.Single(dias, d => d.Fecha == Sabado);
        Assert.Equal(new TimeOnly(10, 0), sabado.Horas[0]);
        Assert.Equal(new TimeOnly(12, 30), sabado.Horas[^1]);
        Assert.DoesNotContain(dias, d => d.Fecha == SabadoSiguiente);
    }

    [Fact]
    public async Task Deja_la_bitacora_Registrada_a_nombre_del_Administrador_con_el_despues()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        Assert.True((await GuardarAsync(escenario, Comando(Sabado, "sábado abierto", (10, 0, 13, 0)))).EsExito);

        var entrada = Assert.Single(await BitacoraAsync(escenario));
        Assert.Equal("Administrador", entrada.ActorTipo);
        Assert.Equal(IdDelAdministrador, entrada.ActorId);
        Assert.Equal("AgendaCustom", entrada.Entidad);
        Assert.Equal("2026-11-07", entrada.EntidadId);
        Assert.Equal("Registrada", entrada.Accion);

        using var detalle = JsonDocument.Parse(entrada.Detalle);
        Assert.Equal(JsonValueKind.Null, detalle.RootElement.GetProperty("antes").ValueKind);
        var despues = detalle.RootElement.GetProperty("despues");
        Assert.Equal("sábado abierto", despues.GetProperty("motivo").GetString());
        var tramo = Assert.Single(despues.GetProperty("tramos").EnumerateArray());
        Assert.Equal("10:00", tramo.GetProperty("inicio").GetString());
        Assert.Equal("13:00", tramo.GetProperty("fin").GetString());
    }

    [Fact]
    public async Task Cerrar_un_dia_sin_Citas_lo_deja_sin_atencion()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await GuardarAsync(escenario, Comando(Miercoles, "vacaciones"));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.DoesNotContain(await HorariosAsync(escenario), d => d.Fecha == Miercoles);
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom_tramo"));

        var miercoles = Assert.Single(await MesAsync(escenario, 2026, 11), d => d.Fecha == Miercoles);
        Assert.Equal(TipoDeDia.CustomSinAtencion, miercoles.Tipo);
        Assert.Equal("vacaciones", miercoles.MotivoDeCustom);
        Assert.Empty(miercoles.Tramos);
    }

    [Fact]
    public async Task Cambiar_la_custom_deja_la_bitacora_Cambiada_con_el_antes_y_el_despues()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCustomAsync(Sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(13, 0)));

        var resultado = await GuardarAsync(escenario, Comando(Sabado, "sábado corto", (10, 0, 12, 0)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(new AgendaCustomGuardada(Sabado, Registrada: false, SinCambios: false), resultado.Valor);
        Assert.Equal("sábado corto", await escenario.EscalarAsync<string>("select motivo from agenda.agenda_custom"));
        Assert.Equal(new TimeOnly(12, 0), await escenario.EscalarAsync<TimeOnly>("select fin from agenda.agenda_custom_tramo"));

        var entrada = Assert.Single(await BitacoraAsync(escenario));
        Assert.Equal("Cambiada", entrada.Accion);
        using var detalle = JsonDocument.Parse(entrada.Detalle);
        Assert.Equal("sábado abierto", detalle.RootElement.GetProperty("antes").GetProperty("motivo").GetString());
        Assert.Equal("sábado corto", detalle.RootElement.GetProperty("despues").GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task Los_mismos_Tramos_y_motivo_son_exito_sin_bitacora()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCustomAsync(Sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(13, 0)));

        var resultado = await GuardarAsync(escenario, Comando(Sabado, " sábado abierto ", (10, 0, 13, 0)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.True(resultado.Valor.SinCambios);
        Assert.Empty(await BitacoraAsync(escenario));
    }

    [Fact]
    public async Task Una_Cita_que_queda_fuera_devuelve_conflicto_y_no_guarda_nada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(17, 30), new TimeOnly(18, 0));

        var resultado = await GuardarAsync(escenario, Comando(Manana, "cirugía", (15, 30, 17, 0)));

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.CitasFuera, resultado.Codigo);
        await SinCambiosAsync(escenario);
        Assert.Equal("Agendada", Assert.Single(await escenario.CitasAsync()).Estado);
    }

    [Fact]
    public async Task Una_Cita_que_cabe_en_un_Tramo_nuevo_no_cuenta()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(16, 0), new TimeOnly(16, 30));

        var resultado = await GuardarAsync(escenario, Comando(Manana, "cirugía", (15, 30, 17, 0)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
    }

    [Fact]
    public async Task Una_Cita_cancelada_o_que_ya_empezo_no_cuenta()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        // Hoy a las 11:00: ya empezó (son las 12:00). Mañana a las 16:00: cancelada.
        await escenario.InsertarCitaDelConsultorioAsync(Hoy, new TimeOnly(11, 0), new TimeOnly(11, 30));
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(16, 0), new TimeOnly(16, 30));
        await escenario.EjecutarAsync(
            $"update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'consultorio', cancelada_en = now() "
            + $"where inicio = '{HoraLocal.AUtc(Manana, new TimeOnly(16, 0)):O}'");

        Assert.True((await GuardarAsync(escenario, Comando(Hoy, "asunto personal"))).EsExito);
        Assert.True((await GuardarAsync(escenario, Comando(Manana, "asunto personal"))).EsExito);
    }

    [Fact]
    public async Task Tramos_encimados_se_rechazan_sin_guardar()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await GuardarAsync(escenario, Comando(Sabado, "sábado abierto", (10, 0, 12, 0), (11, 0, 13, 0)));

        // Bajo cada Tramo del par, el otro (entrada 6.10).
        Assert.Equal([GuardarAgendaCustom.MensajeDeEncimado(2)], resultado.Errores[GuardarAgendaCustom.PropiedadDelTramo(0)]);
        Assert.Equal([GuardarAgendaCustom.MensajeDeEncimado(1)], resultado.Errores[GuardarAgendaCustom.PropiedadDelTramo(1)]);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task De_tres_Tramos_solo_los_dos_encimados_llevan_el_error()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await GuardarAsync(escenario, Comando(Sabado, "sábado abierto", (10, 0, 11, 0), (12, 0, 14, 0), (13, 0, 15, 0)));

        Assert.Equal(
            [GuardarAgendaCustom.PropiedadDelTramo(1), GuardarAgendaCustom.PropiedadDelTramo(2)],
            resultado.Errores.Keys.Order());
        Assert.Equal([GuardarAgendaCustom.MensajeDeEncimado(3)], resultado.Errores[GuardarAgendaCustom.PropiedadDelTramo(1)]);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Un_Tramo_donde_no_cabe_una_Cita_se_rechaza_sin_guardar()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await GuardarAsync(escenario, Comando(Sabado, "sábado abierto", (10, 0, 10, 15)));

        Assert.Equal(CodigosDeAgenda.TramoCorto, resultado.Codigo);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Sin_motivo_o_con_un_Tramo_al_reves_es_invalido_sin_guardar()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var sinMotivo = await GuardarAsync(escenario, Comando(Sabado, "   ", (10, 0, 13, 0)));
        var alReves = await GuardarAsync(escenario, Comando(Sabado, "sábado abierto", (13, 0, 10, 0)));
        var incompleto = Comando(Sabado, "sábado abierto");
        incompleto.Tramos.Add(new TramoAGuardar { Inicio = new TimeOnly(10, 0) });
        var sinFin = await GuardarAsync(escenario, incompleto);

        Assert.Equal(FormaDeResultado.Invalido, sinMotivo.Forma);
        Assert.Equal([GuardarAgendaCustom.MensajeDeMotivo], sinMotivo.Errores[nameof(GuardarAgendaCustom.Motivo)]);
        Assert.Equal([GuardarAgendaCustom.MensajeDeTramoAlReves], alReves.Errores[GuardarAgendaCustom.PropiedadDelTramo(0)]);
        Assert.Equal([GuardarAgendaCustom.MensajeDeTramoIncompleto], sinFin.Errores[GuardarAgendaCustom.PropiedadDelTramo(0)]);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Una_fecha_pasada_se_rechaza_y_hoy_se_acepta()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var ayer = await GuardarAsync(escenario, Comando(Hoy.AddDays(-1), "domingo"));

        Assert.Equal(CodigosDeAgenda.FechaPasada, ayer.Codigo);
        await SinCambiosAsync(escenario);
        Assert.True((await GuardarAsync(escenario, Comando(Hoy, "junta", (15, 30, 16, 30)))).EsExito);
    }

    [Fact]
    public async Task Sin_sesion_vuelve_no_autorizado_y_no_guarda()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);

        var guardar = await ambito.ServiceProvider.GetRequiredService<ICommandHandler<GuardarAgendaCustom, AgendaCustomGuardada>>()
            .HandleAsync(Comando(Sabado, "sábado abierto", (10, 0, 13, 0)), Cancelacion);
        var mes = await ambito.ServiceProvider.GetRequiredService<IQueryHandler<MesDeAtencion, IReadOnlyList<DiaDeAtencion>>>()
            .HandleAsync(new MesDeAtencion(2026, 11), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, guardar.Forma);
        Assert.Equal(FormaDeResultado.NoAutorizado, mes.Forma);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task El_mes_distingue_cada_dia_con_su_motivo_y_sus_Tramos()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCustomAsync(Sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(13, 0)));
        await escenario.InsertarCustomAsync(Manana, "cirugía", (new TimeOnly(15, 30), new TimeOnly(17, 0)));
        await escenario.InsertarCustomAsync(Miercoles, "vacaciones");

        var dias = await MesAsync(escenario, 2026, 11);

        Assert.Equal(30, dias.Count);
        Assert.Equal(new DateOnly(2026, 11, 1), dias[0].Fecha);
        Assert.Equal(TipoDeDia.Normal, dias.Single(d => d.Fecha == Hoy).Tipo);
        Assert.Equal([new TramoDeLaAgenda(new(15, 30), new(18, 0))], dias.Single(d => d.Fecha == Hoy).Tramos);
        Assert.Equal(TipoDeDia.SinAtencion, dias.Single(d => d.Fecha == SabadoSiguiente).Tipo);
        Assert.Equal(TipoDeDia.HorarioEspecial, dias.Single(d => d.Fecha == Manana).Tipo);
        Assert.Equal(TipoDeDia.CustomSinAtencion, dias.Single(d => d.Fecha == Miercoles).Tipo);
        var sabado = dias.Single(d => d.Fecha == Sabado);
        Assert.Equal(TipoDeDia.DiaAbierto, sabado.Tipo);
        Assert.Equal("sábado abierto", sabado.MotivoDeCustom);
        Assert.DoesNotContain("sábado abierto", sabado.ToString());
    }

    [Fact]
    public async Task Un_mes_fuera_de_rango_es_invalido()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        var consulta = ambito.ServiceProvider.GetRequiredService<IQueryHandler<MesDeAtencion, IReadOnlyList<DiaDeAtencion>>>();

        Assert.Equal(FormaDeResultado.Invalido, (await consulta.HandleAsync(new MesDeAtencion(2026, 13), Cancelacion)).Forma);
        Assert.Equal(FormaDeResultado.Invalido, (await consulta.HandleAsync(new MesDeAtencion(1999, 1), Cancelacion)).Forma);
    }

    private static GuardarAgendaCustom Comando(DateOnly fecha, string motivo, params (int H1, int M1, int H2, int M2)[] tramos) => new()
    {
        Fecha = fecha,
        Motivo = motivo,
        Tramos = [.. tramos.Select(t => new TramoAGuardar { Inicio = new TimeOnly(t.H1, t.M1), Fin = new TimeOnly(t.H2, t.M2) })],
    };

    private static void ComoAdministrador(AsyncServiceScope ambito) =>
        Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, IdDelAdministrador), new Claim(ClaimTypes.Name, "administradora")],
            authenticationType: "Prueba")));

    private static async Task<Resultado<AgendaCustomGuardada>> GuardarAsync(Escenario escenario, GuardarAgendaCustom comando)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        return await ambito.ServiceProvider.GetRequiredService<ICommandHandler<GuardarAgendaCustom, AgendaCustomGuardada>>()
            .HandleAsync(comando, Cancelacion);
    }

    private static async Task<IReadOnlyList<DiaDeAtencion>> MesAsync(Escenario escenario, int anio, int mes)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        var resultado = await ambito.ServiceProvider.GetRequiredService<IQueryHandler<MesDeAtencion, IReadOnlyList<DiaDeAtencion>>>()
            .HandleAsync(new MesDeAtencion(anio, mes), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static async Task<IReadOnlyList<DiaDeLaAgenda>> DiasDeLaAgendaAsync(Escenario escenario, DateOnly fecha)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        var resultado = await ambito.ServiceProvider.GetRequiredService<IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>>()
            .HandleAsync(DiasDeLaAgenda.DelDia(fecha), Cancelacion);
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

    private static Task<List<(string ActorTipo, string? ActorId, string Entidad, string EntidadId, string Accion, string Detalle)>> BitacoraAsync(
        Escenario escenario) =>
        escenario.FilasAsync(
            "select actor_tipo, actor_id, entidad, entidad_id, accion, detalle::text from agenda.bitacora",
            l => (l.GetString(0), l.IsDBNull(1) ? null : l.GetString(1), l.GetString(2), l.GetString(3), l.GetString(4), l.GetString(5)));

    // Ninguna custom ni bitácora: lo rechazado no deja rastro.
    private static async Task SinCambiosAsync(Escenario escenario)
    {
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom"));
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"));
    }
}
