using DraSelina.Consultorio.Pacientes.Domain;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>INV-P1 y RN-30 sobre el agregado Paciente, y el correo como objeto de valor.</summary>
public class PacienteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Registrar_da_un_id_Guid_v7_y_guarda_nombre_telefono_y_correo()
    {
        var paciente = Paciente.Registrar(Telefono(), Nombre(), CorreoElectronico.Crear("ana@correo.com"), Ahora);

        Assert.Equal(7, paciente.Id.Valor.Version);
        Assert.Equal("Ana López", paciente.Nombre.Original);
        Assert.Equal("+524441234567", paciente.Telefono.Valor);
        Assert.Equal("ana@correo.com", paciente.Correo!.Valor);
        Assert.Equal(Ahora, paciente.CreadoEn);
    }

    [Fact]
    public void El_correo_es_opcional()
    {
        var paciente = Paciente.Registrar(Telefono(), Nombre(), correo: null, Ahora);

        Assert.Null(paciente.Correo);
    }

    [Fact]
    public void Un_correo_distinto_reemplaza_al_guardado()
    {
        var paciente = Paciente.Registrar(Telefono(), Nombre(), CorreoElectronico.Crear("ana@correo.com"), Ahora);

        var cambio = paciente.ActualizarCorreo(CorreoElectronico.Crear("ana.nueva@correo.com")!, Ahora.AddDays(1));

        Assert.True(cambio);
        Assert.Equal("ana.nueva@correo.com", paciente.Correo!.Valor);
        Assert.Equal(Ahora.AddDays(1), paciente.ActualizadoEn);
    }

    [Fact]
    public void El_mismo_correo_con_otras_mayusculas_no_cambia_nada()
    {
        var paciente = Paciente.Registrar(Telefono(), Nombre(), CorreoElectronico.Crear("ana@correo.com"), Ahora);

        Assert.False(paciente.ActualizarCorreo(CorreoElectronico.Crear("ANA@Correo.com")!, Ahora.AddDays(1)));
        Assert.Equal("ana@correo.com", paciente.Correo!.Valor);
        Assert.Equal(Ahora, paciente.ActualizadoEn);
    }

    [Fact]
    public void Un_paciente_sin_correo_recibe_el_que_llega()
    {
        var paciente = Paciente.Registrar(Telefono(), Nombre(), correo: null, Ahora);

        Assert.True(paciente.ActualizarCorreo(CorreoElectronico.Crear("ana@correo.com")!, Ahora));
        Assert.Equal("ana@correo.com", paciente.Correo!.Valor);
    }

    [Theory]
    [InlineData("ana@correo.com")]
    [InlineData("  ana.lopez+citas@sub.correo.mx ")]
    public void Un_correo_valido_se_construye_sin_espacios(string texto)
    {
        Assert.Equal(texto.Trim(), CorreoElectronico.Crear(texto)!.Valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ana")]
    [InlineData("ana@")]
    [InlineData("ana@correo")]
    [InlineData("Ana <ana@correo.com>")]
    public void Un_correo_invalido_no_existe(string? texto)
    {
        Assert.Null(CorreoElectronico.Crear(texto));
    }

    private static Telefono Telefono() => Pacientes.Domain.Telefono.DesdeE164("+524441234567")!;

    private static NombreCompleto Nombre() => NombreCompleto.Crear("Ana López")!;
}
