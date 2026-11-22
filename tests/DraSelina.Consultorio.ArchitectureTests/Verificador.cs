using System.Text.RegularExpressions;

namespace DraSelina.Consultorio.ArchitectureTests;

/// <summary>Algo que las tablas no permiten. Nombra dónde está y qué referencia.</summary>
public sealed record Violacion(string Donde, string Referencia, string Regla)
{
    public override string ToString() => $"{Donde} -> {Referencia}: {Regla}";
}

/// <summary>
/// Compara proyectos y archivos de código contra <see cref="Reglas"/>. Recibe ambos como dato para poder
/// probarse con casos sintéticos, sin tocar <c>src/</c>.
/// </summary>
public static partial class Verificador
{
    public const string ProyectoDesconocido = "proyecto desconocido: la arquitectura no lo nombra";
    public const string ProyectoNoPermitido = "referencia de proyecto que la tabla de la arquitectura no permite";
    public const string CapaDesconocida = "carpeta fuera de las capas de la arquitectura";
    public const string NamespaceFueraDeSuCarpeta = "el namespace no corresponde a la carpeta de la capa";
    public const string CapaNoPermitida = "dependencia entre namespaces que la tabla de AD-1 no permite";
    public const string PaqueteProhibido = "paquete o framework prohibido: Kernel, contratos y dominio solo usan la biblioteca base (T-28, AD-12)";
    public const string GlobalUsingProhibido = "global using prohibido: llegaría a todas las capas sin que esta prueba lo vea";

    public const string AdministracionSinAuthorize =
        "página de administración sin [Authorize]: toda ruta bajo /admin, salvo las de /admin/entrar, pide sesión (AD-22)";

    public const string UsingImplicitoProhibido = "using implícito fuera de System.*: llegaría a todas las capas sin que esta prueba lo vea";

    public const string CasoDeUsoAjeno =
        "código de la carpeta de otro caso de uso: lo que usan varios va en Features/Compartido/ (arquitectura, AD-4)";

    /// <summary>
    /// Ningún archivo de <c>Modules/&lt;Módulo&gt;/Features/&lt;Carpeta&gt;/</c> nombra un tipo declarado en la carpeta de
    /// otro caso de uso del mismo módulo (AD-4). <c>Compartido/</c> no es caso de uso: lo suyo lo usa cualquiera, pero
    /// tampoco puede nombrar lo de un caso de uso. Fuera de Features (Infrastructure, UI) no se mira: ahí se implementan
    /// los puertos y se llaman los handlers. Un nombre que la carpeta que lo usa también declara es suyo y no cuenta.
    /// Los comentarios y las cadenas no cuentan. Recibe los <c>.cs</c> con su texto.
    /// </summary>
    public static IReadOnlyList<Violacion> VerificarCasosDeUso(IEnumerable<(string Ruta, string Texto)> archivos)
    {
        var enFeatures = archivos
            .Select(a => (a.Ruta, Partes: a.Ruta.Split('/'), Codigo: SinComentariosNiCadenas(a.Texto)))
            .Where(a => a.Partes is ["Modules", _, "Features", _, _])
            .Select(a => (a.Ruta, Modulo: a.Partes[1], Carpeta: a.Partes[3], a.Codigo))
            .ToList();

        // Solo los de primer nivel (fuera de toda llave, con namespace de archivo): uno anidado se alcanza por el de afuera.
        var declarados = enFeatures
            .SelectMany(a => DeclaracionDeTipo().Matches(a.Codigo)
                .Where(m => Profundidad(a.Codigo[..m.Index]) == 0)
                .Select(m => (a.Modulo, a.Carpeta, Nombre: m.Groups["nombre"].Value)))
            .Distinct()
            .ToList();

        return [.. from archivo in enFeatures
            let propios = declarados.Where(d => d.Modulo == archivo.Modulo && d.Carpeta == archivo.Carpeta).Select(d => d.Nombre).ToHashSet()
            from tipo in declarados
            where tipo.Modulo == archivo.Modulo
                && tipo.Carpeta != archivo.Carpeta
                && tipo.Carpeta != Compartido
                && !propios.Contains(tipo.Nombre)
                && Regex.IsMatch(archivo.Codigo, $@"\b{Regex.Escape(tipo.Nombre)}\b")
            select new Violacion(archivo.Ruta, $"{tipo.Carpeta}/{tipo.Nombre}", CasoDeUsoAjeno)];
    }

    private const string Compartido = "Compartido";

    private static int Profundidad(string codigo) => codigo.Count(c => c == '{') - codigo.Count(c => c == '}');

    private static string SinComentariosNiCadenas(string texto) =>
        string.Join('\n', texto.Split('\n').Select(linea => Cadena().Replace(linea.Split("//")[0], "\"\"")));

    // class X · record X · record struct X · interface X · struct X · enum X
    [GeneratedRegex(@"\b(?:class|record|interface|struct|enum)\s+(?<nombre>[A-Z]\w*)")]
    private static partial Regex DeclaracionDeTipo();

    // "..." con escapes; las interpoladas y las verbatim de una línea también.
    [GeneratedRegex(@"[$@]*""(?:[^""\\]|\\.)*""")]
    private static partial Regex Cadena();

    /// <summary>
    /// Los <c>using</c> implícitos que el SDK genera para Web: solo se admiten los de <c>System.*</c>.
    /// Recibe los <c>GlobalUsings.g.cs</c> generados, ya leídos.
    /// </summary>
    public static IReadOnlyList<Violacion> VerificarUsingsImplicitos(IEnumerable<Fuente> generados) =>
        [.. generados.SelectMany(generado => generado.Usos
            .Where(uso => !EsOEstaDentro(uso, "System"))
            .Select(uso => new Violacion(generado.Ruta, uso, UsingImplicitoProhibido)))];

    /// <summary>
    /// Toda página con una ruta bajo <c>/admin</c> que no sea de <c>/admin/entrar</c> (inicio de sesión y segundo factor)
    /// lleva <c>@attribute [Authorize]</c> (AD-22). Recibe los <c>.razor</c> con su texto.
    /// </summary>
    public static IReadOnlyList<Violacion> VerificarPaginasDeAdministracion(IEnumerable<(string Ruta, string Texto)> componentes) =>
        [.. from componente in componentes
            let rutas = DirectivaPage().Matches(componente.Texto).Select(m => m.Groups["ruta"].Value).ToList()
            where rutas.Any(EsDeAdministracionConSesion) && !AtributoAuthorize().IsMatch(componente.Texto)
            select new Violacion(componente.Ruta, rutas.First(EsDeAdministracionConSesion), AdministracionSinAuthorize)];

    private static bool EsDeAdministracionConSesion(string ruta) =>
        EsOEstaDebajo(ruta, "/admin") && !EsOEstaDebajo(ruta, "/admin/entrar");

    private static bool EsOEstaDebajo(string ruta, string raiz) =>
        string.Equals(ruta, raiz, StringComparison.OrdinalIgnoreCase)
        || ruta.StartsWith($"{raiz}/", StringComparison.OrdinalIgnoreCase);

    // @page "/admin/..."
    [GeneratedRegex("""^\s*@page\s+"(?<ruta>[^"]+)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex DirectivaPage();

    // @attribute [Authorize] · [Authorize(...)] · [Microsoft.AspNetCore.Authorization.Authorize]
    [GeneratedRegex("""^\s*@attribute\s+\[\s*(Microsoft\.AspNetCore\.Authorization\.)?Authorize(Attribute)?\s*[\(\]]""", RegexOptions.Multiline)]
    private static partial Regex AtributoAuthorize();

    public static IReadOnlyList<Violacion> VerificarProyectos(IEnumerable<Proyecto> proyectos)
    {
        var violaciones = new List<Violacion>();

        foreach (var proyecto in proyectos)
        {
            var nombre = Lector.NombreDeProyecto(proyecto.Archivo);
            if (!Reglas.Proyectos.TryGetValue(nombre, out var fila) || fila.Archivo != proyecto.Archivo)
            {
                violaciones.Add(new Violacion(proyecto.Archivo, nombre, ProyectoDesconocido));
                continue;
            }

            violaciones.AddRange(proyecto.Referencias
                .Where(referencia => !fila.PuedeReferenciar.Contains(referencia))
                .Select(referencia => new Violacion(nombre, referencia, ProyectoNoPermitido)));
        }

        return violaciones;
    }

    public static IReadOnlyList<Violacion> VerificarFuentes(IEnumerable<Fuente> fuentes)
    {
        var violaciones = new List<Violacion>();

        foreach (var fuente in fuentes)
        {
            if (fuente.TieneGlobalUsing)
            {
                violaciones.Add(new Violacion(fuente.Ruta, "global using", GlobalUsingProhibido));
            }

            var capa = CapaDe(fuente.Ruta);
            if (capa is null)
            {
                // Fuera de las carpetas reservadas es el host, que compone y puede usar todo.
                if (Reglas.CarpetasReservadas.Any(c => fuente.Ruta.StartsWith($"{c}/", StringComparison.Ordinal)))
                {
                    violaciones.Add(new Violacion(fuente.Ruta, fuente.Ruta, CapaDesconocida));
                }

                continue;
            }

            var propio = $"{Reglas.Raiz}{capa.Nombre}";
            if (fuente.Namespace is { } espacio && !EsOEstaDentro(espacio, propio))
            {
                violaciones.Add(new Violacion(fuente.Ruta, espacio, NamespaceFueraDeSuCarpeta));
            }

            foreach (var uso in fuente.Usos)
            {
                if (EsOEstaDentro(uso, Reglas.Raiz.TrimEnd('.')))
                {
                    var destino = CapaDelNamespace(uso);
                    if (destino != capa.Nombre && (destino is null || !capa.PuedeUsar.Contains(destino)))
                    {
                        violaciones.Add(new Violacion(fuente.Ruta, uso, CapaNoPermitida));
                    }
                }
                else if (capa.Pura && !EsOEstaDentro(uso, "System"))
                {
                    violaciones.Add(new Violacion(fuente.Ruta, uso, PaqueteProhibido));
                }
            }
        }

        return violaciones;
    }

    /// <summary>La capa a la que pertenece un archivo, por su carpeta; nulo si no cae en ninguna.</summary>
    public static Capa? CapaDe(string ruta) =>
        Reglas.Capas.Values
            .Where(c => ruta.StartsWith($"{c.Carpeta}/", StringComparison.Ordinal))
            .MaxBy(c => c.Carpeta.Length);

    /// <summary>La capa a la que pertenece un namespace <c>DraSelina.Consultorio.*</c>; nulo si es el host u otro.</summary>
    private static string? CapaDelNamespace(string espacio) =>
        Reglas.Capas.Keys
            .Where(nombre => EsOEstaDentro(espacio, $"{Reglas.Raiz}{nombre}"))
            .MaxBy(nombre => nombre.Length);

    private static bool EsOEstaDentro(string espacio, string raiz) =>
        espacio == raiz || espacio.StartsWith($"{raiz}.", StringComparison.Ordinal);
}
