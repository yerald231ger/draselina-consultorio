using System.Net;
using System.Text.RegularExpressions;
using DraSelina.Consultorio.Agenda.Infrastructure;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La página Tus datos en SSR estático, por HTTP contra la web real: el GET con <c>tipo</c>, <c>fecha</c> y <c>hora</c>
/// en la cadena de consulta, y el POST del formulario, que devuelve Cita confirmada en la misma respuesta (AD-11).
/// </summary>
public sealed partial class TusDatosTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private static string Ruta(string hora = "16:00") =>
        $"/reservar/datos?tipo={TiposDeVisitaSembrados.PrimeraConsulta}&fecha=2026-11-03&hora={hora}";

    [Fact]
    public async Task El_GET_pinta_el_formulario_con_el_horario_elegido_y_sin_ningun_script()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        using var respuesta = await cliente.GetAsync(Ruta(), Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));

        Assert.Contains("Tus datos", html, StringComparison.Ordinal);
        Assert.Contains("Mar 3 nov · 4:00 pm", html, StringComparison.Ordinal);
        Assert.Matches(Control("Comando.Nombre"), html);
        Assert.Matches(Control("Comando.Telefono"), html);
        Assert.Matches(Control("Comando.Correo"), html);
        Assert.Matches(Control("Comando.Motivo"), html);
        Assert.Matches(Control("Comando.Consentimiento"), html);
        Assert.Contains("Confirmar cita", html, StringComparison.Ordinal);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blazor.web.js", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_POST_guarda_la_Cita_y_responde_Cita_confirmada_con_fecha_hora_y_direccion()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var (accion, html) = await EnviarAsync(cliente, Ruta(), Datos());

        Assert.Contains("Tu cita está confirmada", html, StringComparison.Ordinal);
        Assert.Contains("Martes", html, StringComparison.Ordinal);
        Assert.Contains("3 de noviembre, 4:00 pm", html, StringComparison.Ordinal);
        Assert.Contains("Primera consulta · 30 min", html, StringComparison.Ordinal);
        Assert.Contains("Hospital Star Médica, consultorio 101", html, StringComparison.Ordinal);
        Assert.Contains("Mariano Arista No. 735, Col. Barrio de Tequisquiapan, San Luis Potosí", html, StringComparison.Ordinal);
        Assert.Contains("Volver al inicio", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", html, StringComparison.Ordinal);

        Assert.Single(await escenario.CitasAsync());

        // En la dirección solo viajan tipo, fecha y hora; nunca los datos del Paciente (AD-11, AD-23).
        Assert.Equal(Ruta(), accion);
        Assert.Equal(["fecha", "hora", "tipo"], ParametrosDe(accion));
    }

    [Fact]
    public async Task Un_celular_imposible_vuelve_al_formulario_con_el_mensaje_bajo_el_campo_y_sin_guardar()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();
        var datos = Datos();
        datos["Comando.Telefono"] = "12345";

        var (_, html) = await EnviarAsync(cliente, Ruta(), datos);

        Assert.Contains("Revisa tu número: deben ser 10 dígitos.", html, StringComparison.Ordinal);
        Assert.Matches(ControlInvalido("Comando.Telefono"), html);
        Assert.DoesNotContain("Tu cita está confirmada", html, StringComparison.Ordinal);

        // Los datos escritos se conservan en el formulario.
        Assert.Contains("value=\"Ana Sofía López\"", html, StringComparison.Ordinal);
        Assert.Empty(await escenario.CitasAsync());
        Assert.Empty(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task Sin_Consentimiento_el_aviso_aparece_junto_a_la_casilla()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();
        var datos = Datos();
        datos.Remove("Comando.Consentimiento");

        var (_, html) = await EnviarAsync(cliente, Ruta(), datos);

        Assert.Contains("Para agendar tu cita necesitas aceptar el Aviso de privacidad.", html, StringComparison.Ordinal);
        Assert.Matches(ControlInvalido("Comando.Consentimiento"), html);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Un_horario_ocupado_muestra_el_aviso_sin_error_tecnico_y_conserva_los_datos()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).EsExito);
        using var cliente = escenario.Web.CreateClient();
        var datos = Datos();
        datos["Comando.Nombre"] = "Mariana López";

        var (_, html) = await EnviarAsync(cliente, Ruta(), datos);

        Assert.Contains("Ese horario se acaba de ocupar.", html, StringComparison.Ordinal);
        Assert.Contains("value=\"Mariana López\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("agenda.horario_ocupado", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
        Assert.Single(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Con_el_tope_semanal_alcanzado_avisa_ofrece_WhatsApp_y_conserva_los_datos()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await escenario.AgendarTresAsync();
        using var cliente = escenario.Web.CreateClient();
        var datos = Datos();
        datos["Comando.Nombre"] = "Mariana López";

        var (_, html) = await EnviarAsync(cliente, Ruta("17:00"), datos);

        // El WhatsApp todavía no se conoce: el mensaje lo nombra sin número (Gerardo, 2026-11-02).
        Assert.Contains(
            "Este número ya tiene el máximo de citas de esta semana. Escríbenos por WhatsApp y te ayudamos.",
            html,
            StringComparison.Ordinal);
        Assert.Contains("value=\"Mariana López\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"444 123 4567\"", html, StringComparison.Ordinal);
        Assert.Contains("value=\"ana@correo.com\"", html, StringComparison.Ordinal);
        Assert.Contains("Dolor de rodilla derecha</textarea>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Tu cita está confirmada", html, StringComparison.Ordinal);
        Assert.DoesNotContain("agenda.tope_semanal", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
        Assert.Equal(3, (await escenario.CitasAsync()).Count);
    }

    [Theory]
    [InlineData("2026-11-03", "15:45")]
    [InlineData("2026-11-02", "16:00")]
    public async Task Un_horario_fuera_de_la_rejilla_o_de_la_ventana_muestra_el_aviso_sin_error_tecnico(string fecha, string hora)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();
        var ruta = $"/reservar/datos?tipo={TiposDeVisitaSembrados.PrimeraConsulta}&fecha={fecha}&hora={hora}";

        var (_, html) = await EnviarAsync(cliente, ruta, Datos());

        Assert.Contains("Ese horario ya no está disponible. Vuelve a elegir otro horario.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Tu cita está confirmada", html, StringComparison.Ordinal);
        Assert.DoesNotContain("agenda.fuera_de", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Theory]
    [InlineData("/reservar/datos")]
    [InlineData("/reservar/datos?tipo=no-es-un-id&fecha=2026-11-03&hora=16:00")]
    [InlineData("/reservar/datos?tipo=01a19e61-08c5-7c92-a7e1-3b97e6858869&fecha=3-11-2026&hora=16:00")]
    [InlineData("/reservar/datos?tipo=01a19e61-08c5-7c92-a7e1-3b97e6858869&fecha=2026-11-03&hora=4pm")]
    public async Task Sin_un_horario_valido_en_la_direccion_no_pinta_el_formulario(string ruta)
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();

        var html = WebUtility.HtmlDecode(await cliente.GetStringAsync(ruta, Cancelacion));

        Assert.Contains("No encontramos el horario que elegiste.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2021-04-04", "02:30")]
    [InlineData("9999-12-31", "23:59")]
    public async Task Una_fecha_fuera_de_hoy_a_un_anio_no_pinta_el_formulario_ni_guarda_nada(string fecha, string hora)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();
        var ruta = $"/reservar/datos?tipo={TiposDeVisitaSembrados.PrimeraConsulta}&fecha={fecha}&hora={hora}";

        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));

        Assert.Contains("No encontramos el horario que elegiste.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", html, StringComparison.Ordinal);

        // Un envío directo a esa dirección tampoco llega al handler.
        using var envio = await cliente.PostAsync(ruta, new FormUrlEncodedContent(Datos()), Cancelacion);
        Assert.NotEqual(HttpStatusCode.InternalServerError, envio.StatusCode);
        Assert.Empty(await escenario.PacientesAsync());
        Assert.Empty(await escenario.CitasAsync());
    }

    private static Dictionary<string, string> Datos() => new()
    {
        ["Comando.Nombre"] = "Ana Sofía López",
        ["Comando.Telefono"] = "444 123 4567",
        ["Comando.Correo"] = "ana@correo.com",
        ["Comando.Motivo"] = "Dolor de rodilla derecha",
        ["Comando.Consentimiento"] = "true",
    };

    /// <summary>
    /// Pide la página, toma su formulario (destino y campos ocultos, con el token antifalsificación) y lo envía con
    /// los datos, como lo haría el navegador. Devuelve el destino del formulario y el HTML de la respuesta.
    /// </summary>
    private static async Task<(string Accion, string Html)> EnviarAsync(HttpClient cliente, string ruta, Dictionary<string, string> datos)
    {
        var pagina = await cliente.GetStringAsync(ruta, Cancelacion);
        var formulario = Formulario().Match(pagina);
        Assert.True(formulario.Success, "La página no tiene formulario.");

        // Sin action, el navegador envía a la dirección de la página.
        var destino = Accion().Match(formulario.Groups["apertura"].Value);
        var accion = destino.Success ? WebUtility.HtmlDecode(destino.Groups["accion"].Value) : ruta;
        var campos = Ocultos().Matches(formulario.Value)
            .ToDictionary(m => m.Groups["nombre"].Value, m => WebUtility.HtmlDecode(m.Groups["valor"].Value));
        foreach (var (nombre, valor) in datos)
        {
            campos[nombre] = valor;
        }

        using var respuesta = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (accion, WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion)));
    }

    private static List<string> ParametrosDe(string ruta) =>
        [.. ruta[(ruta.IndexOf('?') + 1)..].Split('&').Select(par => par.Split('=')[0]).Order(StringComparer.Ordinal)];

    private static Regex Control(string nombre) => new($"""<(input|textarea)[^>]*name="{Regex.Escape(nombre)}"[^>]*>""");

    private static Regex ControlInvalido(string nombre) =>
        new($"""<(input|textarea)(?=[^>]*name="{Regex.Escape(nombre)}")(?=[^>]*aria-invalid="true")[^>]*>""");

    [GeneratedRegex("""(?<apertura><form[^>]*>).*?</form>""", RegexOptions.Singleline)]
    private static partial Regex Formulario();

    [GeneratedRegex(@"\baction=""(?<accion>[^""]*)""")]
    private static partial Regex Accion();

    [GeneratedRegex("""<input(?=[^>]*type="hidden")(?=[^>]*name="(?<nombre>[^"]+)")(?=[^>]*value="(?<valor>[^"]*)")[^>]*>""")]
    private static partial Regex Ocultos();
}
