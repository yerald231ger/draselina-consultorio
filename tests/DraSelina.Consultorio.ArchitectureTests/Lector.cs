using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DraSelina.Consultorio.ArchitectureTests;

/// <summary>Lo que un <c>.csproj</c> declara y a la regla le importa.</summary>
public sealed record Proyecto(string Archivo, IReadOnlyList<string> Referencias);

/// <summary>Un archivo de código de Web: dónde está, qué namespace declara y qué namespaces usa.</summary>
/// <param name="Ruta">Ruta relativa al proyecto Web, con <c>/</c>.</param>
/// <param name="Namespace">El namespace que declara, con <c>namespace</c> o con <c>@namespace</c> en un <c>.razor</c>; nulo si no declara ninguno.</param>
/// <param name="Usos">Namespaces de sus directivas <c>using</c> y nombres <c>DraSelina.Consultorio.*</c> escritos completos.</param>
public sealed record Fuente(string Ruta, string? Namespace, IReadOnlyList<string> Usos, bool TieneGlobalUsing);

/// <summary>Lee de <c>src/</c> lo que las reglas necesitan: los <c>.csproj</c> y el código de Web.</summary>
public static partial class Lector
{
    public const string ArchivoDeSolucion = "DraSelina.Consultorio.slnx";

    /// <summary>La carpeta <c>src/</c> del repositorio, buscando la solución hacia arriba desde la carpeta de las pruebas.</summary>
    public static string CarpetaSrc()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            if (File.Exists(Path.Combine(carpeta.FullName, ArchivoDeSolucion)))
            {
                return Path.Combine(carpeta.FullName, "src");
            }
        }

        throw new InvalidOperationException($"No se encontró {ArchivoDeSolucion} arriba de {AppContext.BaseDirectory}.");
    }

    public static string CarpetaWeb(string carpetaSrc) => Path.Combine(carpetaSrc, $"{Reglas.Raiz}{Reglas.ProyectoWeb}");

    /// <summary>Todos los <c>.csproj</c> bajo <c>src/</c>, con sus referencias a otros proyectos.</summary>
    public static IReadOnlyList<Proyecto> Proyectos(string carpetaSrc) =>
        [.. Directory
            .EnumerateFiles(carpetaSrc, "*.csproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(ruta => LeerProyecto(Relativa(carpetaSrc, ruta), File.ReadAllText(ruta)))];

    /// <summary>Interpreta el XML de un <c>.csproj</c>. Una referencia cuenta aunque lleve condición.</summary>
    public static Proyecto LeerProyecto(string archivo, string xml)
    {
        var raiz = XDocument.Parse(xml).Root
            ?? throw new InvalidOperationException($"{archivo} no tiene elemento raíz.");

        return new Proyecto(
            archivo,
            [.. raiz.Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .OfType<string>()
                .SelectMany(valor => valor.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(NombreDeProyecto)]);
    }

    /// <summary>De una ruta a un <c>.csproj</c>, su nombre sin carpeta, sin extensión y sin la raíz de nombres.</summary>
    public static string NombreDeProyecto(string ruta)
    {
        var archivo = ruta.Replace('\\', '/').Split('/')[^1];
        var nombre = archivo.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? archivo[..^".csproj".Length] : archivo;
        return nombre.StartsWith(Reglas.Raiz, StringComparison.Ordinal) ? nombre[Reglas.Raiz.Length..] : nombre;
    }

    /// <summary>Los <c>.cs</c> y <c>.razor</c> de Web, sin las salidas de compilación del proyecto.</summary>
    public static IReadOnlyList<Fuente> Fuentes(string carpetaWeb) =>
        [.. new[] { "*.cs", "*.razor" }
            .SelectMany(patron => Directory.EnumerateFiles(carpetaWeb, patron, SearchOption.AllDirectories))
            .Select(ruta => (Ruta: ruta, Relativa: Relativa(carpetaWeb, ruta)))
            .Where(f => !f.Relativa.StartsWith("bin/", StringComparison.Ordinal) && !f.Relativa.StartsWith("obj/", StringComparison.Ordinal))
            .OrderBy(f => f.Relativa, StringComparer.Ordinal)
            .Select(f => LeerFuente(f.Relativa, File.ReadAllText(f.Ruta)))];

    /// <summary>Los <c>.razor</c> de Web con su texto, sin las salidas de compilación del proyecto.</summary>
    public static IReadOnlyList<(string Ruta, string Texto)> Componentes(string carpetaWeb) =>
        [.. Directory.EnumerateFiles(carpetaWeb, "*.razor", SearchOption.AllDirectories)
            .Select(ruta => (Ruta: ruta, Relativa: Relativa(carpetaWeb, ruta)))
            .Where(f => !f.Relativa.StartsWith("bin/", StringComparison.Ordinal) && !f.Relativa.StartsWith("obj/", StringComparison.Ordinal))
            .OrderBy(f => f.Relativa, StringComparer.Ordinal)
            .Select(f => (f.Relativa, File.ReadAllText(f.Ruta)))];

    /// <summary>
    /// Los <c>GlobalUsings.g.cs</c> que la compilación de Web deja en <c>obj/</c>: ahí quedan escritos los
    /// <c>using</c> implícitos del SDK. Vacío si Web no se ha compilado.
    /// </summary>
    public static IReadOnlyList<Fuente> UsingsImplicitos(string carpetaWeb)
    {
        var obj = Path.Combine(carpetaWeb, "obj");
        if (!Directory.Exists(obj))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(obj, "*.GlobalUsings.g.cs", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(ruta => LeerFuente(Relativa(carpetaWeb, ruta), File.ReadAllText(ruta)))];
    }

    /// <summary>Carpetas de Web que existen, relativas al proyecto.</summary>
    public static IReadOnlySet<string> Carpetas(string carpetaWeb) =>
        Directory.EnumerateDirectories(carpetaWeb, "*", SearchOption.AllDirectories)
            .Select(ruta => Relativa(carpetaWeb, ruta))
            .ToHashSet();

    /// <summary>Interpreta el texto de un archivo de código. Los comentarios de línea no cuentan.</summary>
    public static Fuente LeerFuente(string ruta, string texto)
    {
        string? espacio = null;
        var usos = new List<string>();
        var global = false;

        foreach (var cruda in texto.Split('\n'))
        {
            var corte = cruda.IndexOf("//", StringComparison.Ordinal);
            var linea = corte >= 0 ? cruda[..corte] : cruda;

            if (DeclaracionDeNamespace().Match(linea) is { Success: true } declaracion)
            {
                espacio ??= declaracion.Groups[1].Value;
                continue;
            }

            if (DirectivaUsing().Match(linea) is { Success: true } directiva)
            {
                global |= directiva.Groups[1].Success;
                usos.Add(directiva.Groups[2].Value);
                continue;
            }

            usos.AddRange(NombreCompleto().Matches(linea).Select(m => m.Value.TrimEnd('.')));
        }

        return new Fuente(ruta, espacio, [.. usos.Distinct()], global);
    }

    private static string Relativa(string carpeta, string ruta) => Path.GetRelativePath(carpeta, ruta).Replace('\\', '/');

    // namespace X  ·  @namespace X (Razor)
    [GeneratedRegex(@"^\s*@?namespace\s+([\w.]+)")]
    private static partial Regex DeclaracionDeNamespace();

    // using X;  ·  using static X;  ·  using Alias = X;  ·  global using X;  ·  @using X (Razor)
    [GeneratedRegex(@"^\s*(global\s+)?@?using\s+(?:static\s+)?(?:\w+\s*=\s*)?([\w.]+)\s*;?\s*$")]
    private static partial Regex DirectivaUsing();

    [GeneratedRegex(@"\bDraSelina\.Consultorio\.[A-Za-z_][\w.]*")]
    private static partial Regex NombreCompleto();
}
