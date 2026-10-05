using System.Xml.Linq;

namespace DraSelina.Consultorio.ArchitectureTests;

/// <summary>Lee de los <c>.csproj</c> lo que la regla necesita: SDK y referencias a proyectos, paquetes, frameworks y ensamblados.</summary>
public static class LectorDeProyectos
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

    /// <summary>Rutas de todos los <c>.csproj</c> bajo la carpeta, sin excepción.</summary>
    public static IReadOnlyList<string> Archivos(string carpetaSrc) =>
        [.. Directory
            .EnumerateFiles(carpetaSrc, "*.csproj", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Archivos de MSBuild compartidos que existen: los de la raíz del repositorio y todo <c>.props</c> o
    /// <c>.targets</c> bajo <c>src/</c>. Lo que declaran llega a los proyectos sin pasar por su <c>.csproj</c>.
    /// </summary>
    public static IReadOnlyList<string> ArchivosCompartidos(string carpetaSrc)
    {
        var raiz = Path.GetDirectoryName(Path.GetFullPath(carpetaSrc))!;
        string[] deRaiz = ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props"];

        return [.. deRaiz
            .Select(nombre => Path.Combine(raiz, nombre))
            .Where(File.Exists)
            .Concat(Directory.EnumerateFiles(carpetaSrc, "*.props", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(carpetaSrc, "*.targets", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Referencias a paquetes, frameworks o ensamblados que declara un archivo compartido, como
    /// <c>Elemento Nombre</c>. Las entradas <c>PackageVersion</c> no cuentan: fijan versión, no referencian.
    /// </summary>
    public static IReadOnlyList<string> ReferenciasCompartidas(string xml)
    {
        string[] elementos = ["PackageReference", "GlobalPackageReference", "FrameworkReference", "Reference"];

        return [.. (XDocument.Parse(xml).Root?.Descendants() ?? [])
            .Where(e => elementos.Contains(e.Name.LocalName))
            .Select(e => $"{e.Name.LocalName} {e.Attribute("Include")?.Value ?? e.Attribute("Update")?.Value ?? "(sin nombre)"}")];
    }

    public static IReadOnlyList<Proyecto> Leer(string carpetaSrc) =>
        [.. Archivos(carpetaSrc).Select(ruta => LeerProyecto(ruta, File.ReadAllText(ruta)))];

    /// <summary>Interpreta el XML de un <c>.csproj</c>. Una referencia cuenta aunque lleve condición.</summary>
    public static Proyecto LeerProyecto(string archivo, string xml)
    {
        var raiz = XDocument.Parse(xml).Root
            ?? throw new InvalidOperationException($"{archivo} no tiene elemento raíz.");

        string[] Atributos(string elemento, params string[] atributos) =>
            [.. raiz.Descendants()
                .Where(e => e.Name.LocalName == elemento)
                .SelectMany(e => atributos.Select(a => e.Attribute(a)?.Value))
                .OfType<string>()
                .SelectMany(valor => valor.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))];

        string[] sdks =
        [
            .. (raiz.Attribute("Sdk")?.Value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            .. Atributos("Sdk", "Name"),
            .. Atributos("Import", "Sdk"),
        ];

        return new Proyecto(
            Nombre(archivo),
            [.. sdks.Distinct()],
            [.. Atributos("ProjectReference", "Include").Select(Nombre)],
            Atributos("PackageReference", "Include", "Update"),
            Atributos("FrameworkReference", "Include", "Update"),
            Atributos("Reference", "Include", "Update"));
    }

    /// <summary>De una ruta a un <c>.csproj</c>, su nombre sin carpeta, sin extensión y sin la raíz de nombres.</summary>
    public static string Nombre(string ruta)
    {
        var archivo = ruta.Replace('\\', '/').Split('/')[^1];
        var nombre = archivo.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? archivo[..^".csproj".Length] : archivo;
        return nombre.StartsWith(TablaDeReferencias.Raiz, StringComparison.Ordinal) ? nombre[TablaDeReferencias.Raiz.Length..] : nombre;
    }
}
