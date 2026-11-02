using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary><see cref="Tramo"/>: inicio anterior al fin (INV-B1, INV-A2), encimarse y contener.</summary>
public class TramoTests
{
    private static Tramo T(int hi, int mi, int hf, int mf) => Tramo.Crear(new TimeOnly(hi, mi), new TimeOnly(hf, mf));

    [Fact]
    public void Un_tramo_necesita_inicio_anterior_al_fin()
    {
        Assert.Throws<ArgumentException>(() => T(16, 0, 16, 0));
        Assert.Throws<ArgumentException>(() => T(18, 0, 15, 30));
    }

    [Fact]
    public void Un_tramo_sabe_su_duracion()
    {
        Assert.Equal(TimeSpan.FromMinutes(150), T(15, 30, 18, 0).Duracion);
    }

    [Fact]
    public void Dos_tramos_que_comparten_minutos_se_enciman()
    {
        var primero = T(15, 30, 17, 0);
        var segundo = T(16, 30, 18, 0);

        Assert.True(primero.SeEncimaCon(segundo));
        Assert.True(segundo.SeEncimaCon(primero));
        Assert.True(T(15, 30, 18, 0).SeEncimaCon(T(16, 0, 17, 0)));
    }

    [Fact]
    public void Dos_tramos_contiguos_no_se_enciman()
    {
        Assert.False(T(15, 30, 16, 30).SeEncimaCon(T(16, 30, 18, 0)));
        Assert.False(T(16, 30, 18, 0).SeEncimaCon(T(15, 30, 16, 30)));
    }

    [Fact]
    public void Un_tramo_contiene_un_periodo_que_cabe_completo_incluidos_sus_bordes()
    {
        var tramo = T(15, 30, 18, 0);

        Assert.True(tramo.Contiene(new TimeOnly(15, 30), new TimeOnly(16, 0)));
        Assert.True(tramo.Contiene(new TimeOnly(17, 30), new TimeOnly(18, 0)));
        Assert.False(tramo.Contiene(new TimeOnly(17, 45), new TimeOnly(18, 15)));
        Assert.False(tramo.Contiene(new TimeOnly(15, 0), new TimeOnly(15, 45)));
        Assert.False(tramo.Contiene(new TimeOnly(16, 0), new TimeOnly(16, 0)));
    }

    [Fact]
    public void Dos_tramos_con_las_mismas_horas_son_iguales()
    {
        Assert.Equal(T(15, 30, 18, 0), T(15, 30, 18, 0));
    }
}
