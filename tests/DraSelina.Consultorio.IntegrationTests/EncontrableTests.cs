using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Lo que leen los buscadores (FR-29; entrada 7.4) contra la web real y PostgreSQL: títulos y descriptions de / y del
/// Aviso, el JSON-LD Physician de / con el horario de la base sembrada (lunes a viernes, 3:30 a 6:00 pm), robots.txt
/// (sin Disallow desde la entrada 7.6) y sitemap.xml. El meta robots y el X-Robots-Tag de /admin y /c/ los cubren
/// CancelarCitaTests y EntrarConSegundoFactorTests.
/// </summary>
public sealed partial class EncontrableTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/", "Traumatóloga en San Luis Potosí · Dra. Selina Medina Castillo",
        "Dra. Selina Medina Castillo, traumatóloga y ortopedista en San Luis Potosí. Rodilla, hombro, columna, cadera, fracturas y lesiones deportivas. Agenda tu cita en línea.")]
    [InlineData("/aviso-de-privacidad", "Aviso de privacidad · Traumatóloga en San Luis Potosí · Dra. Selina Medina Castillo",
        "Aviso de privacidad del consultorio de la Dra. Selina Medina Castillo, traumatóloga en San Luis Potosí.")]
    public async Task Cada_pagina_trae_su_titulo_y_su_description(string ruta, string titulo, string description)
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();

        var html = await cliente.GetStringAsync(ruta, Cancelacion);

        Assert.Equal(titulo, WebUtility.HtmlDecode(Assert.Single(Titulo().Matches(html)).Groups["texto"].Value));
        Assert.Equal(description, WebUtility.HtmlDecode(Assert.Single(Description().Matches(html)).Groups["texto"].Value));
    }

    [Fact]
    public async Task Inicio_trae_un_Physician_con_el_horario_de_la_base_sembrada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();

        var html = await cliente.GetStringAsync("/", Cancelacion);

        SinMasScriptQueElJsonLd(html);
        using var ficha = JsonDocument.Parse(Assert.Single(JsonLd().Matches(html)).Groups["json"].Value);
        var raiz = ficha.RootElement;
        Assert.Equal("https://schema.org", raiz.GetProperty("@context").GetString());
        Assert.Equal("Physician", raiz.GetProperty("@type").GetString());
        Assert.Equal("Dra. Selina Medina Castillo", raiz.GetProperty("name").GetString());
        Assert.Equal("http://localhost/", raiz.GetProperty("url").GetString());
        Assert.Equal(
            ["12093135", "14899218"],
            raiz.GetProperty("identifier").EnumerateArray().Select(i => i.GetProperty("value").GetString()));
        Assert.Equal("San Luis Potosí", raiz.GetProperty("address").GetProperty("addressLocality").GetString());
        Assert.False(raiz.TryGetProperty("telephone", out _));
        Assert.Equal(
            ["Monday 15:30-18:00", "Tuesday 15:30-18:00", "Wednesday 15:30-18:00", "Thursday 15:30-18:00", "Friday 15:30-18:00"],
            raiz.GetProperty("openingHoursSpecification").EnumerateArray().Select(h =>
                $"{h.GetProperty("dayOfWeek").GetString()!["https://schema.org/".Length..]} {h.GetProperty("opens").GetString()}-{h.GetProperty("closes").GetString()}"));
    }

    [Fact]
    public async Task El_Aviso_no_trae_ningun_script()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();

        var html = await cliente.GetStringAsync("/aviso-de-privacidad", Cancelacion);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Robots_no_bloquea_nada_y_apunta_al_sitemap()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();

        using var respuesta = await cliente.GetAsync("/robots.txt", Cancelacion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("text/plain", respuesta.Content.Headers.ContentType?.MediaType);
        var lineas = (await respuesta.Content.ReadAsStringAsync(Cancelacion)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // Sin Disallow: /admin y /c/ salen del índice por su noindex, que el buscador solo ve si las rastrea (recorrido
        // 7.5, Gerardo, 2026-11-17).
        Assert.Equal(["User-agent: *", "Sitemap: http://localhost/sitemap.xml"], lineas);
    }

    [Fact]
    public async Task El_sitemap_lista_inicio_y_el_Aviso()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();

        using var respuesta = await cliente.GetAsync("/sitemap.xml", Cancelacion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/xml", respuesta.Content.Headers.ContentType?.MediaType);
        var sitemap = XDocument.Parse(await respuesta.Content.ReadAsStringAsync(Cancelacion));
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        Assert.Equal(ns + "urlset", sitemap.Root!.Name);
        Assert.Equal(
            ["http://localhost/", "http://localhost/aviso-de-privacidad"],
            sitemap.Descendants(ns + "loc").Select(loc => loc.Value));
    }

    /// <summary>
    /// El único <c>&lt;script</c> que puede traer una página del sitio es un JSON-LD (README, "Sin script"): uno solo, y
    /// nada de blazor.web.js ni _framework.
    /// </summary>
    internal static void SinMasScriptQueElJsonLd(string html)
    {
        Assert.Single(JsonLd().Matches(html));
        Assert.DoesNotContain("<script", JsonLd().Replace(html, ""), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blazor.web.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("_framework/", html, StringComparison.Ordinal);
    }

    [GeneratedRegex("""<script type="application/ld\+json">(?<json>.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex JsonLd();

    [GeneratedRegex("""<title>(?<texto>[^<]*)</title>""")]
    private static partial Regex Titulo();

    [GeneratedRegex("""<meta name="description" content="(?<texto>[^"]*)"\s*/?>""")]
    private static partial Regex Description();
}
