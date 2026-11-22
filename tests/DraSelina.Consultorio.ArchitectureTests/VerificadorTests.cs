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
            using Microsoft.Extensions.Logging;
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
            using Microsoft.AspNetCore.Builder;
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
    public void Un_using_implicito_fuera_de_System_es_violacion()
    {
        const string ruta = "obj/Debug/net11.0/DraSelina.Consultorio.Web.GlobalUsings.g.cs";

        // Lo que el SDK web genera si no se le quitan sus using implícitos.
        var violaciones = Verificador.VerificarUsingsImplicitos([Lector.LeerFuente(ruta, """
            // <auto-generated/>
            global using System;
            global using System.Net.Http.Json;
            global using Microsoft.AspNetCore.Http;
            global using Microsoft.Extensions.Logging;
            """)]);

        Assert.Equal(
            [
                new Violacion(ruta, "Microsoft.AspNetCore.Http", Verificador.UsingImplicitoProhibido),
                new Violacion(ruta, "Microsoft.Extensions.Logging", Verificador.UsingImplicitoProhibido),
            ],
            violaciones);

        Assert.Empty(Verificador.VerificarUsingsImplicitos([Lector.LeerFuente(ruta, """
            global using System;
            global using System.Linq;
            """)]));
    }

    [Fact]
    public void Un_componente_de_UI_con_el_namespace_de_otra_capa_es_violacion()
    {
        var violacion = Assert.Single(Verificar("UI/Pages/Agendar.razor", """
            @page "/agendar"
            @namespace DraSelina.Consultorio.Agenda.Domain
            """));

        Assert.Equal(
            new Violacion("UI/Pages/Agendar.razor", "DraSelina.Consultorio.Agenda.Domain", Verificador.NamespaceFueraDeSuCarpeta),
            violacion);

        Assert.Empty(Verificar("UI/Pages/Agendar.razor", """
            @page "/agendar"
            @namespace DraSelina.Consultorio.UI.Pages
            """));
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

    [Fact]
    public void Una_pagina_bajo_admin_sin_Authorize_es_violacion()
    {
        var violacion = Assert.Single(Verificador.VerificarPaginasDeAdministracion([("UI/Pages/Administracion/Pacientes.razor", """
            @page "/admin/pacientes"
            @rendermode InteractiveServer
            """)]));

        Assert.Equal(
            new Violacion("UI/Pages/Administracion/Pacientes.razor", "/admin/pacientes", Verificador.AdministracionSinAuthorize),
            violacion);
    }

    [Fact]
    public void Una_pagina_de_admin_con_Authorize_no_es_violacion_ni_las_de_entrar_ni_las_publicas()
    {
        Assert.Empty(Verificador.VerificarPaginasDeAdministracion(
        [
            ("UI/Pages/Administracion/Agenda.razor", """
                @page "/admin"
                @attribute [Authorize]
                """),
            ("UI/Pages/Administracion/Detalle.razor", """
                @page "/admin/citas/{Id:guid}"
                @attribute [Microsoft.AspNetCore.Authorization.Authorize(Policy = "x")]
                """),
            ("Identidad/Paginas/Entrar.razor", """@page "/admin/entrar" """),
            ("Identidad/Paginas/Activar.razor", """@page "/admin/entrar/activar" """),
            ("UI/Pages/Inicio.razor", """@page "/" """),
            ("UI/Pages/Administrador.razor", """@page "/administrador" """),
        ]));
    }

    [Fact]
    public void La_raiz_admin_sin_Authorize_es_violacion_aunque_otra_ruta_de_la_pagina_sea_publica()
    {
        var violacion = Assert.Single(Verificador.VerificarPaginasDeAdministracion([("UI/Pages/Dos.razor", """
            @page "/publica"
            @page "/admin"
            @attribute [EnableRateLimiting("x")]
            """)]));

        Assert.Equal("/admin", violacion.Referencia);
    }

    [Fact]
    public void Un_caso_de_uso_que_nombra_un_tipo_de_otro_es_violacion()
    {
        var violacion = Assert.Single(Verificador.VerificarCasosDeUso(
        [
            ("Modules/Agenda/Features/SemanaTipo/SemanaTipoHandler.cs", "public sealed class SemanaTipoHandler { }"),
            ("Modules/Agenda/Features/SemanaDeAtencion/SemanaDeAtencionHandler.cs", """
                public sealed class SemanaDeAtencionHandler(SemanaTipoHandler otro) { }
                """),
        ]));

        Assert.Equal(
            new Violacion(
                "Modules/Agenda/Features/SemanaDeAtencion/SemanaDeAtencionHandler.cs",
                "SemanaTipo/SemanaTipoHandler",
                Verificador.CasoDeUsoAjeno),
            violacion);
    }

    [Fact]
    public void Compartido_tampoco_puede_nombrar_el_tipo_de_un_caso_de_uso()
    {
        var violacion = Assert.Single(Verificador.VerificarCasosDeUso(
        [
            ("Modules/Agenda/Features/AgendaApi/AgendaApi.cs", "public sealed class AgendaApi { }"),
            ("Modules/Agenda/Features/Compartido/Ayuda.cs", "internal static class Ayuda { static AgendaApi? api; }"),
        ]));

        Assert.Equal("Modules/Agenda/Features/Compartido/Ayuda.cs", violacion.Donde);
    }

    [Fact]
    public void Usar_Compartido_comentarios_cadenas_lo_propio_otro_modulo_o_fuera_de_Features_no_es_violacion()
    {
        Assert.Empty(Verificador.VerificarCasosDeUso(
        [
            ("Modules/Agenda/Features/Compartido/ITramos.cs", "public interface ITramos { }"),
            ("Modules/Agenda/Features/SemanaTipo/SemanaTipoHandler.cs", """
                /// <see cref="SemanaDeAtencionHandler"/> lee lo mismo.
                public sealed class SemanaTipoHandler(ITramos tramos) { string n = "SemanaDeAtencionHandler"; private sealed record Fila; }
                """),
            ("Modules/Agenda/Features/SemanaDeAtencion/SemanaDeAtencionHandler.cs", """
                public sealed class SemanaDeAtencionHandler(ITramos tramos) { private sealed record Fila; } // SemanaTipoHandler
                """),
            ("Modules/Agenda/Features/Compartido/Plantilla.cs", "public static class Plantilla { public sealed record Datos; }"),
            ("Modules/Agenda/Features/AvisoAlCancelar/PlantillaDeAviso.cs", "public static class PlantillaDeAviso { public sealed record Datos; }"),
            ("Modules/Agenda/Features/Confirmacion/Confirmacion.cs", "class Confirmacion { object d = new Plantilla.Datos(); }"),
            ("Modules/Notificaciones/Features/Aviso/Aviso.cs", "public sealed class Aviso(SemanaTipoHandler h) { }"),
            ("Modules/Agenda/Infrastructure/Registro.cs", "class Registro { SemanaTipoHandler a; SemanaDeAtencionHandler b; }"),
        ]));
    }
}
