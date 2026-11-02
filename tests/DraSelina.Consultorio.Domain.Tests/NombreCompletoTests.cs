using DraSelina.Consultorio.Pacientes.Domain;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>El nombre guarda su forma original, la normalizada (identidad, RN-25) y la de búsqueda.</summary>
public class NombreCompletoTests
{
    [Fact]
    public void Guarda_el_original_sin_espacios_en_los_extremos()
    {
        var nombre = NombreCompleto.Crear("  José   Pérez Ñúñez ")!;

        Assert.Equal("José   Pérez Ñúñez", nombre.Original);
    }

    [Fact]
    public void El_normalizado_quita_los_espacios_de_mas_y_conserva_mayusculas_y_acentos()
    {
        // Mayúsculas y acentos los ignora la collation de la base, no el código (RN-26).
        var nombre = NombreCompleto.Crear("  José \t  Pérez   Ñúñez ")!;

        Assert.Equal("José Pérez Ñúñez", nombre.Normalizado);
    }

    [Fact]
    public void El_de_busqueda_va_en_minusculas_y_sin_acentos()
    {
        var nombre = NombreCompleto.Crear("José   PÉREZ Ñúñez")!;

        Assert.Equal("jose perez nunez", nombre.Busqueda);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    public void Sin_letras_no_hay_nombre(string? texto)
    {
        Assert.Null(NombreCompleto.Crear(texto));
    }

    [Fact]
    public void Mas_largo_que_el_maximo_no_hay_nombre()
    {
        Assert.Null(NombreCompleto.Crear(new string('a', NombreCompleto.LargoMaximo + 1)));
        Assert.NotNull(NombreCompleto.Crear(new string('a', NombreCompleto.LargoMaximo)));
    }
}
