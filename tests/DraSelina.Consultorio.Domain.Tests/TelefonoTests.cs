using DraSelina.Consultorio.Pacientes.Domain;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>INV-P2: el teléfono solo existe en E.164 de México.</summary>
public class TelefonoTests
{
    [Theory]
    [InlineData("+524441234567")]
    [InlineData("+525512345678")]
    public void Se_construye_con_un_numero_de_Mexico_en_E164(string e164)
    {
        var telefono = Telefono.DesdeE164(e164);

        Assert.NotNull(telefono);
        Assert.Equal(e164, telefono.Valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("4441234567")]
    [InlineData("+52 444 123 4567")]
    [InlineData("+5214441234567")]
    [InlineData("+520441234567")]
    [InlineData("+524441234")]
    [InlineData("+14155552671")]
    public void No_existe_con_otra_forma(string? texto)
    {
        Assert.Null(Telefono.DesdeE164(texto));
    }

    [Fact]
    public void Dos_telefonos_con_el_mismo_numero_son_iguales()
    {
        Assert.Equal(Telefono.DesdeE164("+524441234567"), Telefono.DesdeE164("+524441234567"));
    }
}
