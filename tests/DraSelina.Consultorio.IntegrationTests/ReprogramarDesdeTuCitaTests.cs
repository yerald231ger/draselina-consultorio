using System.Net;
using System.Text.RegularExpressions;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.UI.Componentes;
using DraSelina.Consultorio.UI.Pages.Gestion;
using PaginaReprogramar = DraSelina.Consultorio.UI.Pages.Gestion.Reprogramar;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Reprogramar desde Tu cita (CAP-7, FR-9, UJ-2 pasos 1 a 3; entrada 3.3), por HTTP contra la web real. Tu cita ofrece
/// "Reprogramar" fuera del umbral (RN-17) y el panel ámbar dentro; <c>/c/&lt;token&gt;/reprogramar</c> elige día y hora
/// con enlaces GET y confirma con un POST con antiforgery, que pinta Cita reprogramada o vuelve con un aviso. El reloj
/// de la web está en el lunes 2 de noviembre a las 18:00 UTC y la Cita, el martes 3 a las 16:00 local (30 min).
/// </summary>
public sealed partial class ReprogramarDesdeTuCitaTests(Arnes arnes)
{
    private const string Robots = """<meta name="robots" content="noindex, nofollow">""";

    private static readonly DateOnly Martes = new(2026, 11, 3);

    private static readonly DateOnly Jueves = new(2026, 11, 5);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Fuera_del_umbral_Tu_cita_ofrece_Reprogramar_con_el_plazo_y_sin_panel_ambar()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, TuCita(cita));

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Matches($"""<a href="{Regex.Escape(Reprogramar(cita))}"[^>]*>[\s\S]*?Reprogramar\s*</a>""", html);
        Assert.Contains("Puedes reprogramar hasta 2 horas antes de tu cita.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Faltan menos de", html, StringComparison.Ordinal);
        Assert.DoesNotContain("¿Buscabas otro horario?", html, StringComparison.Ordinal);
        SinIdNiScripts(html, cita);
    }

    [Fact]
    public async Task Dentro_del_umbral_Tu_cita_no_ofrece_Reprogramar_y_muestra_el_panel_ambar()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        // A 119 minutos del inicio: ya dentro del umbral de 120.
        escenario.Reloj.Avanzar(HoraLocal.AUtc(Martes, new TimeOnly(16, 0)) - ReservaEnLineaTests.Ahora - TimeSpan.FromMinutes(119));
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, TuCita(cita));

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.DoesNotContain("/reprogramar", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Puedes reprogramar", html, StringComparison.Ordinal);
        PanelAmbar(html);
        Assert.Contains("Agregar a mi calendario", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_justo_el_umbral_Tu_cita_todavia_ofrece_Reprogramar()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(HoraLocal.AUtc(Martes, new TimeOnly(16, 0)) - ReservaEnLineaTests.Ahora - TimeSpan.FromMinutes(120));
        using var cliente = escenario.Web.CreateClient();

        var (_, html) = await AbrirAsync(cliente, TuCita(cita));

        Assert.Contains($"href=\"{Reprogramar(cita)}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Faltan menos de", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_Cita_cancelada_no_ofrece_Reprogramar_en_Tu_cita()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        await CancelarAsync(escenario, cita);
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, TuCita(cita));

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.DoesNotContain("Reprogramar", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Faltan menos de", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Abrir_reprogramar_muestra_la_Cita_actual_los_dias_y_su_hora_como_Tu_hora_actual_sin_enlace()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();

        using var respuesta = await cliente.GetAsync(Reprogramar(cita), Cancelacion);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        SinIndiceNiCache(respuesta);
        Assert.Contains(Robots, html, StringComparison.Ordinal);
        Assert.Contains("<title>Reprogramar tu cita · Dra. Selina Medina Castillo</title>", html, StringComparison.Ordinal);
        Assert.Contains($">{PaginaReprogramar.Titulo}</h1>", html, StringComparison.Ordinal);
        Assert.Matches("""<div class="dsm-cita-actual__fecha">Martes 3 de noviembre, 4:00 pm</div>""", html);
        Assert.Matches("""<div class="dsm-cita-actual__tipo">Primera consulta · 30 min</div>""", html);
        Assert.Matches("""<dt>Tu cita actual</dt>\s*<dd>Martes 3 de noviembre, 4:00 pm</dd>""", html);
        Assert.Matches("""<dt>Nuevo horario</dt>\s*<dd>Elige una hora</dd>""", html);
        Assert.Matches($"""<span class="btn btn-primary dsm-accion-principal disabled" aria-disabled="true">{PaginaReprogramar.Confirmar}</span>""", html);

        // Sin paso de tipo: ni la lista de tipos ni la barra de pasos.
        Assert.DoesNotContain("dsm-tipos", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Paso 2 de 3", html, StringComparison.Ordinal);

        // Sin fecha en la dirección, el día de la Cita; su hora se marca y no se enlaza ni cuenta como libre.
        Assert.Matches("""<span class="dsm-hora dsm-hora--actual"[^>]*>\s*<span[^>]*>4:00 pm</span>\s*<span class="dsm-hora__actual"[^>]*>Tu hora actual</span>""", html);
        Assert.Contains(SelectorDeDiaYHora.TuHoraActual, html, StringComparison.Ordinal);
        var horas = Horas().Matches(html).Select(m => m.Groups["href"].Value).ToList();
        Assert.DoesNotContain($"{Reprogramar(cita)}?fecha=2026-11-03&hora=16:00", horas);
        Assert.Equal(
            [.. new[] { "15:30", "16:30", "17:00", "17:30" }.Select(h => $"{Reprogramar(cita)}?fecha=2026-11-03&hora={h}")],
            horas);
        Assert.Contains("aria-label=\"Martes 3 de noviembre, 4 horarios libres\"", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{Reprogramar(cita)}?fecha=2026-11-05\"", html, StringComparison.Ordinal);

        // Los enlaces de día y hora llevan el token solo en la ruta.
        Assert.All(Vinculos().Matches(html).Select(m => m.Groups["href"].Value).Where(h => h.Contains('?', StringComparison.Ordinal)),
            href => Assert.DoesNotContain(Token(cita), href[href.IndexOf('?', StringComparison.Ordinal)..], StringComparison.Ordinal));
        SinIdNiScripts(html, cita);
    }

    [Fact]
    public async Task Un_dia_cuya_unica_hora_es_la_actual_se_ve_Lleno_y_no_se_enlaza()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        // Otros Pacientes toman las demás horas del martes; cada uno con su teléfono, por el tope semanal.
        foreach (var (hora, i) in new[] { new TimeOnly(15, 30), new(16, 30), new(17, 0), new(17, 30) }.Select((h, i) => (h, i)))
        {
            var otra = ReservaEnLineaTests.Comando(Martes, hora);
            otra.Telefono = $"444 555 {1000 + i}";
            Assert.True((await escenario.AgendarAsync(otra)).EsExito);
        }

        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, Reprogramar(cita));

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Matches("""<span id="dia-2026-11-03" class="dsm-dia dsm-dia--lleno">""", html);
        Assert.DoesNotContain($"{Reprogramar(cita)}?fecha=2026-11-03", html, StringComparison.Ordinal);
        Assert.DoesNotContain("0 libres", html, StringComparison.Ordinal);
        Assert.Contains("Este día ya no tiene horarios libres.", html, StringComparison.Ordinal);

        // El teléfono muestra el primer día con horas que se pueden elegir: el miércoles 4.
        Assert.All(Horas().Matches(html), m => Assert.Contains("fecha=2026-11-04", m.Groups["href"].Value, StringComparison.Ordinal));
        Assert.Equal(5, Horas().Matches(html).Count);
    }

    [Fact]
    public async Task Una_Cita_fuera_de_la_ventana_abre_en_el_primer_dia_con_horas()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        // Hoy lunes a las 15:00 local: fuera de la ventana (desde mañana), y a 3 horas, fuera del umbral.
        var inicio = HoraLocal.AUtc(new DateOnly(2026, 11, 2), new TimeOnly(15, 0));
        await escenario.EjecutarAsync(
            $"update agenda.cita set inicio = '{inicio:O}', fin = '{inicio.AddMinutes(30):O}' where id = '{cita.CitaId.Valor}'");
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, Reprogramar(cita));

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.DoesNotContain("Este día no tiene horarios para este tipo de consulta", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dsm-hora--actual", html, StringComparison.Ordinal);
        var horas = Horas().Matches(html).Select(m => m.Groups["href"].Value).ToList();
        Assert.Equal(5, horas.Count);
        Assert.All(horas, h => Assert.Contains("fecha=2026-11-03", h, StringComparison.Ordinal));
    }

    [Fact]
    public void El_plazo_de_Tu_cita_se_lee_bien_con_umbral_y_sin_el()
    {
        Assert.Equal("Puedes reprogramar hasta 2 horas antes de tu cita.", UI.Pages.Gestion.TuCita.Plazo(120));
        Assert.Equal("Puedes reprogramar hasta la hora de tu cita.", UI.Pages.Gestion.TuCita.Plazo(0));
    }

    [Fact]
    public async Task Otro_dia_de_la_ventana_ofrece_todas_sus_horas_sin_Tu_hora_actual()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();

        var (_, html) = await AbrirAsync(cliente, $"{Reprogramar(cita)}?fecha=2026-11-05");

        Assert.Equal(5, Horas().Matches(html).Count);
        Assert.DoesNotContain("dsm-hora--actual", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Elegir_una_hora_muestra_el_nuevo_horario_y_el_formulario_para_confirmar()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, $"{Reprogramar(cita)}?fecha=2026-11-05&hora=17:00");

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Matches("""<dt>Nuevo horario</dt>\s*<dd>Jueves 5 de noviembre, 5:00 pm</dd>""", html);
        Assert.Contains($"<a href=\"{Reprogramar(cita)}?fecha=2026-11-05&hora=17:00\" class=\"dsm-hora\" aria-current=\"true\">", html, StringComparison.Ordinal);
        var (accion, campos) = Formulario(html);
        Assert.Equal(Reprogramar(cita), accion);
        Assert.Equal("2026-11-05", campos["Comando.Fecha"]);
        Assert.Equal("17:00", campos["Comando.Hora"]);
        Assert.Contains("__RequestVerificationToken", campos.Keys);
        Assert.DoesNotContain(campos.Values, v => v.Contains(Token(cita), StringComparison.Ordinal));
        Assert.Matches($"""<button type="submit" class="btn btn-primary dsm-accion-principal">{PaginaReprogramar.Confirmar}</button>""", html);
    }

    [Fact]
    public async Task La_hora_actual_en_la_direccion_no_se_elige()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();

        var (_, html) = await AbrirAsync(cliente, $"{Reprogramar(cita)}?fecha=2026-11-03&hora=16:00");

        Assert.Matches("""<dt>Nuevo horario</dt>\s*<dd>Elige una hora</dd>""", html);
        Assert.DoesNotContain("dsm-reprogramar__envio", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirmar_pinta_Cita_reprogramada_y_Tu_cita_muestra_la_nueva_fecha_con_su_bitacora()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, eleccion) = await AbrirAsync(cliente, $"{Reprogramar(cita)}?fecha=2026-11-05&hora=17:00");
        var (accion, campos) = Formulario(eleccion);

        using var respuesta = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        SinIndiceNiCache(respuesta);
        Assert.Contains(Robots, html, StringComparison.Ordinal);
        Assert.Contains($">{UI.Pages.Gestion.CitaReprogramada.Titulo}</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"dsm-confirmada__dia\">Jueves</div>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"dsm-confirmada__fecha\">5 de noviembre, 5:00 pm</div>", html, StringComparison.Ordinal);
        Assert.Contains("Primera consulta · 30 min", html, StringComparison.Ordinal);
        Assert.Contains("Hospital Star Médica, consultorio 101", html, StringComparison.Ordinal);
        Assert.Matches($"""<a href="{Regex.Escape(TuCita(cita))}/calendario.ics"[^>]*>[\s\S]*?Agregar a mi calendario\s*</a>""", html);
        Assert.Matches($"""<a href="{Regex.Escape(TuCita(cita))}"[^>]*>Ver mi cita</a>""", html);
        SinIdNiScripts(html, cita);

        var (_, tuCita) = await AbrirAsync(cliente, TuCita(cita));
        Assert.Contains("Jueves 5 de noviembre, 5:00 pm", tuCita, StringComparison.Ordinal);
        Assert.DoesNotContain("Martes 3 de noviembre", tuCita, StringComparison.Ordinal);

        var inicio = Assert.Single(await escenario.FilasAsync("select inicio from agenda.cita", l => l.GetFieldValue<DateTimeOffset>(0)));
        Assert.Equal(HoraLocal.AUtc(Jueves, new TimeOnly(17, 0)), inicio);
        var entrada = Assert.Single(await escenario.FilasAsync(
            "select actor_tipo, entidad_id from agenda.bitacora where accion = 'Reprogramada'",
            l => (Actor: l.GetString(0), Entidad: l.GetString(1))));
        Assert.Equal("Paciente", entrada.Actor);
        Assert.Equal(cita.CitaId.Valor.ToString(), entrada.Entidad);
    }

    [Fact]
    public async Task Un_horario_que_otra_Cita_tomo_vuelve_con_el_aviso_y_los_horarios_restantes_sin_cambios()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, eleccion) = await AbrirAsync(cliente, $"{Reprogramar(cita)}?fecha=2026-11-05&hora=17:00");
        var (accion, campos) = Formulario(eleccion);

        // Otro Paciente toma el jueves a las 17:00 antes de confirmar.
        var otra = ReservaEnLineaTests.Comando(Jueves, new TimeOnly(17, 0));
        otra.Telefono = "444 555 1000";
        Assert.True((await escenario.AgendarAsync(otra)).EsExito);

        var html = await EnviarAsync(cliente, accion, campos);

        Assert.Contains(PaginaReprogramar.AvisoOcupado, html, StringComparison.Ordinal);
        Assert.Contains($">{PaginaReprogramar.Titulo}</h1>", html, StringComparison.Ordinal);
        var horas = Horas().Matches(html).Select(m => m.Groups["href"].Value).ToList();
        Assert.Equal(
            [.. new[] { "15:30", "16:00", "16:30", "17:30" }.Select(h => $"{Reprogramar(cita)}?fecha=2026-11-05&hora={h}")],
            horas);
        Assert.Matches("""<dt>Nuevo horario</dt>\s*<dd>Elige una hora</dd>""", html);
        await SinCambiosAsync(escenario);
    }

    [Theory]
    [InlineData("2026-11-05", "16:15")]
    [InlineData("2026-11-07", "16:00")]
    [InlineData("2027-02-01", "16:00")]
    public async Task Un_horario_fuera_de_la_agenda_o_de_la_ventana_vuelve_con_el_aviso_de_no_disponible(string fecha, string hora)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, eleccion) = await AbrirAsync(cliente, $"{Reprogramar(cita)}?fecha=2026-11-05&hora=17:00");
        var (accion, campos) = Formulario(eleccion);
        campos["Comando.Fecha"] = fecha;
        campos["Comando.Hora"] = hora;

        var html = await EnviarAsync(cliente, accion, campos);

        Assert.Contains(PaginaReprogramar.AvisoNoDisponible, html, StringComparison.Ordinal);
        Assert.Contains("dsm-horas__rejilla", html, StringComparison.Ordinal);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Confirmar_ya_dentro_del_umbral_muestra_el_panel_ambar_con_Ver_mi_cita_sin_cambios()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, eleccion) = await AbrirAsync(cliente, $"{Reprogramar(cita)}?fecha=2026-11-05&hora=17:00");
        var (accion, campos) = Formulario(eleccion);
        escenario.Reloj.Avanzar(HoraLocal.AUtc(Martes, new TimeOnly(16, 0)) - ReservaEnLineaTests.Ahora - TimeSpan.FromMinutes(90));

        var html = await EnviarAsync(cliente, accion, campos);

        PanelAmbar(html);
        Assert.Matches($"""<a href="{Regex.Escape(TuCita(cita))}"[^>]*>Ver mi cita</a>""", html);
        Assert.DoesNotContain("dsm-horas__rejilla", html, StringComparison.Ordinal);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Abrir_reprogramar_dentro_del_umbral_muestra_el_panel_ambar_con_Ver_mi_cita()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(HoraLocal.AUtc(Martes, new TimeOnly(16, 0)) - ReservaEnLineaTests.Ahora - TimeSpan.FromMinutes(30));
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, Reprogramar(cita));

        Assert.Equal(HttpStatusCode.OK, estado);
        PanelAmbar(html);
        Assert.Matches($"""<a href="{Regex.Escape(TuCita(cita))}"[^>]*>Ver mi cita</a>""", html);
        Assert.DoesNotContain("dsm-horas__rejilla", html, StringComparison.Ordinal);
        Assert.Contains(Robots, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_Cita_cancelada_al_abrir_o_al_confirmar_dice_que_ya_esta_cancelada_con_Ver_mi_cita()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, eleccion) = await AbrirAsync(cliente, $"{Reprogramar(cita)}?fecha=2026-11-05&hora=17:00");
        var (accion, campos) = Formulario(eleccion);
        await CancelarAsync(escenario, cita);

        var (estado, alAbrir) = await AbrirAsync(cliente, Reprogramar(cita));
        var alConfirmar = await EnviarAsync(cliente, accion, campos);

        Assert.Equal(HttpStatusCode.OK, estado);
        foreach (var html in new[] { alAbrir, alConfirmar })
        {
            Assert.Contains($">{PaginaReprogramar.Cancelada}</h1>", html, StringComparison.Ordinal);
            Assert.Matches($"""<a href="{Regex.Escape(TuCita(cita))}"[^>]*>Ver mi cita</a>""", html);
            Assert.DoesNotContain("dsm-horas__rejilla", html, StringComparison.Ordinal);
            Assert.Contains(Robots, html, StringComparison.Ordinal);
        }

        Assert.Equal(0, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora where accion = 'Reprogramada'"));
    }

    [Fact]
    public async Task Un_enlace_no_valido_al_abrir_o_al_confirmar_es_404_sin_indice_ni_cache()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, eleccion) = await AbrirAsync(cliente, $"{Reprogramar(cita)}?fecha=2026-11-05&hora=17:00");
        var (_, campos) = Formulario(eleccion);
        var noValido = $"/c/{new string('A', 43)}/reprogramar";

        using var alAbrir = await cliente.GetAsync(noValido, Cancelacion);
        using var alConfirmar = await cliente.PostAsync(noValido, new FormUrlEncodedContent(campos), Cancelacion);
        using var conElId = await cliente.GetAsync($"/c/{cita.CitaId.Valor}/reprogramar", Cancelacion);

        foreach (var respuesta in new[] { alAbrir, alConfirmar, conElId })
        {
            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
            SinIndiceNiCache(respuesta);
            var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
            Assert.Contains(EnlaceNoValido.Titulo, html, StringComparison.Ordinal);
            Assert.Contains(Robots, html, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
        }

        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Un_envio_sin_antiforgery_se_rechaza_sin_cambios()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, eleccion) = await AbrirAsync(cliente, $"{Reprogramar(cita)}?fecha=2026-11-05&hora=17:00");
        var (accion, campos) = Formulario(eleccion);
        campos.Remove("__RequestVerificationToken");

        using var respuesta = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);

        Assert.NotEqual(HttpStatusCode.OK, respuesta.StatusCode);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Una_Cita_de_un_tipo_que_no_se_agenda_en_linea_no_se_reprograma_en_linea()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        await escenario.EjecutarAsync(
            $"update agenda.tipo_de_visita set reservable_en_linea = false where id = '{TiposDeVisitaSembrados.PrimeraConsulta}'");
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, Reprogramar(cita));

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Contains(PaginaReprogramar.NoEnLinea, html, StringComparison.Ordinal);
        Assert.Contains("Escríbenos por WhatsApp", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dsm-horas__rejilla", html, StringComparison.Ordinal);
    }

    private static async Task<CitaAgendadaEnLinea> AgendarAsync(ReservaEnLineaTests.Escenario escenario)
    {
        var resultado = await escenario.AgendarAsync(ReservaEnLineaTests.Comando(Martes, new TimeOnly(16, 0)));
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static Task CancelarAsync(ReservaEnLineaTests.Escenario escenario, CitaAgendadaEnLinea cita) =>
        escenario.EjecutarAsync(
            $"update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now() where id = '{cita.CitaId.Valor}'");

    // La Cita sigue el martes a las 16:00 y no hay bitácora de reprogramación.
    private static async Task SinCambiosAsync(ReservaEnLineaTests.Escenario escenario)
    {
        var inicio = await escenario.FilasAsync(
            "select inicio from agenda.cita where origen = 'en_linea' order by creada_en limit 1",
            l => l.GetFieldValue<DateTimeOffset>(0));
        Assert.Equal(HoraLocal.AUtc(Martes, new TimeOnly(16, 0)), Assert.Single(inicio));
        Assert.Equal(0, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora where accion = 'Reprogramada'"));
    }

    private static void PanelAmbar(string html)
    {
        Assert.Contains("Faltan menos de 2 horas", html, StringComparison.Ordinal);
        Assert.Contains("¿Buscabas otro horario?", html, StringComparison.Ordinal);
        Assert.Contains(PanelDentroDelUmbral.OtroHorario, html, StringComparison.Ordinal);
    }

    private static void SinIdNiScripts(string html, CitaAgendadaEnLinea cita)
    {
        Assert.DoesNotContain(cita.CitaId.Valor.ToString(), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(cita.CitaId.Valor.ToString("N"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    private static void SinIndiceNiCache(HttpResponseMessage respuesta)
    {
        Assert.Equal("noindex, nofollow", Assert.Single(respuesta.Headers.GetValues("X-Robots-Tag")));
        Assert.True(respuesta.Headers.CacheControl?.NoStore, "Lleva el Enlace de gestión: no se guarda en caché.");
    }

    private static async Task<(HttpStatusCode Estado, string Html)> AbrirAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
        return (respuesta.StatusCode, WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion)));
    }

    private static async Task<string> EnviarAsync(HttpClient cliente, string accion, Dictionary<string, string> campos)
    {
        using var respuesta = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    // El formulario de "Confirmar nuevo horario": su destino y sus campos ocultos, con el token antifalsificación.
    private static (string Accion, Dictionary<string, string> Campos) Formulario(string html)
    {
        var formulario = FormularioDeEnvio().Match(html);
        Assert.True(formulario.Success, "La página no tiene el formulario de confirmar.");
        var campos = Ocultos().Matches(formulario.Value)
            .ToDictionary(m => m.Groups["nombre"].Value, m => m.Groups["valor"].Value);
        return (formulario.Groups["accion"].Value, campos);
    }

    private static string TuCita(CitaAgendadaEnLinea cita) => new Uri(cita.EnlaceDeGestion).AbsolutePath;

    private static string Reprogramar(CitaAgendadaEnLinea cita) => $"{TuCita(cita)}/reprogramar";

    private static string Token(CitaAgendadaEnLinea cita) =>
        cita.EnlaceDeGestion[(cita.EnlaceDeGestion.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];

    // Las horas que se pueden elegir: enlaces de la rejilla.
    [GeneratedRegex("""<a href="(?<href>[^"]*)" class="dsm-hora"[^>]*>""")]
    private static partial Regex Horas();

    [GeneratedRegex("""<a[^>]*href="(?<href>[^"]*)"[^>]*>""")]
    private static partial Regex Vinculos();

    [GeneratedRegex("""<form method="post" action="(?<accion>[^"]*)"[^>]*>.*?</form>""", RegexOptions.Singleline)]
    private static partial Regex FormularioDeEnvio();

    [GeneratedRegex("""<input(?=[^>]*type="hidden")(?=[^>]*name="(?<nombre>[^"]+)")(?=[^>]*value="(?<valor>[^"]*)")[^>]*>""")]
    private static partial Regex Ocultos();
}
