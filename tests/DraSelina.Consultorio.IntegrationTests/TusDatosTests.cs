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
        Assert.Equal(7, SolicitudDe(html).Version);

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
    public async Task Cada_vez_que_se_pinta_el_formulario_lleva_una_solicitud_nueva()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var primera = SolicitudDe(WebUtility.HtmlDecode(await cliente.GetStringAsync(Ruta(), Cancelacion)));
        var segunda = SolicitudDe(WebUtility.HtmlDecode(await cliente.GetStringAsync(Ruta(), Cancelacion)));

        Assert.NotEqual(primera, segunda);
    }

    [Fact]
    public async Task Una_solicitud_con_ceros_escrita_a_mano_no_reserva_ni_falla_y_vuelve_con_una_solicitud_nueva()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();
        var datos = Datos();
        datos["Comando.SolicitudId"] = Guid.Empty.ToString();

        var (_, html) = await EnviarAsync(cliente, Ruta(), datos);

        Assert.DoesNotContain("Tu cita está confirmada", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
        Assert.NotEqual(Guid.Empty, SolicitudDe(html));
        Assert.Empty(await escenario.CitasAsync());
        Assert.Empty(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task Confirmar_dos_veces_el_mismo_formulario_da_una_Cita_y_la_misma_confirmacion()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();
        var (accion, campos) = await LlenarAsync(cliente, Ruta(), Datos());

        var primera = await PostAsync(cliente, accion, campos);
        var segunda = await PostAsync(cliente, accion, campos);

        Assert.Contains("Tu cita está confirmada", primera, StringComparison.Ordinal);
        Assert.Equal(Confirmacion(primera), Confirmacion(segunda));
        Assert.DoesNotContain("Ese horario se acaba de ocupar", segunda, StringComparison.Ordinal);
        Assert.Single(await escenario.CitasAsync());
        Assert.Equal(
            Guid.Parse(campos["Comando.SolicitudId"]),
            await escenario.EscalarAsync<Guid>("select solicitud_id from agenda.cita"));
    }

    [Fact]
    public async Task Un_horario_ocupado_muestra_el_aviso_y_los_horarios_libres_restantes_sin_error_tecnico()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).EsExito);

        // El miércoles, lleno por Citas del consultorio: se ve "Lleno", como en Día y hora.
        var miercoles = new DateOnly(2026, 11, 4);
        for (var inicio = new TimeOnly(15, 30); inicio < new TimeOnly(18, 0); inicio = inicio.AddMinutes(30))
        {
            await escenario.InsertarCitaDelConsultorioAsync(miercoles, inicio, inicio.AddMinutes(30));
        }

        using var cliente = escenario.Web.CreateClient();
        var datos = Datos();
        datos["Comando.Nombre"] = "Mariana López";

        var (_, html) = await EnviarAsync(cliente, Ruta(), datos);

        Assert.Contains("Ese horario se acaba de ocupar. Elige otro; tus datos se conservan.", html, StringComparison.Ordinal);
        Assert.Contains("Elige otro horario", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Tu cita está confirmada", html, StringComparison.Ordinal);
        Assert.DoesNotContain("agenda.horario_ocupado", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await escenario.CitasAsync(), c => c.Origen == "en_linea");

        // Las horas libres del tipo; la tomada ya no se ofrece y el día lleno no tiene horas.
        var horas = Horas().Matches(html)
            .Select(m => (Accion: m.Groups["accion"].Value, Texto: m.Groups["texto"].Value))
            .ToList();
        Assert.Contains(horas, h => h.Accion == Ruta("16:30") && h.Texto == "4:30 pm");
        Assert.Contains(horas, h => h.Accion == Ruta("15:30"));
        Assert.DoesNotContain(horas, h => h.Accion == Ruta("16:00"));
        Assert.DoesNotContain(horas, h => h.Accion.Contains("fecha=2026-11-04", StringComparison.Ordinal));
        Assert.Matches("Miércoles 4 de noviembre</span>\\s*<span class=\"dsm-otro-dia__estado\">Lleno</span>", html);

        // Los datos viajan ocultos en el cuerpo; en la dirección de cada hora, solo tipo, fecha y hora (AD-11).
        Assert.All(horas, h => Assert.Equal(["fecha", "hora", "tipo"], ParametrosDe(h.Accion)));
        var ocultos = CamposOcultos(html);
        Assert.Equal("Mariana López", ocultos["Comando.Nombre"]);
        Assert.Equal("444 123 4567", ocultos["Comando.Telefono"]);
        Assert.Equal("ana@correo.com", ocultos["Comando.Correo"]);
        Assert.Equal("Dolor de rodilla derecha", ocultos["Comando.Motivo"]);
        Assert.Equal("true", ocultos["Comando.Consentimiento"]);
    }

    [Fact]
    public async Task Elegir_otra_hora_vuelve_a_Tus_datos_llena_sin_reservar_y_confirmar_reserva_con_la_misma_solicitud()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).EsExito);
        using var cliente = escenario.Web.CreateClient();
        var datos = Datos();
        datos["Comando.Nombre"] = "Mariana López";
        var (accion, campos) = await LlenarAsync(cliente, Ruta(), datos);
        var solicitud = Guid.Parse(campos["Comando.SolicitudId"]);
        var tomado = await PostAsync(cliente, accion, campos);

        // Elegir las 4:30 pm: el botón envía el formulario de la vista a Tus datos con la nueva hora.
        var otraHora = Horas().Matches(tomado).Select(m => m.Groups["accion"].Value).Single(a => a == Ruta("16:30"));
        var datosConHora = await PostAsync(cliente, otraHora, CamposOcultos(tomado));

        Assert.Contains("Tus datos", datosConHora, StringComparison.Ordinal);
        Assert.Contains("Mar 3 nov · 4:30 pm", datosConHora, StringComparison.Ordinal);
        Assert.Contains("value=\"Mariana López\"", datosConHora, StringComparison.Ordinal);
        Assert.Contains("value=\"444 123 4567\"", datosConHora, StringComparison.Ordinal);
        Assert.Contains("value=\"ana@correo.com\"", datosConHora, StringComparison.Ordinal);
        Assert.Contains("Dolor de rodilla derecha</textarea>", datosConHora, StringComparison.Ordinal);
        Assert.Matches(Marcada(), datosConHora);
        Assert.DoesNotContain("Ese horario se acaba de ocupar", datosConHora, StringComparison.Ordinal);
        Assert.DoesNotContain("Tu cita está confirmada", datosConHora, StringComparison.Ordinal);
        Assert.Single(await escenario.CitasAsync());

        // Confirmar sin volver a escribir nada: la Cita de las 16:30, con la misma solicitud.
        var formulario = Formulario().Match(datosConHora).Value;
        Assert.Contains("value=\"datos\"", formulario, StringComparison.Ordinal);
        var confirmada = await PostAsync(cliente, otraHora, CamposDelFormulario(formulario));

        Assert.Contains("Tu cita está confirmada", confirmada, StringComparison.Ordinal);
        Assert.Contains("3 de noviembre, 4:30 pm", confirmada, StringComparison.Ordinal);
        Assert.Equal(2, (await escenario.CitasAsync()).Count);
        Assert.Equal(
            solicitud,
            await escenario.EscalarAsync<Guid>(
                $"select solicitud_id from agenda.cita where inicio = '{new DateTimeOffset(2026, 11, 3, 22, 30, 0, TimeSpan.Zero):O}'"));
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
        var (accion, campos) = await LlenarAsync(cliente, ruta, datos);
        return (accion, await PostAsync(cliente, accion, campos));
    }

    /// <summary>Pide la página y llena su formulario: devuelve su destino y sus campos, los ocultos y los datos.</summary>
    private static async Task<(string Accion, Dictionary<string, string> Campos)> LlenarAsync(
        HttpClient cliente, string ruta, Dictionary<string, string> datos)
    {
        var pagina = await cliente.GetStringAsync(ruta, Cancelacion);
        var formulario = Formulario().Match(pagina);
        Assert.True(formulario.Success, "La página no tiene formulario.");

        // Sin action, el navegador envía a la dirección de la página.
        var destino = Accion().Match(formulario.Groups["apertura"].Value);
        var accion = destino.Success ? WebUtility.HtmlDecode(destino.Groups["accion"].Value) : ruta;
        var campos = CamposOcultos(formulario.Value);
        foreach (var (nombre, valor) in datos)
        {
            campos[nombre] = valor;
        }

        return (accion, campos);
    }

    /// <summary>Envía los campos como el navegador y devuelve el HTML de la respuesta, ya decodificado.</summary>
    private static async Task<string> PostAsync(HttpClient cliente, string accion, Dictionary<string, string> campos)
    {
        using var respuesta = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    // Los campos ocultos, con su valor decodificado. Sirve también sobre HTML ya decodificado: los valores de las
    // pruebas no llevan comillas ni "&".
    private static Dictionary<string, string> CamposOcultos(string html) =>
        Ocultos().Matches(html).ToDictionary(m => m.Groups["nombre"].Value, m => WebUtility.HtmlDecode(m.Groups["valor"].Value));

    // Lo que el navegador enviaría del formulario de Tus datos (ya decodificado) sin tocar nada: los ocultos, los
    // controles de texto con su valor, el motivo y la casilla si está marcada.
    private static Dictionary<string, string> CamposDelFormulario(string formulario)
    {
        var campos = CamposOcultos(formulario);
        foreach (Match control in Textos().Matches(formulario))
        {
            campos[control.Groups["nombre"].Value] = control.Groups["valor"].Value;
        }

        campos["Comando.Motivo"] = Motivo().Match(formulario).Groups["valor"].Value;
        if (Marcada().IsMatch(formulario))
        {
            campos["Comando.Consentimiento"] = "true";
        }

        return campos;
    }

    private static Guid SolicitudDe(string html) => Guid.Parse(CamposOcultos(html)["Comando.SolicitudId"]);

    // La tarjeta de Cita confirmada: dos respuestas se comparan sin el resto de la página.
    private static string Confirmacion(string html)
    {
        var inicio = html.IndexOf("<section class=\"dsm-contenedor dsm-confirmada\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "La respuesta no es Cita confirmada.");
        var fin = html.IndexOf("</section>", inicio, StringComparison.Ordinal) + "</section>".Length;
        return html[inicio..fin];
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

    // Cada hora de Horario tomado: un botón que envía el formulario a Tus datos con la hora elegida.
    [GeneratedRegex("""<button[^>]*formaction="(?<accion>[^"]*)"[^>]*>\s*(?<texto>[^<]*?)\s*</button>""")]
    private static partial Regex Horas();

    [GeneratedRegex("""<input(?=[^>]*type="(?:text|tel|email)")(?=[^>]*name="(?<nombre>[^"]+)")(?=[^>]*value="(?<valor>[^"]*)")[^>]*>""")]
    private static partial Regex Textos();

    [GeneratedRegex("""<textarea[^>]*name="Comando.Motivo"[^>]*>(?<valor>[^<]*)</textarea>""")]
    private static partial Regex Motivo();

    [GeneratedRegex("""<input(?=[^>]*name="Comando.Consentimiento")(?=[^>]*checked)[^>]*>""")]
    private static partial Regex Marcada();

    [GeneratedRegex("""<input(?=[^>]*type="hidden")(?=[^>]*name="(?<nombre>[^"]+)")(?=[^>]*value="(?<valor>[^"]*)")[^>]*>""")]
    private static partial Regex Ocultos();
}
