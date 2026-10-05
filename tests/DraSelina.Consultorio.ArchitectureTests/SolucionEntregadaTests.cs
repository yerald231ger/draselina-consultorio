namespace DraSelina.Consultorio.ArchitectureTests;

/// <summary>Las reglas contra lo que hay de verdad en <c>src/</c>.</summary>
public class SolucionEntregadaTests
{
    private static readonly string Src = Lector.CarpetaSrc();
    private static readonly string Web = Lector.CarpetaWeb(Src);

    [Fact]
    public void Los_proyectos_de_src_coinciden_uno_a_uno_con_el_Structural_Seed()
    {
        var esperados = Reglas.Proyectos.Values.Select(fila => fila.Archivo).Order(StringComparer.Ordinal);

        Assert.Equal(esperados, Lector.Proyectos(Src).Select(p => p.Archivo));
    }

    [Fact]
    public void Las_referencias_entre_proyectos_no_tienen_violaciones()
    {
        Assert.Empty(Verificador.VerificarProyectos(Lector.Proyectos(Src)));
    }

    [Fact]
    public void Cada_capa_de_la_arquitectura_tiene_su_carpeta_en_Web()
    {
        var carpetas = Lector.Carpetas(Web);

        Assert.All(Reglas.Capas.Values, capa => Assert.Contains(capa.Carpeta, carpetas));
    }

    [Fact]
    public void El_codigo_de_Web_no_tiene_violaciones()
    {
        var fuentes = Lector.Fuentes(Web);

        Assert.NotEmpty(fuentes);
        Assert.Empty(Verificador.VerificarFuentes(fuentes));
    }
}
