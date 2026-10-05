namespace DraSelina.Consultorio.ArchitectureTests;

/// <summary>Una capa dentro de <c>DraSelina.Consultorio.Web</c>: un namespace y su carpeta.</summary>
/// <param name="Nombre">Namespace sin la raíz <c>DraSelina.Consultorio.</c></param>
/// <param name="Carpeta">Carpeta relativa al proyecto Web.</param>
/// <param name="PuedeUsar">Capas que puede usar. Lo que no está, está prohibido.</param>
/// <param name="Pura">Kernel, contratos y dominio: solo la biblioteca base de .NET (T-28, AD-12).</param>
public sealed record Capa(string Nombre, string Carpeta, IReadOnlySet<string> PuedeUsar, bool Pura);

/// <summary>Un proyecto de <c>src/</c> y los proyectos que puede referenciar.</summary>
/// <param name="Nombre">Nombre sin la raíz <c>DraSelina.Consultorio.</c></param>
public sealed record FilaDeProyecto(string Nombre, IReadOnlySet<string> PuedeReferenciar)
{
    /// <summary>Ruta del <c>.csproj</c> relativa a <c>src/</c>: la carpeta lleva el nombre completo del proyecto.</summary>
    public string Archivo => $"{Reglas.Raiz}{Nombre}/{Reglas.Raiz}{Nombre}.csproj";
}

/// <summary>
/// Las dos tablas de "Invariants &amp; Rules" de la arquitectura (AD-1) y el Structural Seed, como datos:
/// dependencias entre namespaces dentro de Web, y referencias entre proyectos.
/// </summary>
public static class Reglas
{
    public const string Raiz = "DraSelina.Consultorio.";
    public const string ProyectoWeb = "Web";

    private const string Kernel = "Kernel";
    private const string ContratosBase = "BuildingBlocks.Contracts";
    private const string Contenido = "Contenido";

    private sealed record Modulo(string Nombre, bool TieneDominio);

    /// <summary>Módulos con Features e Infrastructure. Notificaciones no tiene dominio.</summary>
    private static readonly Modulo[] Modulos =
    [
        new("Agenda", TieneDominio: true),
        new("Pacientes", TieneDominio: true),
        new("Notificaciones", TieneDominio: false),
    ];

    /// <summary>Carpetas de Web reservadas a las capas: un archivo ahí debe caer en una capa de la tabla.</summary>
    public static IReadOnlyList<string> CarpetasReservadas { get; } = ["Kernel", "BuildingBlocks", "Modules", "UI"];

    public static IReadOnlyDictionary<string, Capa> Capas { get; } = ConstruirCapas();

    public static IReadOnlyDictionary<string, FilaDeProyecto> Proyectos { get; } = new FilaDeProyecto[]
    {
        new(ProyectoWeb, Conjunto("ServiceDefaults")),
        new("MigrationService", Conjunto(ProyectoWeb, "ServiceDefaults")),
        new("AppHost", Conjunto(ProyectoWeb, "MigrationService")),
        new("ServiceDefaults", Conjunto()),
    }.ToDictionary(f => f.Nombre);

    private static Dictionary<string, Capa> ConstruirCapas()
    {
        string[] contratosDeModulo = [.. Modulos.Select(m => $"{m.Nombre}.Contracts"), $"{Contenido}.Contracts"];

        List<Capa> capas =
        [
            new(Kernel, "Kernel", Conjunto(), Pura: true),
            new(ContratosBase, "BuildingBlocks/Contracts", Conjunto(Kernel), Pura: true),
            new($"{Contenido}.Contracts", $"Modules/{Contenido}/Contracts", Conjunto(Kernel, ContratosBase), Pura: true),
            new(Contenido, $"Modules/{Contenido}/{Contenido}", Conjunto($"{Contenido}.Contracts", Kernel, ContratosBase), Pura: false),
            new("UI", "UI", Conjunto([.. contratosDeModulo, ContratosBase, Kernel]), Pura: false),
        ];

        foreach (var modulo in Modulos)
        {
            var m = modulo.Nombre;
            string[] dominio = modulo.TieneDominio ? [$"{m}.Domain"] : [];

            capas.Add(new($"{m}.Contracts", $"Modules/{m}/Contracts", Conjunto(Kernel, ContratosBase), Pura: true));
            if (modulo.TieneDominio)
            {
                capas.Add(new($"{m}.Domain", $"Modules/{m}/Domain", Conjunto(Kernel), Pura: true));
            }

            // Su Domain, su Contracts y los Contracts de otros módulos.
            capas.Add(new($"{m}.Features", $"Modules/{m}/Features",
                Conjunto([.. dominio, .. contratosDeModulo, Kernel, ContratosBase]), Pura: false));
            capas.Add(new($"{m}.Infrastructure", $"Modules/{m}/Infrastructure",
                Conjunto([$"{m}.Features", .. dominio, $"{m}.Contracts", Kernel, ContratosBase]), Pura: false));
        }

        return capas.ToDictionary(c => c.Nombre);
    }

    private static HashSet<string> Conjunto(params string[] nombres) => [.. nombres];
}
