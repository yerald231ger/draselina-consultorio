using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>INV-C2 (rango semiabierto) y AD-17 (hora local de Ciudad de México, guardada en UTC).</summary>
public class RangoYHoraLocalTests
{
    private static readonly DateTimeOffset Cuatro = new(2026, 11, 3, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Un_rango_necesita_inicio_anterior_al_fin()
    {
        Assert.Throws<ArgumentException>(() => RangoDeTiempo.Crear(Cuatro, Cuatro));
        Assert.Throws<ArgumentException>(() => RangoDeTiempo.Crear(Cuatro, Cuatro.AddMinutes(-1)));
    }

    [Fact]
    public void Dos_rangos_contiguos_no_se_traslapan()
    {
        var primero = RangoDeTiempo.Crear(Cuatro, Cuatro.AddMinutes(30));
        var siguiente = RangoDeTiempo.Crear(Cuatro.AddMinutes(30), Cuatro.AddMinutes(60));

        Assert.False(primero.Traslapa(siguiente));
        Assert.False(siguiente.Traslapa(primero));
    }

    [Fact]
    public void Dos_rangos_que_comparten_un_minuto_se_traslapan()
    {
        var primero = RangoDeTiempo.Crear(Cuatro, Cuatro.AddMinutes(30));
        var otro = RangoDeTiempo.Crear(Cuatro.AddMinutes(29), Cuatro.AddMinutes(59));

        Assert.True(primero.Traslapa(otro));
        Assert.True(otro.Traslapa(primero));
    }

    [Fact]
    public void Las_cuatro_de_la_tarde_en_Ciudad_de_Mexico_son_las_22_UTC()
    {
        // Sin horario de verano desde 2022: UTC-6 todo el año.
        Assert.Equal(Cuatro, HoraLocal.AUtc(new DateOnly(2026, 11, 3), new TimeOnly(16, 0)));
        Assert.Equal(
            new DateTimeOffset(2026, 7, 1, 22, 0, 0, TimeSpan.Zero),
            HoraLocal.AUtc(new DateOnly(2026, 7, 1), new TimeOnly(16, 0)));
    }

    [Fact]
    public void De_UTC_a_hora_local_y_de_vuelta()
    {
        var (fecha, hora) = HoraLocal.DesdeUtc(Cuatro);

        Assert.Equal(new DateOnly(2026, 11, 3), fecha);
        Assert.Equal(new TimeOnly(16, 0), hora);
    }
}
