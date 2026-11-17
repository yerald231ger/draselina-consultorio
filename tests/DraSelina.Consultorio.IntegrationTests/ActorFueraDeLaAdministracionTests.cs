using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Quién actúa en el sitio público cuando el mismo navegador tiene abierta la administración (AD-22; entrada 5.10): la
/// reserva en <c>/reservar</c>, la reprogramación y la cancelación por el Enlace de gestión son del Paciente aunque la petición traiga la
/// cookie de sesión. Por HTTP contra la web real, con y sin sesión. El reloj está en el lunes 2 de noviembre de 2026 a
/// las 18:00 UTC y la Cita, el martes 3 a las 16:00 local.
/// </summary>
public sealed partial class ActorFueraDeLaAdministracionTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private static string RutaDeTusDatos =>
        $"/reservar/datos?tipo={TiposDeVisitaSembrados.PrimeraConsulta}&fecha=2026-11-03&hora=16:00";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reservar_y_cancelar_por_el_enlace_son_del_Paciente_con_o_sin_la_sesion_de_administracion(bool conSesion)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        using var cliente = conSesion
            ? await EntrarConSegundoFactorTests.ClienteConSesionAsync(escenario.Web)
            : EntrarConSegundoFactorTests.Cliente(escenario.Web);

        // Reservar en /reservar.
        var confirmada = await ReservarAsync(escenario, cliente);
        Assert.Contains("Tu cita está confirmada", confirmada, StringComparison.Ordinal);
        var cita = Assert.Single(await escenario.CitasAsync());

        var agendada = Assert.Single(await BitacoraAsync(escenario, "Agendada"));
        Assert.Equal(("Paciente", null), agendada);
        Assert.Equal("Paciente", ActorDelEvento(Assert.Single(await EventosAsync(escenario, cita.Id, nameof(CitaAgendada)))));

        // Reprogramar por /c/<token>/reprogramar al jueves 5 a las 17:00, con el mismo navegador.
        await ReprogramarPorElEnlaceAsync(escenario, cliente, cita.Id);

        Assert.Equal(HoraLocal.AUtc(new DateOnly(2026, 11, 5), new TimeOnly(17, 0)), Assert.Single(await escenario.CitasAsync()).Inicio);
        var reprogramada = Assert.Single(await BitacoraAsync(escenario, "Reprogramada"));
        Assert.Equal(("Paciente", null), reprogramada);
        Assert.Equal("Paciente", ActorDelEvento(Assert.Single(await EventosAsync(escenario, cita.Id, nameof(CitaReprogramada)))));

        // Cancelar por /c/<token>/cancelar, con el mismo navegador.
        await CancelarPorElEnlaceAsync(escenario, cliente, cita.Id);

        Assert.Equal("Cancelada", Assert.Single(await escenario.CitasAsync()).Estado);
        var cancelada = Assert.Single(await BitacoraAsync(escenario, "Cancelada"));
        Assert.Equal(("Paciente", null), cancelada);
        Assert.Equal("Paciente", ActorDelEvento(Assert.Single(await EventosAsync(escenario, cita.Id, nameof(CitaCancelada)))));

        // El Detalle de esa Cita, en la administración, lo dice.
        using var administracion = conSesion ? null : await EntrarConSegundoFactorTests.ClienteConSesionAsync(escenario.Web);
        var detalle = await LeerAsync(administracion ?? cliente, $"/admin/citas/{cita.Id}");
        Assert.Contains("Agendada · en línea · Paciente", detalle, StringComparison.Ordinal);
        Assert.Contains("Reprogramada de 3 de noviembre, 4:00 pm a 5 de noviembre, 5:00 pm · Paciente", detalle, StringComparison.Ordinal);
        Assert.Contains("Cancelada · por el Paciente · Paciente", detalle, StringComparison.Ordinal);
        Assert.DoesNotContain("· Administrador", detalle, StringComparison.Ordinal);
    }

    // Pide Tus datos, llena su formulario (los ocultos, con el token antifalsificación, y los datos) y lo envía después
    // de lo que tarda una persona (la verificación anti-bots). Devuelve el HTML de la respuesta.
    private static async Task<string> ReservarAsync(Escenario escenario, HttpClient cliente)
    {
        var pagina = await LeerAsync(cliente, RutaDeTusDatos);
        var formulario = FormularioDeTusDatos().Match(pagina);
        Assert.True(formulario.Success, "Tus datos no tiene formulario.");
        var destino = Accion().Match(formulario.Groups["apertura"].Value);
        var accion = destino.Success ? WebUtility.HtmlDecode(destino.Groups["accion"].Value) : RutaDeTusDatos;
        var campos = Ocultos().Matches(formulario.Value)
            .ToDictionary(m => m.Groups["nombre"].Value, m => WebUtility.HtmlDecode(m.Groups["valor"].Value));
        campos["Comando.Nombre"] = "Ana Sofía López";
        campos["Comando.Telefono"] = "444 123 4567";
        campos["Comando.Correo"] = "ana@correo.com";
        campos["Comando.Motivo"] = "Dolor de rodilla derecha";
        campos["Comando.Consentimiento"] = "true";

        escenario.Reloj.Avanzar(Escenario.LoQueTardaUnaPersona);
        return await EnviarAsync(cliente, accion, campos);
    }

    // Elige el jueves 5 a las 17:00 en Reprogramar y confirma.
    private static Task ReprogramarPorElEnlaceAsync(Escenario escenario, HttpClient cliente, Guid citaId) =>
        EnviarElFormularioAsync(cliente, $"{TuCita(escenario, citaId)}/reprogramar?fecha=2026-11-05&hora=17:00");

    private static Task CancelarPorElEnlaceAsync(Escenario escenario, HttpClient cliente, Guid citaId) =>
        EnviarElFormularioAsync(cliente, $"{TuCita(escenario, citaId)}/cancelar");

    // La ruta de Tu cita (/c/<token>) de la Cita, armada por Agenda.
    private static string TuCita(Escenario escenario, Guid citaId)
    {
        using var ambito = escenario.Web.Services.CreateScope();
        var enlace = ambito.ServiceProvider.GetRequiredService<IAgendaApi>().ObtenerEnlaceDeGestion(new CitaId(citaId));
        return new Uri(enlace).AbsolutePath;
    }

    // Abre una página del Enlace de gestión y envía su formulario de confirmar con sus campos ocultos.
    private static async Task EnviarElFormularioAsync(HttpClient cliente, string ruta)
    {
        var pregunta = await LeerAsync(cliente, ruta);
        var formulario = FormularioDeConfirmar().Match(pregunta);
        Assert.True(formulario.Success, $"{ruta} no tiene formulario de confirmar.");
        var campos = Ocultos().Matches(formulario.Value)
            .ToDictionary(m => m.Groups["nombre"].Value, m => WebUtility.HtmlDecode(m.Groups["valor"].Value));

        await EnviarAsync(cliente, WebUtility.HtmlDecode(formulario.Groups["accion"].Value), campos);
    }

    private static Task<List<(string ActorTipo, string? ActorId)>> BitacoraAsync(Escenario escenario, string accion) =>
        escenario.FilasAsync(
            $"select actor_tipo, actor_id from agenda.bitacora where accion = '{accion}' order by id",
            l => (l.GetString(0), l.IsDBNull(1) ? null : l.GetString(1)));

    private static async Task<List<FilaLeidaDeOutbox>> EventosAsync(Escenario escenario, Guid citaId, string tipo) =>
        [.. (await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, citaId, Cancelacion))
            .Where(f => f.Tipo == tipo)];

    // El actor va por su nombre en el contenido del evento.
    private static string? ActorDelEvento(FilaLeidaDeOutbox fila)
    {
        using var contenido = JsonDocument.Parse(fila.Contenido);
        return contenido.RootElement.GetProperty(nameof(CitaAgendada.Actor)).GetString();
    }

    private static async Task<string> LeerAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {ruta}: {respuesta.StatusCode}");
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    private static async Task<string> EnviarAsync(HttpClient cliente, string accion, Dictionary<string, string> campos)
    {
        using var respuesta = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"POST {accion}: {respuesta.StatusCode}");
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    [GeneratedRegex("""(?<apertura><form[^>]*>).*?</form>""", RegexOptions.Singleline)]
    private static partial Regex FormularioDeTusDatos();

    [GeneratedRegex(@"\baction=""(?<accion>[^""]*)""")]
    private static partial Regex Accion();

    [GeneratedRegex("""<form method="post" action="(?<accion>[^"]*)"[^>]*>.*?</form>""", RegexOptions.Singleline)]
    private static partial Regex FormularioDeConfirmar();

    [GeneratedRegex("""<input(?=[^>]*type="hidden")(?=[^>]*name="(?<nombre>[^"]+)")(?=[^>]*value="(?<valor>[^"]*)")[^>]*>""")]
    private static partial Regex Ocultos();
}
