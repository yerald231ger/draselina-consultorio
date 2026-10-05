namespace DraSelina.Consultorio.ArchitectureTests;

/// <summary>La regla contra los proyectos reales de <c>src/</c>.</summary>
public class SolucionEntregadaTests
{
    private static readonly string Src = LectorDeProyectos.CarpetaSrc();

    [Fact]
    public void Los_proyectos_reales_de_src_no_tienen_violaciones()
    {
        var proyectos = LectorDeProyectos.Leer(Src);

        Assert.NotEmpty(proyectos);
        Assert.Empty(Verificador.Verificar(proyectos));
    }

    [Fact]
    public void Los_proyectos_de_src_coinciden_uno_a_uno_con_el_Structural_Seed()
    {
        var esperados = TablaDeReferencias.Filas.Values
            .Select(fila => $"{fila.Carpeta}/{TablaDeReferencias.Raiz}{fila.Proyecto}.csproj")
            .Order(StringComparer.Ordinal);

        var reales = LectorDeProyectos.Archivos(Src)
            .Select(ruta => Path.GetRelativePath(Src, ruta).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);

        Assert.Equal(esperados, reales);
    }

    [Fact]
    public void Ningun_archivo_compartido_de_MSBuild_declara_referencias()
    {
        var archivos = LectorDeProyectos.ArchivosCompartidos(Src);
        var raiz = Path.GetDirectoryName(Src)!;

        var declaradas = archivos.SelectMany(archivo => LectorDeProyectos
            .ReferenciasCompartidas(File.ReadAllText(archivo))
            .Select(referencia => $"{Path.GetRelativePath(raiz, archivo).Replace('\\', '/')} -> {referencia}"));

        Assert.Contains(archivos, archivo => Path.GetFileName(archivo) == "Directory.Build.props");
        Assert.Empty(declaradas);
    }
}
