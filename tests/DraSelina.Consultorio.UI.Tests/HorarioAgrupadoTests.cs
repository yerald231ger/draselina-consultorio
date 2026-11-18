using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.UI.Pages.Sitio;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>
/// Cómo se escribe la semana tipo en Ubicación y horarios (entrada 7.1): rango para días seguidos, lista para sueltos,
/// "y" para dos seguidos, mezcla de rachas, varios Tramos en un día y la semana sin atención.
/// </summary>
public sealed class HorarioAgrupadoTests
{
    private static readonly TramoDeLaAgenda Tarde = new(new TimeOnly(15, 30), new TimeOnly(18, 0));

    private static readonly TramoDeLaAgenda Manana = new(new TimeOnly(10, 0), new TimeOnly(13, 0));

    private static readonly TramoDeLaAgenda TardeCorta = new(new TimeOnly(16, 0), new TimeOnly(18, 0));

    [Fact]
    public void Lunes_a_viernes_es_un_rango()
    {
        var grupos = HorarioAgrupado.Agrupar(Semana(
            (DayOfWeek.Monday, [Tarde]), (DayOfWeek.Tuesday, [Tarde]), (DayOfWeek.Wednesday, [Tarde]),
            (DayOfWeek.Thursday, [Tarde]), (DayOfWeek.Friday, [Tarde])));

        Assert.Equal([new GrupoDeHorario("Lunes a viernes", "3:30 a 6:00 pm, con cita")], grupos);
    }

    [Fact]
    public void Dias_sueltos_son_una_lista()
    {
        var grupos = HorarioAgrupado.Agrupar(Semana(
            (DayOfWeek.Monday, [Tarde]), (DayOfWeek.Wednesday, [Tarde]), (DayOfWeek.Friday, [Tarde])));

        Assert.Equal([new GrupoDeHorario("Lunes, miércoles y viernes", "3:30 a 6:00 pm, con cita")], grupos);
    }

    [Fact]
    public void Dos_horarios_distintos_son_dos_grupos_en_el_orden_de_la_semana()
    {
        // El sábado va primero en la lista de entrada; el grupo de lunes a viernes sigue saliendo antes.
        var grupos = HorarioAgrupado.Agrupar(Semana(
            (DayOfWeek.Saturday, [Manana]),
            (DayOfWeek.Monday, [Tarde]), (DayOfWeek.Tuesday, [Tarde]), (DayOfWeek.Wednesday, [Tarde]),
            (DayOfWeek.Thursday, [Tarde]), (DayOfWeek.Friday, [Tarde])));

        Assert.Equal(
            [
                new GrupoDeHorario("Lunes a viernes", "3:30 a 6:00 pm, con cita"),
                new GrupoDeHorario("Sábado", "10:00 am a 1:00 pm, con cita"),
            ],
            grupos);
    }

    [Fact]
    public void Dos_Tramos_en_un_dia_se_unen_con_y()
    {
        var grupos = HorarioAgrupado.Agrupar(Semana((DayOfWeek.Tuesday, [TardeCorta, Manana])));

        Assert.Equal([new GrupoDeHorario("Martes", "10:00 am a 1:00 pm y 4:00 a 6:00 pm, con cita")], grupos);
    }

    [Fact]
    public void La_semana_sin_atencion_no_tiene_grupos()
    {
        Assert.Empty(HorarioAgrupado.Agrupar(Semana()));
    }

    [Fact]
    public void Una_racha_y_un_dia_suelto_se_mezclan()
    {
        var grupos = HorarioAgrupado.Agrupar(Semana(
            (DayOfWeek.Monday, [Tarde]), (DayOfWeek.Tuesday, [Tarde]), (DayOfWeek.Wednesday, [Tarde]),
            (DayOfWeek.Friday, [Tarde])));

        Assert.Equal([new GrupoDeHorario("Lunes a miércoles y viernes", "3:30 a 6:00 pm, con cita")], grupos);
    }

    [Fact]
    public void Dos_dias_seguidos_se_unen_con_y()
    {
        var grupos = HorarioAgrupado.Agrupar(Semana((DayOfWeek.Saturday, [Manana]), (DayOfWeek.Sunday, [Manana])));

        Assert.Equal([new GrupoDeHorario("Sábado y domingo", "10:00 am a 1:00 pm, con cita")], grupos);
    }

    // Los siete días de lunes a domingo, como los devuelve SemanaDeAtencion; los que no se nombran, sin Tramos.
    private static List<DiaDeLaBase> Semana(params (DayOfWeek Dia, TramoDeLaAgenda[] Tramos)[] dias)
    {
        DayOfWeek[] deLunesADomingo =
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
            DayOfWeek.Saturday, DayOfWeek.Sunday,
        ];

        // Primero los nombrados, en el orden dado; después los que faltan, vacíos.
        return
        [
            .. dias.Select(d => new DiaDeLaBase(d.Dia, d.Tramos)),
            .. deLunesADomingo.Where(dia => dias.All(d => d.Dia != dia)).Select(dia => new DiaDeLaBase(dia, [])),
        ];
    }
}
