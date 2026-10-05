namespace DraSelina.Consultorio.ArchitectureTests;

public class LectorDeProyectosTests
{
    [Fact]
    public void Lee_SDK_proyectos_paquetes_frameworks_y_ensamblados_de_un_csproj()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\..\..\BuildingBlocks\Kernel\DraSelina.Consultorio.Kernel.csproj" />
                <ProjectReference Include="../../Pacientes/Contracts/DraSelina.Consultorio.Pacientes.Contracts.csproj" />
              </ItemGroup>
              <ItemGroup Condition="'$(Configuration)' == 'Debug'">
                <PackageReference Include="Microsoft.EntityFrameworkCore" />
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <Reference Include="Algo.dll" />
              </ItemGroup>
            </Project>
            """;

        var proyecto = LectorDeProyectos.LeerProyecto("src/Modules/Agenda/Domain/DraSelina.Consultorio.Agenda.Domain.csproj", xml);

        Assert.Equal("Agenda.Domain", proyecto.Nombre);
        Assert.Equal(["Microsoft.NET.Sdk"], proyecto.Sdks);
        Assert.Equal(["Kernel", "Pacientes.Contracts"], proyecto.Proyectos);
        Assert.Equal(["Microsoft.EntityFrameworkCore"], proyecto.Paquetes);
        Assert.Equal(["Microsoft.AspNetCore.App"], proyecto.Frameworks);
        Assert.Equal(["Algo.dll"], proyecto.Ensamblados);
    }

    [Fact]
    public void Lo_leido_de_un_Domain_con_EF_Core_es_violacion()
    {
        const string xml = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore" />
              </ItemGroup>
            </Project>
            """;

        var proyecto = LectorDeProyectos.LeerProyecto("DraSelina.Consultorio.Agenda.Domain.csproj", xml);

        var violacion = Assert.Single(Verificador.Verificar([proyecto]));
        Assert.Equal(("Agenda.Domain", "Microsoft.EntityFrameworkCore"), (violacion.Proyecto, violacion.Referencia));
    }

    [Fact]
    public void Un_csproj_sin_la_raiz_de_nombres_conserva_su_nombre_y_queda_fuera_de_la_tabla()
    {
        var proyecto = LectorDeProyectos.LeerProyecto("src/Otro/Otro.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");

        Assert.Equal("Otro", proyecto.Nombre);
        Assert.Equal(Verificador.ProyectoDesconocido, Assert.Single(Verificador.Verificar([proyecto])).Regla);
    }

    [Fact]
    public void De_un_archivo_compartido_cuentan_las_referencias_y_no_las_versiones()
    {
        const string xml = """
            <Project>
              <ItemGroup>
                <PackageVersion Include="xunit.v3" Version="4.0.1" />
                <PackageReference Include="Microsoft.EntityFrameworkCore" />
                <GlobalPackageReference Include="Un.Analizador" Version="1.0.0" />
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
                <Reference Include="Algo.dll" />
              </ItemGroup>
            </Project>
            """;

        Assert.Equal(
            [
                "PackageReference Microsoft.EntityFrameworkCore",
                "GlobalPackageReference Un.Analizador",
                "FrameworkReference Microsoft.AspNetCore.App",
                "Reference Algo.dll",
            ],
            LectorDeProyectos.ReferenciasCompartidas(xml));
    }
}
