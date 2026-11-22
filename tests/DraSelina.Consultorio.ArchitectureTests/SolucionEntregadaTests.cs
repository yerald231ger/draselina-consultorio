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

    [Fact]
    public void Features_solo_tiene_carpetas_de_caso_de_uso_y_Compartido()
    {
        // Modules/<Módulo>/Features/<Archivo>.cs: un archivo suelto en la raíz de Features, fuera de todo slice.
        var enFeatures = Lector.Fuentes(Web)
            .Select(fuente => fuente.Ruta.Split('/'))
            .Where(partes => partes is ["Modules", _, "Features", ..])
            .ToList();

        // Si la regla no viera los slices, no estaría verificando nada.
        Assert.Contains(enFeatures, partes => partes is ["Modules", "Agenda", "Features", "Compartido", _]);
        Assert.Empty(enFeatures.Where(partes => partes.Length == 4).Select(partes => string.Join('/', partes)));
    }

    [Fact]
    public void Ningun_caso_de_uso_usa_codigo_de_la_carpeta_de_otro()
    {
        var codigo = Lector.CodigoDeFeatures(Web);

        // Si la regla no viera los slices de Agenda, no estaría verificando nada.
        Assert.Contains(codigo, archivo => archivo.Ruta == "Modules/Agenda/Features/AgendaApi/AgendaApi.cs");
        Assert.Empty(Verificador.VerificarCasosDeUso(codigo));
    }

    [Fact]
    public void Toda_pagina_de_administracion_salvo_entrar_lleva_Authorize()
    {
        var componentes = Lector.Componentes(Web);

        // La Agenda es de administración: si la regla no la viera, no estaría verificando nada.
        Assert.Contains(componentes, c => c.Ruta == "UI/Pages/Administracion/Agenda.razor");
        Assert.Empty(Verificador.VerificarPaginasDeAdministracion(componentes));
    }

    [Fact]
    public void Web_no_tiene_using_implicitos_fuera_de_System()
    {
        var generados = Lector.UsingsImplicitos(Web);

        Assert.True(generados.Count > 0, "No hay GlobalUsings.g.cs en obj/ de Web: compila la solución antes de correr esta prueba.");
        Assert.Empty(Verificador.VerificarUsingsImplicitos(generados));
    }

    [Fact]
    public void Los_componentes_de_UI_declaran_el_namespace_de_la_interfaz()
    {
        // Sin este @namespace los .razor de UI/ caerían en DraSelina.Consultorio.Web.UI, el namespace por carpeta.
        var imports = Assert.Single(Lector.Fuentes(Web), fuente => fuente.Ruta == "UI/_Imports.razor");

        Assert.Equal("DraSelina.Consultorio.UI", imports.Namespace);
    }
}
