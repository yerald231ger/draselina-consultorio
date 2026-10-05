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
public static class Verificador
{
    public const string ProyectoDesconocido = "proyecto desconocido: la arquitectura no lo nombra";
    public const string ProyectoNoPermitido = "referencia de proyecto que la tabla de la arquitectura no permite";
    public const string CapaDesconocida = "carpeta fuera de las capas de la arquitectura";
    public const string NamespaceFueraDeSuCarpeta = "el namespace no corresponde a la carpeta de la capa";
    public const string CapaNoPermitida = "dependencia entre namespaces que la tabla de AD-1 no permite";
    public const string PaqueteProhibido = "paquete o framework prohibido: Kernel, contratos y dominio solo usan la biblioteca base (T-28, AD-12)";
    public const string GlobalUsingProhibido = "global using prohibido: llegaría a todas las capas sin que esta prueba lo vea";

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
