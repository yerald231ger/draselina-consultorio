using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Sitio · Inicio (CAP-21; entrada 7.1) contra la web real y PostgreSQL: <see cref="HorarioDeAtencion"/> se lee sin
/// sesión, Ubicación y horarios muestra la Agenda base y cambia con ella, y el encabezado del sitio trae las anclas y
/// "Agendar cita" sin ningún script. La reserva conserva su encabezado. Base sembrada: lunes a viernes, 3:30 a 6:00 pm.
/// Entrada 7.2: la portada, Sobre mí y Padecimientos con los datos de la doctora, lo pendiente marcado y sin
/// testimonios ni imágenes.
/// </summary>
public sealed partial class SitioInicioTests(Arnes arnes)
{
    private static readonly DayOfWeek[] DeLunesADomingo =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HorarioDeAtencion_se_lee_sin_sesion_con_la_base_sembrada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);

        var resultado = await ambito.ServiceProvider.GetRequiredService<IQueryHandler<HorarioDeAtencion, IReadOnlyList<DiaDeLaBase>>>()
            .HandleAsync(new HorarioDeAtencion(), Cancelacion);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(DeLunesADomingo, resultado.Valor.Select(d => d.Dia));
        var tarde = new TramoDeLaAgenda(new TimeOnly(15, 30), new TimeOnly(18, 0));
        Assert.All(resultado.Valor.Take(5), d => Assert.Equal([tarde], d.Tramos));
        Assert.All(resultado.Valor.Skip(5), d => Assert.Empty(d.Tramos));
    }

    [Fact]
    public async Task Ubicacion_y_horarios_muestra_la_base_y_cambia_con_ella()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var antes = await cliente.GetStringAsync("/", Cancelacion);
        var ubicacion = Seccion(antes, "ubicacion");
        Assert.Contains("Lunes a viernes", ubicacion, StringComparison.Ordinal);
        Assert.Contains("3:30 a 6:00 pm, con cita", ubicacion, StringComparison.Ordinal);
        Assert.DoesNotContain("Sábado", ubicacion, StringComparison.Ordinal);
        Assert.Contains("Hospital Star Médica, consultorio 101", ubicacion, StringComparison.Ordinal);
        Assert.Contains("Mariano Arista No. 735", ubicacion, StringComparison.Ordinal);
        Assert.Contains(">Cómo llegar</a>", ubicacion, StringComparison.Ordinal);
        Assert.Matches(MapaIncrustado(), ubicacion);

        await AbrirElSabadoAsync(escenario);

        var despues = Seccion(await cliente.GetStringAsync("/", Cancelacion), "ubicacion");
        Assert.Contains("Lunes a viernes", despues, StringComparison.Ordinal);
        Assert.Contains("Sábado", despues, StringComparison.Ordinal);
        Assert.Contains("10:00 am a 1:00 pm, con cita", despues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_encabezado_del_sitio_trae_las_anclas_y_Agendar_cita_sin_scripts()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();

        using var respuesta = await cliente.GetAsync("/", Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = await respuesta.Content.ReadAsStringAsync(Cancelacion);

        var encabezado = Assert.Single(Encabezado().Matches(html)).Value;
        Assert.Matches(AgendarCitaAReservar(), encabezado);
        foreach (var ancla in new[] { "inicio", "sobre-mi", "padecimientos", "ubicacion" })
        {
            Assert.Contains($"href=\"/#{ancla}\"", encabezado, StringComparison.Ordinal);
            Assert.Contains($"id=\"{ancla}\"", html, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blazor.web.js", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Portada_Sobre_mi_y_Padecimientos_traen_los_datos_de_la_doctora()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();

        using var respuesta = await cliente.GetAsync("/", Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = await respuesta.Content.ReadAsStringAsync(Cancelacion);

        var portada = Seccion(html, "inicio");
        Assert.Matches(AgendarCitaEnLineaAReservar(), portada);
        foreach (var dato in new[] { "Traumatología y Ortopedia", "Dra. Selina Medina Castillo", "Céd. Prof. 12093135", "Céd. Especialidad 14899218", "Certificada por el Consejo de Ortopedia", ">SM<", "Escríbenos por WhatsApp" })
        {
            Assert.Contains(dato, portada, StringComparison.Ordinal);
        }

        var sobreMi = Seccion(html, "sobre-mi");
        Assert.Contains("Consejo de Ortopedia, 24/8379/2", sobreMi, StringComparison.Ordinal);
        foreach (var dato in new[] { "12093135", "14899218", "[Pendiente: universidad]", "[Pendiente: semblanza", "[Pendiente: afiliaciones" })
        {
            Assert.Contains(dato, sobreMi, StringComparison.Ordinal);
        }

        // La universidad no está decidida (UANL o UASLP): ninguna se nombra.
        Assert.DoesNotContain("UANL", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Universidad", html, StringComparison.Ordinal);

        var padecimientos = Seccion(html, "padecimientos");
        Assert.Equal(
            ["Rodilla", "Hombro", "Columna", "Cadera", "Fracturas", "Lesiones deportivas"],
            NombreDePadecimiento().Matches(padecimientos).Select(m => m.Groups[1].Value));
        Assert.Contains("class=\"dsm-pendiente\"", padecimientos, StringComparison.Ordinal);

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("testimonio", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task La_reserva_conserva_su_encabezado_sin_anclas()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();

        var html = await cliente.GetStringAsync("/reservar", Cancelacion);

        var encabezado = Assert.Single(Encabezado().Matches(html)).Value;
        Assert.DoesNotContain("<nav", encabezado, StringComparison.Ordinal);
        Assert.DoesNotContain("Agendar cita", encabezado, StringComparison.Ordinal);
    }

    // La sección con ese id, hasta su cierre, con las entidades decodificadas: Blazor codifica el texto dinámico
    // ("M&#xE9;dica", "&amp;").
    private static string Seccion(string html, string id)
    {
        var inicio = html.IndexOf($"<section id=\"{id}\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"No hay sección '{id}'.");
        var fin = html.IndexOf("</section>", inicio, StringComparison.Ordinal);
        return WebUtility.HtmlDecode(html[inicio..fin]);
    }

    // El sábado 10:00–13:00 con CambiarAgendaBase, como lo haría el Administrador en Configuración; lunes a viernes
    // conservan su tarde.
    private static async Task AbrirElSabadoAsync(Escenario escenario)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "cuenta-del-administrador"), new Claim(ClaimTypes.Name, "administradora")],
            authenticationType: "Prueba")));

        var comando = new CambiarAgendaBase
        {
            Dias =
            [
                .. DeLunesADomingo.Select(dia => new DiaDeLaBaseAGuardar
                {
                    Dia = dia,
                    Tramos = dia switch
                    {
                        DayOfWeek.Saturday => [new TramoAGuardar { Inicio = new TimeOnly(10, 0), Fin = new TimeOnly(13, 0) }],
                        DayOfWeek.Sunday => [],
                        _ => [new TramoAGuardar { Inicio = new TimeOnly(15, 30), Fin = new TimeOnly(18, 0) }],
                    },
                }),
            ],
        };

        var resultado = await ambito.ServiceProvider.GetRequiredService<ICommandHandler<CambiarAgendaBase, AgendaBaseCambiada>>()
            .HandleAsync(comando, Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
    }

    [GeneratedRegex("""<a(?=[^>]*href="/reservar")[^>]*class="[^"]*\bbtn-primary\b[^"]*"[^>]*>\s*Agendar cita en línea\s*<svg""")]
    private static partial Regex AgendarCitaEnLineaAReservar();

    [GeneratedRegex("""<h3 class="dsm-padecimiento__nombre">([^<]+)</h3>""")]
    private static partial Regex NombreDePadecimiento();

    [GeneratedRegex("""<header[^>]*>.*?</header>""", RegexOptions.Singleline)]
    private static partial Regex Encabezado();

    [GeneratedRegex("""<a(?=[^>]*href="/reservar")[^>]*class="[^"]*\bbtn-primary\b[^"]*"[^>]*>\s*Agendar cita\s*</a>""")]
    private static partial Regex AgendarCitaAReservar();

    [GeneratedRegex("""<iframe(?=[^>]*src="https://www\.google\.com/maps\?q=[^"]+&output=embed")(?=[^>]*loading="lazy")(?=[^>]*title="Mapa del consultorio")[^>]*>""")]
    private static partial Regex MapaIncrustado();
}
