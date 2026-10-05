namespace DraSelina.Consultorio.ArchitectureTests;

/// <summary>Las reglas con casos sintéticos: cada archivo se da como texto, sin tocar <c>src/</c>.</summary>
public class VerificadorTests
{
    private static IReadOnlyList<Violacion> Verificar(string ruta, string texto) =>
        Verificador.VerificarFuentes([Lector.LeerFuente(ruta, texto)]);

    [Fact]
    public void Un_Domain_que_usa_EF_Core_es_violacion_que_nombra_el_archivo_y_la_referencia()
    {
        var violacion = Assert.Single(Verificar("Modules/Agenda/Domain/Cita.cs", """
            using Microsoft.EntityFrameworkCore;
            using DraSelina.Consultorio.Kernel;

            namespace DraSelina.Consultorio.Agenda.Domain;

            public sealed class Cita;
            """));

        Assert.Equal(
            new Violacion("Modules/Agenda/Domain/Cita.cs", "Microsoft.EntityFrameworkCore", Verificador.PaqueteProhibido),
            violacion);
        Assert.Contains("Modules/Agenda/Domain/Cita.cs", violacion.ToString());
        Assert.Contains("Microsoft.EntityFrameworkCore", violacion.ToString());
    }

    [Fact]
    public void Un_Domain_que_usa_los_Contracts_de_otro_modulo_es_violacion()
    {
        var violacion = Assert.Single(Verificar("Modules/Agenda/Domain/Cita.cs", """
            using DraSelina.Consultorio.Pacientes.Contracts;

            namespace DraSelina.Consultorio.Agenda.Domain;
            """));

        Assert.Equal(("DraSelina.Consultorio.Pacientes.Contracts", Verificador.CapaNoPermitida), (violacion.Referencia, violacion.Regla));
    }

    [Fact]
    public void La_interfaz_que_usa_un_Domain_es_violacion_tambien_en_un_razor()
    {
        var violacion = Assert.Single(Verificar("UI/Pages/Agendar.razor", """
            @page "/agendar"
            @using DraSelina.Consultorio.Agenda.Contracts
            @using DraSelina.Consultorio.Agenda.Domain
            """));

        Assert.Equal(("DraSelina.Consultorio.Agenda.Domain", Verificador.CapaNoPermitida), (violacion.Referencia, violacion.Regla));
    }

    [Fact]
    public void Features_puede_usar_los_Contracts_de_otro_modulo_y_cualquier_paquete()
    {
        Assert.Empty(Verificar("Modules/Agenda/Features/Agendar/AgendarHandler.cs", """
            using Wolverine;
            using DraSelina.Consultorio.Kernel;
            using DraSelina.Consultorio.Agenda.Domain;
            using DraSelina.Consultorio.Agenda.Contracts;
            using DraSelina.Consultorio.Pacientes.Contracts;

            namespace DraSelina.Consultorio.Agenda.Features.Agendar;
            """));
    }

    [Fact]
    public void Features_no_puede_usar_el_Domain_ni_las_Features_de_otro_modulo()
    {
        var referencias = Verificar("Modules/Agenda/Features/Agendar/AgendarHandler.cs", """
            using DraSelina.Consultorio.Pacientes.Domain;
            using DraSelina.Consultorio.Pacientes.Features;

            namespace DraSelina.Consultorio.Agenda.Features.Agendar;
            """).Select(v => v.Referencia);

        Assert.Equal(["DraSelina.Consultorio.Pacientes.Domain", "DraSelina.Consultorio.Pacientes.Features"], referencias);
    }

    [Fact]
    public void Un_nombre_escrito_completo_cuenta_igual_que_un_using()
    {
        var violacion = Assert.Single(Verificar("Modules/Agenda/Domain/Cita.cs", """
            namespace DraSelina.Consultorio.Agenda.Domain;

            public sealed class Cita
            {
                private DraSelina.Consultorio.Agenda.Infrastructure.AgendaDbContext? _db;
            }
            """));

        Assert.Equal("DraSelina.Consultorio.Agenda.Infrastructure.AgendaDbContext", violacion.Referencia);
    }

    [Fact]
    public void Ninguna_capa_puede_usar_el_host()
    {
        var violacion = Assert.Single(Verificar("Modules/Agenda/Infrastructure/AgendaDbContext.cs", """
            using DraSelina.Consultorio.Web;

            namespace DraSelina.Consultorio.Agenda.Infrastructure;
            """));

        Assert.Equal(Verificador.CapaNoPermitida, violacion.Regla);
    }

    [Fact]
    public void El_Kernel_no_usa_nada_salvo_la_biblioteca_base()
    {
        Assert.Empty(Verificar("Kernel/Resultado.cs", """
            using System.Collections.ObjectModel;

            namespace DraSelina.Consultorio.Kernel;
            """));

        var violacion = Assert.Single(Verificar("Kernel/Resultado.cs", """
            using DraSelina.Consultorio.BuildingBlocks.Contracts;

            namespace DraSelina.Consultorio.Kernel;
            """));

        Assert.Equal(Verificador.CapaNoPermitida, violacion.Regla);
    }

    [Fact]
    public void Un_Contracts_que_usa_ASP_NET_es_violacion()
    {
        var violacion = Assert.Single(Verificar("Modules/Agenda/Contracts/ConfirmarReserva.cs", """
            using System.ComponentModel.DataAnnotations;
            using Microsoft.AspNetCore.Mvc;

            namespace DraSelina.Consultorio.Agenda.Contracts;
            """));

        Assert.Equal(("Microsoft.AspNetCore.Mvc", Verificador.PaqueteProhibido), (violacion.Referencia, violacion.Regla));
    }

    [Fact]
    public void El_namespace_debe_corresponder_a_la_carpeta()
    {
        var violacion = Assert.Single(Verificar("Modules/Agenda/Domain/Cita.cs", """
            namespace DraSelina.Consultorio.Agenda.Features;
            """));

        Assert.Equal(("DraSelina.Consultorio.Agenda.Features", Verificador.NamespaceFueraDeSuCarpeta), (violacion.Referencia, violacion.Regla));
    }

    [Fact]
    public void Una_carpeta_de_modulo_que_la_arquitectura_no_nombra_es_violacion()
    {
        var violacion = Assert.Single(Verificar("Modules/Facturacion/Domain/Factura.cs", """
            namespace DraSelina.Consultorio.Facturacion.Domain;
            """));

        Assert.Equal(Verificador.CapaDesconocida, violacion.Regla);
    }

    [Fact]
    public void El_host_compone_y_puede_usar_todo()
    {
        Assert.Empty(Verificar("Program.cs", """
            using Wolverine;
            using DraSelina.Consultorio.Agenda.Infrastructure;
            using DraSelina.Consultorio.UI;
            """));
    }

    [Fact]
    public void Un_global_using_es_violacion_en_cualquier_archivo()
    {
        var violacion = Assert.Single(Verificar("GlobalUsings.cs", """
            global using Microsoft.EntityFrameworkCore;
            """));

        Assert.Equal(Verificador.GlobalUsingProhibido, violacion.Regla);
    }

    [Fact]
    public void Un_using_de_recurso_o_un_comentario_no_cuentan_como_dependencia()
    {
        Assert.Empty(Verificar("Modules/Agenda/Domain/Cita.cs", """
            namespace DraSelina.Consultorio.Agenda.Domain;

            // No conoce a DraSelina.Consultorio.Agenda.Infrastructure ni a Microsoft.EntityFrameworkCore.
            public sealed class Cita
            {
                public void Leer()
                {
                    using var flujo = new System.IO.MemoryStream();
                }
            }
            """));
    }

    [Fact]
    public void Un_proyecto_que_la_arquitectura_no_nombra_es_violacion()
    {
        var proyecto = Lector.LeerProyecto("Modules/Agenda/Domain/DraSelina.Consultorio.Agenda.Domain.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");

        var violacion = Assert.Single(Verificador.VerificarProyectos([proyecto]));

        Assert.Equal(("Agenda.Domain", Verificador.ProyectoDesconocido), (violacion.Referencia, violacion.Regla));
    }

    [Fact]
    public void Web_no_puede_referenciar_al_MigrationService_y_el_MigrationService_si_a_Web()
    {
        static Proyecto Con(string nombre, string referencia) => Lector.LeerProyecto(
            $"DraSelina.Consultorio.{nombre}/DraSelina.Consultorio.{nombre}.csproj",
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\DraSelina.Consultorio.{referencia}\DraSelina.Consultorio.{referencia}.csproj" />
              </ItemGroup>
            </Project>
            """);

        Assert.Empty(Verificador.VerificarProyectos([Con("MigrationService", "Web")]));

        var violacion = Assert.Single(Verificador.VerificarProyectos([Con("Web", "MigrationService")]));
        Assert.Equal(new Violacion("Web", "MigrationService", Verificador.ProyectoNoPermitido), violacion);
    }
}
