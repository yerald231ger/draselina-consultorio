namespace DraSelina.Consultorio.ArchitectureTests;

/// <summary>Una fila de la tabla: un proyecto de <c>src/</c>, dónde vive y qué puede referenciar.</summary>
/// <param name="Proyecto">Nombre sin la raíz <c>DraSelina.Consultorio.</c></param>
/// <param name="Carpeta">Carpeta del Structural Seed, relativa a <c>src/</c>.</param>
/// <param name="PuedeReferenciar">Proyectos que puede referenciar de forma directa. Lo que no está, está prohibido.</param>
/// <param name="SinPaquetesNiFrameworks">Kernel, contratos y dominio: ningún paquete ni framework (T-28, AD-12).</param>
public sealed record Fila(
    string Proyecto,
    string Carpeta,
    IReadOnlySet<string> PuedeReferenciar,
    bool SinPaquetesNiFrameworks);

/// <summary>
/// La tabla "Puede referenciar" de la arquitectura (Invariants &amp; Rules, AD-1) y el Structural Seed, como datos.
/// Las filas de AppHost y ServiceDefaults no están en la tabla escrita: AppHost sale del diagrama
/// (AppHost → Web, MigrationService) y ServiceDefaults no referencia nada.
/// </summary>
public static class TablaDeReferencias
{
    public const string Raiz = "DraSelina.Consultorio.";

    private const string Kernel = "Kernel";
    private const string ContratosBase = "BuildingBlocks.Contracts";

    private sealed record Modulo(string Nombre, bool TieneDominio);

    /// <summary>Módulos con Features e Infrastructure. Notificaciones no tiene dominio.</summary>
    private static readonly Modulo[] Modulos =
    [
        new("Agenda", TieneDominio: true),
        new("Pacientes", TieneDominio: true),
        new("Notificaciones", TieneDominio: false),
    ];

    /// <summary>Contenido es Contracts más un solo proyecto de implementación.</summary>
    private const string Contenido = "Contenido";

    public static IReadOnlyDictionary<string, Fila> Filas { get; } = Construir();

    private static Dictionary<string, Fila> Construir()
    {
        string[] contratosDeModulo = [.. Modulos.Select(m => $"{m.Nombre}.Contracts"), $"{Contenido}.Contracts"];
        string[] todosLosContratos = [ContratosBase, .. contratosDeModulo];
        string[] infraestructuras = [.. Modulos.Select(m => $"{m.Nombre}.Infrastructure")];

        List<Fila> filas =
        [
            Pura(Kernel, "BuildingBlocks/Kernel"),
            Pura(ContratosBase, "BuildingBlocks/Contracts", Kernel),
            Pura($"{Contenido}.Contracts", $"Modules/{Contenido}/Contracts", Kernel, ContratosBase),
            Normal(Contenido, $"Modules/{Contenido}/{Contenido}", $"{Contenido}.Contracts"),
            Normal("UI", "Consultorio.UI", [.. todosLosContratos, Kernel]),
            Normal("MigrationService", "Consultorio.MigrationService", infraestructuras),
            Normal("AppHost", "Consultorio.AppHost", "Web", "MigrationService"),
            Normal("ServiceDefaults", "Consultorio.ServiceDefaults"),
        ];

        foreach (var modulo in Modulos)
        {
            var m = modulo.Nombre;
            string[] dominio = modulo.TieneDominio ? [$"{m}.Domain"] : [];

            filas.Add(Pura($"{m}.Contracts", $"Modules/{m}/Contracts", Kernel, ContratosBase));
            if (modulo.TieneDominio)
            {
                filas.Add(Pura($"{m}.Domain", $"Modules/{m}/Domain", Kernel));
            }

            // Su Domain, su Contracts y los Contracts de otros módulos.
            filas.Add(Normal($"{m}.Features", $"Modules/{m}/Features", [.. dominio, .. contratosDeModulo]));
            filas.Add(Normal($"{m}.Infrastructure", $"Modules/{m}/Infrastructure", [$"{m}.Features", .. dominio, $"{m}.Contracts"]));
        }

        // Consultorio.Web compone: puede referenciar todo lo demás.
        filas.Add(Normal("Web", "Consultorio.Web", [.. filas.Select(f => f.Proyecto)]));

        return filas.ToDictionary(f => f.Proyecto);
    }

    private static Fila Pura(string proyecto, string carpeta, params string[] puedeReferenciar) =>
        new(proyecto, carpeta, puedeReferenciar.ToHashSet(), SinPaquetesNiFrameworks: true);

    private static Fila Normal(string proyecto, string carpeta, params string[] puedeReferenciar) =>
        new(proyecto, carpeta, puedeReferenciar.ToHashSet(), SinPaquetesNiFrameworks: false);
}
