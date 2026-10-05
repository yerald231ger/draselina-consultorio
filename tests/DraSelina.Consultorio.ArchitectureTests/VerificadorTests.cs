namespace DraSelina.Consultorio.ArchitectureTests;

/// <summary>Una prueba por fila de la matriz del plan, con proyectos sintéticos.</summary>
public class VerificadorTests
{
    [Fact]
    public void Un_Domain_con_un_paquete_es_violacion_que_nombra_el_proyecto_y_la_referencia()
    {
        var proyecto = Proyecto.Con("Agenda.Domain", "Kernel") with { Paquetes = ["Microsoft.EntityFrameworkCore"] };

        var violacion = Assert.Single(Verificador.Verificar([proyecto]));

        Assert.Equal(new Violacion("Agenda.Domain", "Microsoft.EntityFrameworkCore", Verificador.PaqueteProhibido), violacion);
        Assert.Contains("Agenda.Domain", violacion.ToString());
        Assert.Contains("Microsoft.EntityFrameworkCore", violacion.ToString());
    }

    [Fact]
    public void Un_Domain_que_referencia_los_Contracts_de_otro_modulo_es_violacion()
    {
        var proyecto = Proyecto.Con("Agenda.Domain", "Kernel", "Pacientes.Contracts");

        var violacion = Assert.Single(Verificador.Verificar([proyecto]));

        Assert.Equal(new Violacion("Agenda.Domain", "Pacientes.Contracts", Verificador.ProyectoNoPermitido), violacion);
    }

    [Fact]
    public void La_interfaz_que_referencia_un_Domain_es_violacion()
    {
        var proyecto = Proyecto.Con("UI", "Agenda.Contracts", "Agenda.Domain");

        var violacion = Assert.Single(Verificador.Verificar([proyecto]));

        Assert.Equal(new Violacion("UI", "Agenda.Domain", Verificador.ProyectoNoPermitido), violacion);
    }

    [Fact]
    public void Features_puede_referenciar_los_Contracts_de_otro_modulo()
    {
        var proyecto = Proyecto.Con("Agenda.Features", "Agenda.Domain", "Agenda.Contracts", "Pacientes.Contracts");

        Assert.Empty(Verificador.Verificar([proyecto]));
    }

    [Fact]
    public void Un_proyecto_que_la_tabla_no_nombra_es_violacion()
    {
        var proyecto = Proyecto.Con("Facturacion.Domain", "Kernel");

        var violacion = Assert.Single(Verificador.Verificar([proyecto]));

        Assert.Equal(new Violacion("Facturacion.Domain", "Facturacion.Domain", Verificador.ProyectoDesconocido), violacion);
    }

    [Fact]
    public void Features_no_puede_referenciar_el_Domain_ni_las_Features_de_otro_modulo()
    {
        var proyecto = Proyecto.Con("Agenda.Features", "Pacientes.Domain", "Pacientes.Features");

        var referencias = Verificador.Verificar([proyecto]).Select(v => v.Referencia);

        Assert.Equal(["Pacientes.Domain", "Pacientes.Features"], referencias);
    }

    [Fact]
    public void El_Kernel_no_referencia_nada()
    {
        var proyecto = Proyecto.Con("Kernel", "BuildingBlocks.Contracts");

        var violacion = Assert.Single(Verificador.Verificar([proyecto]));

        Assert.Equal("BuildingBlocks.Contracts", violacion.Referencia);
    }

    [Fact]
    public void Un_Contracts_con_framework_ensamblado_u_otro_SDK_es_violacion()
    {
        var proyecto = Proyecto.Con("Agenda.Contracts", "Kernel", "BuildingBlocks.Contracts") with
        {
            Sdks = ["Microsoft.NET.Sdk.Web"],
            Frameworks = ["Microsoft.AspNetCore.App"],
            Ensamblados = ["Algo.dll"],
        };

        var reglas = Verificador.Verificar([proyecto]).Select(v => (v.Referencia, v.Regla));

        Assert.Equal(
            [
                ("Microsoft.AspNetCore.App", Verificador.FrameworkProhibido),
                ("Algo.dll", Verificador.EnsambladoProhibido),
                ("Microsoft.NET.Sdk.Web", Verificador.SdkProhibido),
            ],
            reglas);
    }

    [Fact]
    public void Fuera_de_Kernel_contratos_y_dominio_los_paquetes_no_son_asunto_de_esta_regla()
    {
        var proyecto = Proyecto.Con("Agenda.Infrastructure", "Agenda.Features") with
        {
            Paquetes = ["Microsoft.EntityFrameworkCore"],
        };

        Assert.Empty(Verificador.Verificar([proyecto]));
    }
}
