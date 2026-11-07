using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.UI.Pages.Gestion;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Tu cita (CAP-6, FR-8; entrada 3.1): <c>GET /c/&lt;token&gt;</c> por HTTP contra la web real. Se llega con el token
/// del Enlace de gestión, nunca con el id (AD-19). Una Cita futura, Agendada o Cancelada, se muestra; todo lo demás es
/// Enlace no válido con 404, sin decir por qué (RN-22). Toda respuesta bajo <c>/c/</c> va sin índice y sin caché
/// (CAP-21) y con el límite de frecuencia de la reserva (AD-22). Ni el token ni el nombre van a trazas o registros
/// (AD-23).
/// </summary>
public sealed class TuCitaTests(Arnes arnes)
{
    private const string Robots = """<meta name="robots" content="noindex, nofollow">""";

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Una_Cita_Agendada_futura_muestra_Tu_cita_con_su_fecha_tipo_Paciente_Lugar_calendario_y_contacto()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var comando = ReservaEnLineaTests.Comando();
        var cita = (await escenario.AgendarAsync(comando)).Valor;
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, Ruta(cita.EnlaceDeGestion));

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Contains("<title>Tu cita · Dra. Selina Medina Castillo</title>", html, StringComparison.Ordinal);
        Assert.Contains(">Tu cita</h1>", html, StringComparison.Ordinal);
        Assert.Matches("""<span class="dsm-chip-de-estado">\s*<svg[\s\S]*?</svg>\s*Agendada\s*</span>""", html);
        Assert.Contains("Martes 3 de noviembre, 4:00 pm", html, StringComparison.Ordinal);
        Assert.Contains("Primera consulta · 30 min", html, StringComparison.Ordinal);
        Assert.Matches("""<dt>Paciente</dt>\s*<dd>Ana Sofía López</dd>""", html);
        Assert.Matches("""<dt>Lugar</dt>\s*<dd>Hospital Star Médica, consultorio 101</dd>""", html);
        Assert.Contains($"href=\"{Ruta(cita.EnlaceDeGestion)}/calendario.ics\"", html, StringComparison.Ordinal);
        Assert.Contains("Agregar a mi calendario", html, StringComparison.Ordinal);
        Assert.Contains("¿Otra duda? Escríbenos por WhatsApp", html, StringComparison.Ordinal);

        // Sin Reprogramar ni Cancelar todavía (3.3, 3.4), sin id, sin motivo ni contacto del Paciente, sin scripts.
        Assert.DoesNotContain("Reprogramar", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelar cita", html, StringComparison.Ordinal);
        Assert.DoesNotContain(cita.CitaId.Valor.ToString(), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(cita.CitaId.Valor.ToString("N"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rodilla", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(comando.Correo!, html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4441234567", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("paciente")]
    [InlineData("tarde")]
    public async Task Una_Cita_cancelada_por_el_Paciente_a_tiempo_o_tarde_se_ve_Cancelada_con_Agendar_otra_cita(string motivo)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        await CancelarAsync(escenario, cita, motivo);
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, Ruta(cita.EnlaceDeGestion));

        Assert.Equal(HttpStatusCode.OK, estado);
        Cancelada(html);
        Assert.Contains(TuCita.CanceladaPorElPaciente, html, StringComparison.Ordinal);
        Assert.DoesNotContain(TuCita.CanceladaPorElConsultorio, html, StringComparison.Ordinal);
        Assert.Matches("""<a href="/reservar"[^>]*>Agendar otra cita</a>""", html);
    }

    [Fact]
    public async Task Una_Cita_cancelada_por_el_Consultorio_se_ve_Cancelada_con_la_disculpa_y_Elegir_otro_horario()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        await CancelarAsync(escenario, cita, "consultorio");
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, Ruta(cita.EnlaceDeGestion));

        Assert.Equal(HttpStatusCode.OK, estado);
        Cancelada(html);
        Assert.Contains(TuCita.CanceladaPorElConsultorio, html, StringComparison.Ordinal);
        Assert.DoesNotContain(TuCita.CanceladaPorElPaciente, html, StringComparison.Ordinal);
        Assert.Matches("""<a href="/reservar"[^>]*>Elegir otro horario</a>""", html);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData(", estado = 'Cancelada', motivo_de_cancelacion = 'consultorio', cancelada_en = now()", 0)]
    [InlineData(", estado = 'NoSePresento'", 0)]
    [InlineData("", -60)]
    [InlineData(", estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now()", -60)]
    public async Task Una_Cita_cuyo_inicio_ya_paso_es_Enlace_no_valido_cualquiera_que_sea_su_estado(string cambio, int minutosDesdeAhora)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;

        // El reloj de la web está en el lunes 2 a las 18:00 UTC: con 0 la Cita empieza justo entonces (el límite) y con
        // -60, una hora antes; en los dos casos ya pasó (RN-31).
        var inicio = ReservaEnLineaTests.Ahora.AddMinutes(minutosDesdeAhora);
        await escenario.EjecutarAsync(
            $"update agenda.cita set inicio = '{inicio:O}', fin = '{inicio.AddMinutes(30):O}'{cambio} where id = '{cita.CitaId.Valor}'");
        using var cliente = escenario.Web.CreateClient();

        await EnlaceNoValidoAsync(cliente, Ruta(cita.EnlaceDeGestion));
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("no-es-un-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB")]
    public async Task Un_token_inexistente_o_mal_formado_es_Enlace_no_valido(string token)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await escenario.AgendarAsync(ReservaEnLineaTests.Comando());
        using var cliente = escenario.Web.CreateClient();

        await EnlaceNoValidoAsync(cliente, $"/c/{token}");
    }

    [Fact]
    public async Task Un_token_alterado_o_el_id_de_la_Cita_son_Enlace_no_valido()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        var token = Token(cita.EnlaceDeGestion);
        var alterado = token[..10] + (token[10] == 'A' ? 'B' : 'A') + token[11..];
        using var cliente = escenario.Web.CreateClient();

        await EnlaceNoValidoAsync(cliente, $"/c/{alterado}");
        await EnlaceNoValidoAsync(cliente, $"/c/{cita.CitaId.Valor}");
        await EnlaceNoValidoAsync(cliente, $"/c/{cita.CitaId.Valor:N}");
    }

    [Fact]
    public async Task El_enlace_de_una_Cita_muestra_su_fecha_y_no_la_de_otra()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var a = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando(new DateOnly(2026, 11, 3), new TimeOnly(16, 0)))).Valor;
        var b = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando(new DateOnly(2026, 11, 5), new TimeOnly(17, 30)))).Valor;
        using var cliente = escenario.Web.CreateClient();

        var (_, deB) = await AbrirAsync(cliente, Ruta(b.EnlaceDeGestion));
        var (_, deA) = await AbrirAsync(cliente, Ruta(a.EnlaceDeGestion));

        Assert.Contains("Jueves 5 de noviembre, 5:30 pm", deB, StringComparison.Ordinal);
        Assert.DoesNotContain("3 de noviembre", deB, StringComparison.Ordinal);
        Assert.DoesNotContain("4:00 pm", deB, StringComparison.Ordinal);
        Assert.Contains("Martes 3 de noviembre, 4:00 pm", deA, StringComparison.Ordinal);
        Assert.DoesNotContain("5 de noviembre", deA, StringComparison.Ordinal);
        Assert.DoesNotContain("5:30 pm", deA, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Toda_respuesta_bajo_c_va_sin_indice_y_sin_cache_y_las_paginas_llevan_el_meta_robots()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        using var cliente = escenario.Web.CreateClient();
        var noValido = $"/c/{new string('A', 43)}";

        foreach (var (ruta, esperado, esPagina) in new[]
                 {
                     (Ruta(cita.EnlaceDeGestion), HttpStatusCode.OK, true),
                     (noValido, HttpStatusCode.NotFound, true),
                     ($"{Ruta(cita.EnlaceDeGestion)}/calendario.ics", HttpStatusCode.OK, false),
                     ($"{noValido}/calendario.ics", HttpStatusCode.NotFound, false),
                 })
        {
            using var respuesta = await cliente.GetAsync(ruta, Cancelacion);

            Assert.Equal(esperado, respuesta.StatusCode);
            SinIndiceNiCache(respuesta);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(Cancelacion);
            if (esPagina)
            {
                Assert.Contains(Robots, cuerpo, StringComparison.Ordinal);
            }
        }

        // Lo que no cuelga de /c/ no los lleva.
        using var inicio = await cliente.GetAsync("/", Cancelacion);
        Assert.False(inicio.Headers.Contains("X-Robots-Tag"));
    }

    [Fact]
    public async Task Pasado_el_limite_de_visitas_Tu_cita_y_el_calendario_responden_429_amable_sin_indice_ni_cache()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        using var cliente = escenario.Web.CreateClient();

        // El límite de fábrica: 60 visitas por minuto desde una IP; Tu cita y el .ics cuentan juntos.
        for (var visita = 1; visita <= 60; visita++)
        {
            var ruta = visita % 2 == 0 ? Ruta(cita.EnlaceDeGestion) : $"{Ruta(cita.EnlaceDeGestion)}/calendario.ics";
            using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        foreach (var ruta in new[] { Ruta(cita.EnlaceDeGestion), $"{Ruta(cita.EnlaceDeGestion)}/calendario.ics" })
        {
            using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
            Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
            SinIndiceNiCache(respuesta);
            var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
            Assert.Contains(UI.Pages.DemasiadosIntentos.Mensaje, html, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Abrir_Tu_cita_no_escribe_bitacora_ni_outbox()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        var bitacora = await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora");
        var outbox = await escenario.EscalarAsync<long>("select count(*) from agenda.outbox");
        using var cliente = escenario.Web.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await AbrirAsync(cliente, Ruta(cita.EnlaceDeGestion))).Estado);

        Assert.Equal(bitacora, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"));
        Assert.Equal(outbox, await escenario.EscalarAsync<long>("select count(*) from agenda.outbox"));
    }

    [Fact]
    public async Task Ninguna_traza_ni_registro_lleva_el_token_ni_el_nombre_y_la_ruta_va_enmascarada()
    {
        var trazas = new ConcurrentQueue<Activity>();
        using var oyente = new ActivityListener
        {
            ShouldListenTo = fuente => fuente.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = trazas.Enqueue,
        };
        ActivitySource.AddActivityListener(oyente);

        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        var token = Token(cita.EnlaceDeGestion);
        using var cliente = escenario.Web.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await AbrirAsync(cliente, Ruta(cita.EnlaceDeGestion))).Estado);
        Assert.Equal(HttpStatusCode.NotFound, (await AbrirAsync(cliente, $"/c/{new string('A', 43)}")).Estado);

        Assert.Contains(trazas, t => t.GetTagItem("url.path") is "/c/{token}");
        Assert.All(trazas, traza =>
        {
            Assert.DoesNotContain(token[..20], traza.DisplayName, StringComparison.Ordinal);
            Assert.DoesNotContain(token[..20], traza.OperationName, StringComparison.Ordinal);
            Assert.All(traza.TagObjects, tag => Assert.DoesNotContain(token[..20], tag.Value?.ToString() ?? "", StringComparison.Ordinal));
        });
        var registros = Arnes.Registros(escenario.Web);
        Assert.DoesNotContain(registros, r => r.Mensaje.Contains(token[..20], StringComparison.Ordinal));
        Assert.DoesNotContain(registros, r => r.Mensaje.Contains("Ana Sofía", StringComparison.Ordinal));
    }

    [Fact]
    public void La_consulta_no_muestra_el_token_ni_la_Cita_el_nombre()
    {
        var token = new string('Q', 43);
        var cita = new CitaDelEnlace(
            new DateOnly(2026, 11, 3), new TimeOnly(16, 0), "Primera consulta", 30, "Ana Sofía López",
            "Hospital Star Médica, consultorio 101", EstadoDelEnlace.Agendada);

        Assert.DoesNotContain(token, new CitaPorEnlace(token).ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Ana", cita.ToString(), StringComparison.Ordinal);
        Assert.Contains("Primera consulta", cita.ToString(), StringComparison.Ordinal);
    }

    private static Task CancelarAsync(ReservaEnLineaTests.Escenario escenario, CitaAgendadaEnLinea cita, string motivo) =>
        escenario.EjecutarAsync(
            $"update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = '{motivo}', cancelada_en = now() where id = '{cita.CitaId.Valor}'");

    private static void Cancelada(string html)
    {
        Assert.Matches("""<span class="dsm-chip-de-estado dsm-chip-de-estado--cancelada">\s*<svg[\s\S]*?</svg>\s*Cancelada\s*</span>""", html);
        Assert.DoesNotContain("<span class=\"dsm-chip-de-estado\">", html, StringComparison.Ordinal);
        Assert.Matches("""<dt>Paciente</dt>\s*<dd>Ana Sofía López</dd>""", html);
        Assert.Matches("""<dt>Lugar</dt>\s*<dd>Hospital Star Médica, consultorio 101</dd>""", html);
        Assert.DoesNotContain("calendario.ics", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Agregar a mi calendario", html, StringComparison.Ordinal);
        Assert.Contains(Robots, html, StringComparison.Ordinal);
    }

    private static async Task EnlaceNoValidoAsync(HttpClient cliente, string ruta)
    {
        var (estado, html) = await AbrirAsync(cliente, ruta);

        Assert.Equal(HttpStatusCode.NotFound, estado);
        Assert.Contains(EnlaceNoValido.Titulo, html, StringComparison.Ordinal);
        Assert.Matches("""<a href="/reservar"[^>]*>Agendar cita</a>""", html);
        Assert.Contains(Robots, html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Tu cita</h1>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
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

    // La ruta de Tu cita, sin el sitio.
    private static string Ruta(string enlace) => new Uri(enlace).AbsolutePath;

    private static string Token(string enlace) => enlace[(enlace.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];
}
