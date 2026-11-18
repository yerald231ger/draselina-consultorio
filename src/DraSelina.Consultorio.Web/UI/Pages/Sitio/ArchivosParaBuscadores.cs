using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace DraSelina.Consultorio.UI.Pages.Sitio;

/// <summary>
/// <c>robots.txt</c> y <c>sitemap.xml</c> (FR-29; entrada 7.4). robots bloquea la administración y los Enlaces de
/// gestión y apunta al sitemap; el sitemap lista las páginas públicas para buscadores: <c>/</c> y el Aviso de
/// privacidad. Las URLs absolutas usan el esquema y el host de la petición (Gerardo, 2026-11-17).
/// </summary>
public static class ArchivosParaBuscadores
{
    private static readonly XNamespace Sitemaps = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>Las páginas que lista el sitemap.</summary>
    public static readonly IReadOnlyList<string> Paginas = ["/", "/aviso-de-privacidad"];

    public static IEndpointRouteBuilder MapArchivosParaBuscadores(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/robots.txt", (HttpContext contexto) => Results.Text(Robots(Base(contexto)), "text/plain", Encoding.UTF8));
        rutas.MapGet("/sitemap.xml", (HttpContext contexto) => Results.Text(Sitemap(Base(contexto)), "application/xml", Encoding.UTF8));
        return rutas;
    }

    /// <summary>El robots.txt con la URL base dada: "http://localhost".</summary>
    public static string Robots(string urlBase) =>
        $"""
        User-agent: *
        Disallow: /admin
        Disallow: /c/
        Sitemap: {urlBase}/sitemap.xml

        """;

    /// <summary>El sitemap.xml con la URL base dada.</summary>
    public static string Sitemap(string urlBase)
    {
        var documento = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                Sitemaps + "urlset",
                Paginas.Select(pagina => new XElement(Sitemaps + "url", new XElement(Sitemaps + "loc", urlBase + pagina)))));
        return documento.Declaration + "\n" + documento.ToString();
    }

    private static string Base(HttpContext contexto) => $"{contexto.Request.Scheme}://{contexto.Request.Host}";
}
