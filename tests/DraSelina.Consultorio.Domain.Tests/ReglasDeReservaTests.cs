using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary><see cref="ReglasDeReserva"/>: anticipación y umbral no negativos; ventana y tope mayores que cero.</summary>
public class ReglasDeReservaTests
{
    [Fact]
    public void Se_crean_con_valores_positivos()
    {
        var rechazo = ReglasDeReserva.Crear(1, 2, 120, 3, out var reglas);

        Assert.Null(rechazo);
        Assert.NotNull(reglas);
        Assert.Equal(1, reglas.AnticipacionMinimaDias);
        Assert.Equal(2, reglas.VentanaMeses);
        Assert.Equal(120, reglas.UmbralCancelacionTardiaMin);
        Assert.Equal(3, reglas.TopeSemanalPorTelefono);
    }

    [Fact]
    public void Cambiar_con_valores_positivos_los_reemplaza()
    {
        var reglas = Sembradas();

        Assert.Null(reglas.Cambiar(2, 3, 60, 5));

        Assert.Equal(2, reglas.AnticipacionMinimaDias);
        Assert.Equal(3, reglas.VentanaMeses);
        Assert.Equal(60, reglas.UmbralCancelacionTardiaMin);
        Assert.Equal(5, reglas.TopeSemanalPorTelefono);
    }

    [Theory]
    [InlineData(-1, 2, 120, 3, RechazoDeReglasDeReserva.AnticipacionMinimaNegativa)]
    [InlineData(1, 0, 120, 3, RechazoDeReglasDeReserva.VentanaNoPositiva)]
    [InlineData(1, -1, 120, 3, RechazoDeReglasDeReserva.VentanaNoPositiva)]
    [InlineData(1, 2, -30, 3, RechazoDeReglasDeReserva.UmbralNegativo)]
    [InlineData(1, 2, -1, 3, RechazoDeReglasDeReserva.UmbralNegativo)]
    [InlineData(1, 2, 120, 0, RechazoDeReglasDeReserva.TopeNoPositivo)]
    public void Cambiar_con_un_valor_fuera_de_rango_se_rechaza_y_nada_cambia(
        int anticipacion, int ventana, int umbral, int tope, RechazoDeReglasDeReserva esperado)
    {
        var reglas = Sembradas();

        Assert.Equal(esperado, reglas.Cambiar(anticipacion, ventana, umbral, tope));

        Assert.Equal(1, reglas.AnticipacionMinimaDias);
        Assert.Equal(2, reglas.VentanaMeses);
        Assert.Equal(120, reglas.UmbralCancelacionTardiaMin);
        Assert.Equal(3, reglas.TopeSemanalPorTelefono);
    }

    [Theory]
    [InlineData(1, 0, 120, 3, RechazoDeReglasDeReserva.VentanaNoPositiva)]
    [InlineData(-1, 2, 120, 3, RechazoDeReglasDeReserva.AnticipacionMinimaNegativa)]
    [InlineData(1, 2, -1, 3, RechazoDeReglasDeReserva.UmbralNegativo)]
    [InlineData(1, 2, 120, 0, RechazoDeReglasDeReserva.TopeNoPositivo)]
    public void Crear_con_un_valor_fuera_de_rango_se_rechaza(int anticipacion, int ventana, int umbral, int tope, RechazoDeReglasDeReserva esperado)
    {
        Assert.Equal(esperado, ReglasDeReserva.Crear(anticipacion, ventana, umbral, tope, out var reglas));
        Assert.Null(reglas);
    }

    [Fact]
    public void Anticipacion_y_umbral_en_cero_son_validos()
    {
        // Reservar el mismo día; ninguna cancelación es tardía.
        Assert.Null(ReglasDeReserva.Crear(0, 2, 0, 3, out var creadas));
        Assert.NotNull(creadas);

        var reglas = Sembradas();
        Assert.Null(reglas.Cambiar(0, 2, 0, 3));
        Assert.Equal(0, reglas.AnticipacionMinimaDias);
        Assert.Equal(0, reglas.UmbralCancelacionTardiaMin);
    }

    private static ReglasDeReserva Sembradas()
    {
        ReglasDeReserva.Crear(1, 2, 120, 3, out var reglas);
        return reglas!;
    }
}
