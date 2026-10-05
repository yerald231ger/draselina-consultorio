namespace DraSelina.Consultorio.ArchitectureTests;

/// <summary>Lo que un <c>.csproj</c> declara y a la regla le importa.</summary>
/// <param name="Nombre">Nombre sin la raíz <c>DraSelina.Consultorio.</c></param>
public sealed record Proyecto(
    string Nombre,
    IReadOnlyList<string> Sdks,
    IReadOnlyList<string> Proyectos,
    IReadOnlyList<string> Paquetes,
    IReadOnlyList<string> Frameworks,
    IReadOnlyList<string> Ensamblados)
{
    public const string SdkBase = "Microsoft.NET.Sdk";

    /// <summary>Un proyecto que solo declara referencias a otros proyectos, sobre el SDK base.</summary>
    public static Proyecto Con(string nombre, params string[] proyectos) =>
        new(nombre, [SdkBase], proyectos, [], [], []);
}

/// <summary>Una referencia que la tabla no permite. Nombra el proyecto y la referencia.</summary>
public sealed record Violacion(string Proyecto, string Referencia, string Regla)
{
    public override string ToString() => $"{Proyecto} -> {Referencia}: {Regla}";
}

/// <summary>
/// Compara proyectos contra <see cref="TablaDeReferencias"/>. Recibe los proyectos como dato para poder
/// probarse con casos sintéticos, sin tocar <c>src/</c>.
/// </summary>
public static class Verificador
{
    public const string ProyectoDesconocido = "proyecto desconocido: la tabla de AD-1 no lo nombra";
    public const string ProyectoNoPermitido = "referencia de proyecto que la tabla de AD-1 no permite";
    public const string PaqueteProhibido = "paquete prohibido: Kernel, contratos y dominio no llevan paquetes (T-28, AD-12)";
    public const string FrameworkProhibido = "framework prohibido: Kernel, contratos y dominio no llevan frameworks (T-28, AD-12)";
    public const string EnsambladoProhibido = "ensamblado prohibido: Kernel, contratos y dominio no llevan referencias externas (T-28, AD-12)";
    public const string SdkProhibido = "SDK prohibido: Kernel, contratos y dominio usan solo Microsoft.NET.Sdk (T-28, AD-12)";

    public static IReadOnlyList<Violacion> Verificar(IEnumerable<Proyecto> proyectos)
    {
        var violaciones = new List<Violacion>();

        foreach (var proyecto in proyectos)
        {
            if (!TablaDeReferencias.Filas.TryGetValue(proyecto.Nombre, out var fila))
            {
                violaciones.Add(new Violacion(proyecto.Nombre, proyecto.Nombre, ProyectoDesconocido));
                continue;
            }

            violaciones.AddRange(proyecto.Proyectos
                .Where(referencia => !fila.PuedeReferenciar.Contains(referencia))
                .Select(referencia => new Violacion(proyecto.Nombre, referencia, ProyectoNoPermitido)));

            if (!fila.SinPaquetesNiFrameworks)
            {
                continue;
            }

            violaciones.AddRange(proyecto.Paquetes.Select(p => new Violacion(proyecto.Nombre, p, PaqueteProhibido)));
            violaciones.AddRange(proyecto.Frameworks.Select(f => new Violacion(proyecto.Nombre, f, FrameworkProhibido)));
            violaciones.AddRange(proyecto.Ensamblados.Select(e => new Violacion(proyecto.Nombre, e, EnsambladoProhibido)));

            if (proyecto.Sdks is not [Proyecto.SdkBase])
            {
                var sdks = proyecto.Sdks.Count == 0 ? "(ninguno)" : string.Join(";", proyecto.Sdks);
                violaciones.Add(new Violacion(proyecto.Nombre, sdks, SdkProhibido));
            }
        }

        return violaciones;
    }
}
