using System.Net;
using System.Security.Claims;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;
using PaginaDelDetalle = DraSelina.Consultorio.UI.Pages.Administracion.DetalleDeCita;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// El Detalle de Cita (entrada 6) contra PostgreSQL real: la consulta <see cref="DetalleDeCita"/> en cada fila de datos
/// de la matriz del plan (datos completos, Atendida, del consultorio, sin correo, cancelada por el Consultorio, No se
/// presentó, reprogramada, cancelada tarde, inexistente y sin sesión), la bitácora en orden con lo que su detalle guarda,
/// y por HTTP: la Agenda enlaza cada tarjeta y la página se pinta con sesión, o lleva a entrar sin ella. Los comandos
/// corren por sus handlers en un ámbito con la sesión que pide la página. Con el escenario y el reloj de
/// <see cref="ReservaEnLineaTests"/>: lunes 2 de noviembre de 2026 a las 12:00 local; la Cita, en línea, el martes 3 a
/// las 16:00 local, de Ana con correo.
/// </summary>
public sealed class DetalleDeCitaTests(Arnes arnes)
{
    private const string IdDelAdministrador = "cuenta-del-administrador";

    private static readonly DateTimeOffset Inicio = HoraLocal.AUtc(Manana, new TimeOnly(16, 0));

    private static readonly DateOnly Jueves = new(2026, 11, 5);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Una_Cita_futura_en_linea_trae_sus_datos_completos_solo_Cancelar_y_su_Agendada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        var guardada = Assert.Single(await escenario.CitasAsync());

        var detalle = await DetalleAsync(escenario, cita.CitaId);

        Assert.Equal(cita.CitaId, detalle.CitaId);
        Assert.Equal(new ContactoDelPaciente("Ana Sofía López", "+524441234567", "ana@correo.com"), detalle.Paciente);
        Assert.False(detalle.SinCorreo);
        Assert.Equal("Primera consulta", detalle.TipoDeVisita);
        Assert.Equal(Manana, detalle.Fecha);
        Assert.Equal(new TimeOnly(16, 0), detalle.Inicio);
        Assert.Equal(new TimeOnly(16, 30), detalle.Fin);
        Assert.Equal(OrigenEnLaAgenda.EnLinea, detalle.Origen);
        Assert.Null(detalle.Nota);
        Assert.Equal("Dolor de rodilla derecha", detalle.MotivoDeConsulta);
        var (fechaDelConsentimiento, horaDelConsentimiento) = HoraLocal.DesdeUtc(guardada.ConsentimientoEn);
        Assert.Equal(
            new ConsentimientoEnDetalle(CanalEnDetalle.EnLinea, fechaDelConsentimiento, horaDelConsentimiento, guardada.ConsentimientoVersion),
            detalle.Consentimiento);
        Assert.Equal(EstadoEnLaAgenda.Agendada, detalle.Estado);
        Assert.Null(detalle.Cancelacion);
        Assert.False(detalle.PuedeMarcarNoSePresento);
        Assert.True(detalle.PuedeCancelar);

        var entrada = Assert.Single(detalle.Bitacora);
        Assert.Equal(
            new EntradaDeLaBitacora(new DateOnly(2026, 11, 2), new TimeOnly(12, 0), Actor.Paciente, AccionEnLaBitacora.Agendada,
                OrigenEnLaAgenda.EnLinea, null, null, null),
            entrada);
        Assert.Equal("Agendada · en línea · Paciente", PaginaDelDetalle.Que(entrada));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public async Task Una_Cita_que_ya_empezo_se_ve_Atendida_y_solo_ofrece_No_se_presento(int minutosDespues)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(minutosDespues) - Ahora);

        var detalle = await DetalleAsync(escenario, cita.CitaId);

        Assert.Equal(EstadoEnLaAgenda.Atendida, detalle.Estado);
        Assert.True(detalle.PuedeMarcarNoSePresento);
        Assert.False(detalle.PuedeCancelar);
    }

    [Fact]
    public async Task Una_Cita_del_consultorio_trae_su_origen_su_nota_su_Consentimiento_externo_y_su_Agendada_del_Administrador()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = new AgendarCitaPorConsultorio
        {
            TipoDeVisitaId = TiposDeVisitaSembrados.PrimeraConsulta,
            Fecha = Manana,
            Hora = new TimeOnly(19, 0),
            Nombre = "Bruno Díaz",
            Telefono = "444 555 1000",
            Nota = "lo pidió Selina",
            Consentimiento = true,
        };
        var agendada = await EnviarAsync<AgendarCitaPorConsultorio, CitaAgendadaPorConsultorio>(escenario, comando, ComoAdministrador);
        Assert.True(agendada.EsExito, $"{agendada.Forma} {agendada.Codigo}");

        var detalle = await DetalleAsync(escenario, agendada.Valor.CitaId);

        Assert.Equal(OrigenEnLaAgenda.DelConsultorio, detalle.Origen);
        Assert.Equal("lo pidió Selina", detalle.Nota);
        Assert.Null(detalle.MotivoDeConsulta);
        Assert.Equal(CanalEnDetalle.Externo, detalle.Consentimiento.Canal);
        Assert.Equal(new ContactoDelPaciente("Bruno Díaz", "+524445551000", null), detalle.Paciente);
        Assert.True(detalle.SinCorreo);
        var entrada = Assert.Single(detalle.Bitacora);
        Assert.Equal((Actor.Administrador, AccionEnLaBitacora.Agendada, (OrigenEnLaAgenda?)OrigenEnLaAgenda.DelConsultorio), (entrada.Quien, entrada.Accion, entrada.Origen));
        Assert.Equal("Agendada · del consultorio · Administrador", PaginaDelDetalle.Que(entrada));
    }

    [Fact]
    public async Task Sin_correo_lo_dice_y_su_contacto_trae_nombre_y_telefono()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando();
        comando.Correo = null;
        var agendada = await escenario.AgendarAsync(comando);
        Assert.True(agendada.EsExito, $"{agendada.Forma} {agendada.Codigo}");

        var detalle = await DetalleAsync(escenario, agendada.Valor.CitaId);

        Assert.True(detalle.SinCorreo);
        Assert.Equal(new ContactoDelPaciente("Ana Sofía López", "+524441234567", null), detalle.Paciente);
    }

    [Fact]
    public async Task Reservada_en_linea_y_cancelada_por_el_Consultorio_su_bitacora_va_en_orden_y_ya_no_ofrece_acciones()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(TimeSpan.FromHours(1));

        var cancelada = await EnviarAsync<CancelarCitaPorConsultorio, CitaCanceladaPorConsultorio>(
            escenario, new() { CitaId = cita.CitaId }, ComoAdministrador);
        Assert.True(cancelada.EsExito, $"{cancelada.Forma} {cancelada.Codigo}");

        var detalle = await DetalleAsync(escenario, cita.CitaId);

        Assert.Equal(EstadoEnLaAgenda.Cancelada, detalle.Estado);
        Assert.Equal(CancelacionEnLaAgenda.PorElConsultorio, detalle.Cancelacion);
        Assert.False(detalle.PuedeMarcarNoSePresento);
        Assert.False(detalle.PuedeCancelar);
        Assert.Equal(
            ["Agendada · en línea · Paciente", "Cancelada · por el Consultorio · Administrador"],
            detalle.Bitacora.Select(e => PaginaDelDetalle.Que(e)));
        Assert.Equal(
            new EntradaDeLaBitacora(new DateOnly(2026, 11, 2), new TimeOnly(13, 0), Actor.Administrador, AccionEnLaBitacora.Cancelada,
                null, CancelacionEnLaAgenda.PorElConsultorio, null, null),
            detalle.Bitacora[1]);
    }

    [Fact]
    public async Task Marcada_No_se_presento_ya_no_ofrece_acciones_y_su_bitacora_lo_suma()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(10) - Ahora);

        var marcada = await EnviarAsync<MarcarNoSePresento, CitaMarcadaNoSePresento>(
            escenario, new() { CitaId = cita.CitaId }, ComoAdministrador);
        Assert.True(marcada.EsExito, $"{marcada.Forma} {marcada.Codigo}");

        var detalle = await DetalleAsync(escenario, cita.CitaId);

        Assert.Equal(EstadoEnLaAgenda.NoSePresento, detalle.Estado);
        Assert.Null(detalle.Cancelacion);
        Assert.False(detalle.PuedeMarcarNoSePresento);
        Assert.False(detalle.PuedeCancelar);
        Assert.Equal(
            ["Agendada · en línea · Paciente", "No se presentó · Administrador"],
            detalle.Bitacora.Select(e => PaginaDelDetalle.Que(e)));
        Assert.Equal((new DateOnly(2026, 11, 3), new TimeOnly(16, 10)), (detalle.Bitacora[1].Fecha, detalle.Bitacora[1].Hora));
    }

    [Fact]
    public async Task Reprogramada_por_el_Paciente_su_bitacora_dice_de_que_fecha_y_hora_a_cual()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(TimeSpan.FromMinutes(5));

        var reprogramada = await EnviarAsync<ReprogramarCitaPorEnlace, CitaReprogramadaPorEnlace>(
            escenario, new() { Token = Token(cita), Fecha = Jueves, Hora = new TimeOnly(17, 0) }, SinSesion);
        Assert.True(reprogramada.EsExito, $"{reprogramada.Forma} {reprogramada.Codigo}");

        var detalle = await DetalleAsync(escenario, cita.CitaId);

        Assert.Equal((Jueves, new TimeOnly(17, 0)), (detalle.Fecha, detalle.Inicio));
        Assert.Equal(
            [AccionEnLaBitacora.Agendada, AccionEnLaBitacora.Reprogramada],
            detalle.Bitacora.Select(e => e.Accion));
        var entrada = detalle.Bitacora[1];
        Assert.Equal(Actor.Paciente, entrada.Quien);
        Assert.Equal(new MomentoLocal(Manana, new TimeOnly(16, 0)), entrada.De);
        Assert.Equal(new MomentoLocal(Jueves, new TimeOnly(17, 0)), entrada.A);
        Assert.Equal(
            "Reprogramada de 3 de noviembre, 4:00 pm a 5 de noviembre, 5:00 pm · Paciente",
            PaginaDelDetalle.Que(entrada));
    }

    [Fact]
    public async Task Cancelada_tarde_por_el_Paciente_se_ve_tardia_sin_acciones()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(-60) - Ahora);

        var cancelada = await EnviarAsync<CancelarCitaPorEnlace, CitaCanceladaPorEnlace>(escenario, new() { Token = Token(cita) }, SinSesion);
        Assert.True(cancelada.EsExito, $"{cancelada.Forma} {cancelada.Codigo}");

        var detalle = await DetalleAsync(escenario, cita.CitaId);

        Assert.Equal(EstadoEnLaAgenda.Cancelada, detalle.Estado);
        Assert.Equal(CancelacionEnLaAgenda.Tardia, detalle.Cancelacion);
        Assert.False(detalle.PuedeMarcarNoSePresento);
        Assert.False(detalle.PuedeCancelar);
        Assert.Equal(
            ["Agendada · en línea · Paciente", "Cancelada · tardía · Paciente"],
            detalle.Bitacora.Select(e => PaginaDelDetalle.Que(e)));
    }

    [Fact]
    public async Task Una_Cita_que_no_existe_vuelve_cita_no_encontrada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAsync(escenario);

        var resultado = await ConsultarAsync(escenario, CitaId.Nuevo(), ComoAdministrador);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.CitaNoEncontrada, resultado.Codigo);
    }

    [Fact]
    public async Task Sin_sesion_la_consulta_vuelve_no_autorizado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);

        var resultado = await ConsultarAsync(escenario, cita.CitaId, SinSesion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
    }

    [Fact]
    public async Task Una_Cita_cuyo_Paciente_no_existe_se_muestra_sin_contacto_y_sin_correo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(9, 0), new TimeOnly(9, 30));
        var id = new CitaId(Assert.Single(await escenario.CitasAsync()).Id);

        var detalle = await DetalleAsync(escenario, id);

        Assert.Null(detalle.Paciente);
        Assert.True(detalle.SinCorreo);
        Assert.Empty(detalle.Bitacora);
    }

    [Fact]
    public async Task El_Detalle_no_muestra_contacto_motivo_ni_nota_al_convertirse_en_texto()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);

        var detalle = await DetalleAsync(escenario, cita.CitaId) with { Nota = "lo pidió Selina" };
        var texto = string.Join(" ", detalle.ToString(), detalle.Paciente!.ToString(), new DetalleDeCita(cita.CitaId).ToString());

        foreach (var dato in new[] { "Ana", "4441234567", "ana@correo.com", "rodilla", "Selina" })
        {
            Assert.DoesNotContain(dato, texto, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(cita.CitaId.ToString(), texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_sesion_cada_tarjeta_de_la_Agenda_abre_su_Detalle_que_se_pinta_con_su_bitacora()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = await EntrarConSegundoFactorTests.ClienteConSesionAsync(escenario.Web);
        var direccion = $"/admin/citas/{cita.CitaId.Valor}";

        // En vista de día y de semana.
        Assert.Contains($"href=\"{direccion}\"", await LeerAsync(cliente, "/admin?fecha=2026-11-03&vista=dia"), StringComparison.Ordinal);
        Assert.Contains($"href=\"{direccion}\"", await LeerAsync(cliente, "/admin?fecha=2026-11-03&vista=semana"), StringComparison.Ordinal);

        var pagina = await LeerAsync(cliente, direccion);
        Assert.Contains("Ana Sofía López", pagina, StringComparison.Ordinal);
        Assert.Contains("Agendada · en línea · Paciente", pagina, StringComparison.Ordinal);
        Assert.Contains(PaginaDelDetalle.CancelarCitaTexto, pagina, StringComparison.Ordinal);
        Assert.Contains("href=\"/admin?fecha=2026-11-03&vista=dia\"", pagina, StringComparison.Ordinal);
        Assert.Contains(PaginaDelDetalle.VolverALaAgenda, pagina, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_sesion_una_Cita_que_no_existe_dice_que_no_existe_con_el_enlace_a_la_Agenda()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        using var cliente = await EntrarConSegundoFactorTests.ClienteConSesionAsync(escenario.Web);

        var pagina = await LeerAsync(cliente, $"/admin/citas/{Guid.CreateVersion7()}");

        Assert.Contains(PaginaDelDetalle.NoExiste, pagina, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/admin\" class=\"dsm-volver\">", pagina, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sin_sesion_la_pagina_lleva_a_entrar_con_su_direccion_de_regreso()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = EntrarConSegundoFactorTests.Cliente(escenario.Web);

        using var respuesta = await cliente.GetAsync($"/admin/citas/{cita.CitaId.Valor}", Cancelacion);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(
            $"/admin/entrar?ReturnUrl=%2Fadmin%2Fcitas%2F{cita.CitaId.Valor}",
            respuesta.Headers.Location!.IsAbsoluteUri ? respuesta.Headers.Location.PathAndQuery : respuesta.Headers.Location.OriginalString);
    }

    private static async Task<CitaAgendadaEnLinea> AgendarAsync(Escenario escenario)
    {
        var resultado = await escenario.AgendarAsync(Comando(Manana, new TimeOnly(16, 0)));
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static string Token(CitaAgendadaEnLinea cita) =>
        cita.EnlaceDeGestion[(cita.EnlaceDeGestion.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];

    private static async Task<CitaEnDetalle> DetalleAsync(Escenario escenario, CitaId cita)
    {
        var resultado = await ConsultarAsync(escenario, cita, ComoAdministrador);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    /// <summary>La consulta por su interfaz, en un ámbito propio con la sesión que se pida, como la pide la página.</summary>
    private static async Task<Resultado<CitaEnDetalle>> ConsultarAsync(Escenario escenario, CitaId cita, Action<AsyncServiceScope> sesion)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        sesion(ambito);
        return await ambito.ServiceProvider
            .GetRequiredService<IQueryHandler<DetalleDeCita, CitaEnDetalle>>()
            .HandleAsync(new DetalleDeCita(cita), Cancelacion);
    }

    private static async Task<Resultado<TResultado>> EnviarAsync<TComando, TResultado>(
        Escenario escenario, TComando comando, Action<AsyncServiceScope> sesion)
        where TComando : ICommand<TResultado>
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        sesion(ambito);
        return await ambito.ServiceProvider
            .GetRequiredService<ICommandHandler<TComando, TResultado>>()
            .HandleAsync(comando, Cancelacion);
    }

    private static void ComoAdministrador(AsyncServiceScope ambito) =>
        ((IHostEnvironmentAuthenticationStateProvider)ambito.ServiceProvider.GetRequiredService<AuthenticationStateProvider>())
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, IdDelAdministrador), new Claim(ClaimTypes.Name, "administradora")],
                authenticationType: "Prueba")))));

    private static async Task<string> LeerAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {ruta}: {respuesta.StatusCode}");
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }
}
