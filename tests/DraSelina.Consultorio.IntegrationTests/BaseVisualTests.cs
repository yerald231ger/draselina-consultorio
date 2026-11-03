using System.Net;
using System.Text.RegularExpressions;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La base visual del sitio: Bootstrap, Montserrat y los tokens de DESIGN.md, servidos por la propia web, y una
/// página en SSR estático sin ningún script (AD-11). Todo se pide a la web real del arnés.
/// </summary>
public sealed partial class BaseVisualTests(Arnes arnes)
{
    private const string ArchivoDeSolucion = "DraSelina.Consultorio.slnx";

    private static readonly string RutaDeDesign = Path.Combine(
        "_bmad-output", "initiative-draselina", "ux-appointment-booking", "DESIGN.md");

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Inicio_trae_el_encabezado_el_nombre_el_boton_y_las_hojas()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();

        using var respuesta = await cliente.GetAsync("/", Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = await respuesta.Content.ReadAsStringAsync(Cancelacion);

        Assert.Matches(EncabezadoConNombre(), html);
        Assert.Matches(H1ConNombre(), html);
        Assert.Matches(BotonAgendar(), html);

        var hojas = Hojas(html);
        Assert.Contains(hojas, href => href.Contains("lib/bootstrap/bootstrap.min", StringComparison.Ordinal));
        Assert.Contains(hojas, href => href.Contains("css/consultorio", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Inicio_no_trae_ningun_script()
    {
        var html = await PedirInicioAsync();

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blazor.web.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("_framework/", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cada_hoja_que_enlaza_Inicio_se_sirve_como_css()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();
        var html = await cliente.GetStringAsync("/", Cancelacion);

        var hojas = Hojas(html);
        Assert.Equal(2, hojas.Count);

        foreach (var href in hojas)
        {
            using var respuesta = await cliente.GetAsync(href, Cancelacion);
            Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"'{href}' respondió {(int)respuesta.StatusCode}.");
            Assert.Equal("text/css", respuesta.Content.Headers.ContentType?.MediaType);
        }

        var bootstrap = await cliente.GetStringAsync(hojas.Single(h => h.Contains("bootstrap", StringComparison.Ordinal)), Cancelacion);
        Assert.Contains("Bootstrap  v5.3.8", bootstrap, StringComparison.Ordinal);

        var propia = await cliente.GetStringAsync(HojaPropia(hojas), Cancelacion);
        Assert.Matches(PrimarioDeclarado(), propia);
    }

    [Fact]
    public async Task Cada_fuente_de_la_hoja_propia_se_sirve()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();
        var html = await cliente.GetStringAsync("/", Cancelacion);

        var urlDeLaHoja = new Uri(cliente.BaseAddress!, HojaPropia(Hojas(html)));
        var propia = await cliente.GetStringAsync(urlDeLaHoja, Cancelacion);

        var fuentes = UrlsEnCss().Matches(propia).Select(m => m.Groups["url"].Value)
            .Where(url => url.EndsWith(".woff2", StringComparison.Ordinal))
            .ToList();

        // Los cuatro pesos de DESIGN.md: 400, 500, 600 y 700.
        foreach (var peso in new[] { "400", "500", "600", "700" })
        {
            Assert.Contains(fuentes, url => url.Contains($"-{peso}-", StringComparison.Ordinal));
        }

        foreach (var fuente in fuentes)
        {
            using var respuesta = await cliente.GetAsync(new Uri(urlDeLaHoja, fuente), Cancelacion);
            Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"'{fuente}' respondió {(int)respuesta.StatusCode}.");
        }
    }

    [Fact]
    public async Task Ni_Inicio_ni_la_hoja_propia_piden_nada_a_terceros()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();
        var html = await cliente.GetStringAsync("/", Cancelacion);
        var propia = await cliente.GetStringAsync(HojaPropia(Hojas(html)), Cancelacion);

        Assert.Empty(AtributoConUrlAbsoluta().Matches(html).Select(m => m.Value));
        Assert.DoesNotContain(UrlsEnCss().Matches(propia).Select(m => m.Groups["url"].Value), EsAbsoluta);
        Assert.DoesNotMatch(ImportEnCss(), propia);
    }

    [Fact]
    public async Task La_hoja_propia_declara_cada_color_de_DESIGN_md_con_su_valor()
    {
        var colores = ColoresDeDesign(File.ReadAllText(Path.Combine(RaizDelRepositorio(), RutaDeDesign)));
        Assert.NotEmpty(colores);

        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();
        var html = await cliente.GetStringAsync("/", Cancelacion);
        var propia = await cliente.GetStringAsync(HojaPropia(Hojas(html)), Cancelacion);

        var declarados = VariableDeColor().Matches(propia)
            .GroupBy(m => m.Groups["clave"].Value, m => m.Groups["valor"].Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var problemas = new List<string>();
        foreach (var (clave, valor) in colores)
        {
            if (!declarados.TryGetValue(clave, out var valores))
            {
                problemas.Add($"falta --dsm-{clave} (DESIGN.md: {valor})");
            }
            else if (valores.Count > 1)
            {
                problemas.Add($"--dsm-{clave} se declara {valores.Count} veces ({string.Join(", ", valores)})");
            }
            else if (valores[0] is var enLaHoja && !string.Equals(enLaHoja, valor, StringComparison.OrdinalIgnoreCase))
            {
                problemas.Add($"--dsm-{clave} es {enLaHoja} y DESIGN.md dice {valor}");
            }
        }

        Assert.True(problemas.Count == 0, "La hoja propia no coincide con DESIGN.md: " + string.Join("; ", problemas));
    }

    private async Task<string> PedirInicioAsync()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = web.CreateClient();
        return await cliente.GetStringAsync("/", Cancelacion);
    }

    private static List<string> Hojas(string html) =>
        EnlaceAHoja().Matches(html).Select(m => m.Groups["href"].Value).ToList();

    private static string HojaPropia(List<string> hojas) =>
        hojas.Single(h => h.Contains("css/consultorio", StringComparison.Ordinal));

    private static bool EsAbsoluta(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("//", StringComparison.Ordinal);

    /// <summary>Los colores de la cabecera YAML de DESIGN.md: las líneas con sangría bajo <c>colors:</c>.</summary>
    private static List<(string Clave, string Valor)> ColoresDeDesign(string design)
    {
        var colores = new List<(string, string)>();
        var dentro = false;
        foreach (var linea in design.Split('\n'))
        {
            if (!dentro)
            {
                dentro = linea.TrimEnd() == "colors:";
                continue;
            }

            if (linea.Length == 0 || !char.IsWhiteSpace(linea[0]))
            {
                break;
            }

            var color = ColorDeDesign().Match(linea);
            if (color.Success)
            {
                colores.Add((color.Groups["clave"].Value, color.Groups["valor"].Value));
            }
        }

        return colores;
    }

    private static string RaizDelRepositorio()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            if (File.Exists(Path.Combine(carpeta.FullName, ArchivoDeSolucion)))
            {
                return carpeta.FullName;
            }
        }

        throw new InvalidOperationException($"No se encontró {ArchivoDeSolucion} arriba de {AppContext.BaseDirectory}.");
    }

    [GeneratedRegex("""<header[^>]*>.*Dra\. Selina Medina Castillo.*Traumatología y Ortopedia.*</header>""", RegexOptions.Singleline)]
    private static partial Regex EncabezadoConNombre();

    [GeneratedRegex("""<h1[^>]*>\s*Dra\. Selina Medina Castillo\s*</h1>""")]
    private static partial Regex H1ConNombre();

    [GeneratedRegex("""<a(?=[^>]*href="/reservar")[^>]*class="[^"]*\bbtn-primary\b[^"]*"[^>]*>\s*Agendar una cita\s*</a>""")]
    private static partial Regex BotonAgendar();

    [GeneratedRegex("""<link[^>]*rel="stylesheet"[^>]*href="(?<href>[^"]+)""")]
    private static partial Regex EnlaceAHoja();

    [GeneratedRegex("""(?:href|src)\s*=\s*["']?\s*(?:https?:)?//""", RegexOptions.IgnoreCase)]
    private static partial Regex AtributoConUrlAbsoluta();

    [GeneratedRegex("""url\(\s*['"]?(?<url>[^'")\s]+)""")]
    private static partial Regex UrlsEnCss();

    [GeneratedRegex("""@import""")]
    private static partial Regex ImportEnCss();

    [GeneratedRegex("""--dsm-primary:\s*#6B2A45\s*;""", RegexOptions.IgnoreCase)]
    private static partial Regex PrimarioDeclarado();

    [GeneratedRegex("""--dsm-(?<clave>[a-z0-9-]+):\s*(?<valor>#[0-9A-Fa-f]{3,8})\s*;""")]
    private static partial Regex VariableDeColor();

    [GeneratedRegex("""^\s+(?<clave>[a-z0-9-]+):\s*'(?<valor>#[0-9A-Fa-f]{3,8})'""")]
    private static partial Regex ColorDeDesign();
}
