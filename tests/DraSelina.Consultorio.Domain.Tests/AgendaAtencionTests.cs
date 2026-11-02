using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>
/// <see cref="AgendaAtencion"/>: INV-B1 a B5, INV-A1 a A6 e INV-V1, sin base de datos. La agenda de partida es la
/// sembrada: lunes a viernes de 15:30 a 18:00.
/// </summary>
public class AgendaAtencionTests
{
    private static readonly TimeSpan Media = TimeSpan.FromMinutes(30);

    /// <summary>Martes 3 de noviembre de 2026.</summary>
    private static readonly DateOnly Martes = new(2026, 11, 3);

    /// <summary>Sábado 7 de noviembre de 2026.</summary>
    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static Tramo T(int hi, int mi, int hf, int mf) => Tramo.Crear(new TimeOnly(hi, mi), new TimeOnly(hf, mf));

    [Fact]
    public void Un_dia_habil_sin_custom_vale_la_base()
    {
        var vigentes = Sembrada().TramosVigentes(Martes);

        Assert.Equal(Martes, vigentes.Fecha);
        Assert.Equal([T(15, 30, 18, 0)], vigentes.Tramos);
        Assert.Equal(OrigenDeTramos.Base, vigentes.Origen);
    }

    [Fact]
    public void Un_sabado_sin_custom_no_tiene_tramos_y_viene_de_la_base()
    {
        var vigentes = Sembrada().TramosVigentes(Sabado);

        Assert.Empty(vigentes.Tramos);
        Assert.True(vigentes.SinTramos);
        Assert.Equal(OrigenDeTramos.Base, vigentes.Origen);
    }

    [Fact]
    public void Una_agenda_recien_creada_no_atiende_ningun_dia()
    {
        var agenda = AgendaAtencion.Crear();

        foreach (var dia in Enum.GetValues<DayOfWeek>())
        {
            Assert.Empty(agenda.TramosBase(dia));
        }

        Assert.Empty(agenda.FechasConCustom());
    }

    [Fact]
    public void Una_fecha_con_custom_vale_la_custom()
    {
        var agenda = Sembrada();
        Assert.Null(agenda.RegistrarCustom(Martes, [T(16, 0, 17, 0)], "cirugía", Media));

        var vigentes = agenda.TramosVigentes(Martes);

        Assert.Equal([T(16, 0, 17, 0)], vigentes.Tramos);
        Assert.Equal(OrigenDeTramos.Custom, vigentes.Origen);
        Assert.Equal([T(15, 30, 18, 0)], agenda.TramosVigentes(Martes.AddDays(7)).Tramos);
    }

    [Fact]
    public void Una_custom_sin_tramos_es_un_dia_que_no_se_atiende()
    {
        var agenda = Sembrada();
        Assert.Null(agenda.RegistrarCustom(Martes, [], "vacaciones", Media));

        var vigentes = agenda.TramosVigentes(Martes);

        Assert.Empty(vigentes.Tramos);
        Assert.Equal(OrigenDeTramos.Custom, vigentes.Origen);
    }

    [Fact]
    public void Una_custom_puede_abrir_un_sabado()
    {
        var agenda = Sembrada();
        Assert.Null(agenda.RegistrarCustom(Sabado, [T(10, 0, 13, 0)], "sábado abierto", Media));

        Assert.Equal([T(10, 0, 13, 0)], agenda.TramosVigentes(Sabado).Tramos);
    }

    [Fact]
    public void Tramos_encimados_en_la_base_se_rechazan_y_nada_cambia()
    {
        var agenda = Sembrada();

        var rechazo = agenda.CambiarBase(DayOfWeek.Tuesday, [T(15, 30, 17, 0), T(16, 30, 18, 0)], Media);

        Assert.Equal(RechazoDeAgendaAtencion.TramosEncimados, rechazo);
        Assert.Equal([T(15, 30, 18, 0)], agenda.TramosBase(DayOfWeek.Tuesday));
    }

    [Fact]
    public void Tramos_encimados_en_una_custom_se_rechazan_y_no_se_registra()
    {
        var agenda = Sembrada();

        var rechazo = agenda.RegistrarCustom(Martes, [T(16, 30, 18, 0), T(15, 30, 17, 0)], "cirugía", Media);

        Assert.Equal(RechazoDeAgendaAtencion.TramosEncimados, rechazo);
        Assert.Empty(agenda.FechasConCustom());
    }

    [Fact]
    public void Tramos_contiguos_no_se_enciman_y_quedan_en_orden()
    {
        var agenda = Sembrada();

        Assert.Null(agenda.CambiarBase(DayOfWeek.Tuesday, [T(17, 0, 18, 0), T(15, 30, 17, 0)], Media));

        Assert.Equal([T(15, 30, 17, 0), T(17, 0, 18, 0)], agenda.TramosBase(DayOfWeek.Tuesday));
    }

    [Fact]
    public void Un_tramo_de_la_base_mas_corto_que_el_tipo_mas_corto_se_rechaza()
    {
        var agenda = Sembrada();

        var rechazo = agenda.CambiarBase(DayOfWeek.Monday, [T(15, 30, 15, 50)], Media);

        Assert.Equal(RechazoDeAgendaAtencion.TramoMasCortoQueLaDuracionMinima, rechazo);
        Assert.Equal([T(15, 30, 18, 0)], agenda.TramosBase(DayOfWeek.Monday));
    }

    [Fact]
    public void Un_tramo_de_una_custom_mas_corto_que_el_tipo_mas_corto_se_rechaza()
    {
        var agenda = Sembrada();

        var rechazo = agenda.RegistrarCustom(Martes, [T(16, 0, 16, 20)], "cirugía", Media);

        Assert.Equal(RechazoDeAgendaAtencion.TramoMasCortoQueLaDuracionMinima, rechazo);
        Assert.Empty(agenda.FechasConCustom());
    }

    [Fact]
    public void Un_tramo_justo_del_tipo_mas_corto_se_acepta()
    {
        Assert.Null(Sembrada().CambiarBase(DayOfWeek.Monday, [T(15, 30, 16, 0)], Media));
    }

    [Fact]
    public void Un_dia_de_la_base_puede_quedar_sin_tramos()
    {
        var agenda = Sembrada();

        Assert.Null(agenda.CambiarBase(DayOfWeek.Friday, [], Media));

        Assert.Empty(agenda.TramosVigentes(new DateOnly(2026, 11, 6)).Tramos);
    }

    [Fact]
    public void Cambiar_la_base_no_toca_las_fechas_con_custom()
    {
        var agenda = Sembrada();
        agenda.RegistrarCustom(Martes, [T(16, 0, 17, 0)], "cirugía", Media);

        Assert.Null(agenda.CambiarBase(DayOfWeek.Tuesday, [T(9, 0, 12, 0)], Media));

        Assert.Equal([T(16, 0, 17, 0)], agenda.TramosVigentes(Martes).Tramos);
        Assert.Equal([T(9, 0, 12, 0)], agenda.TramosVigentes(Martes.AddDays(7)).Tramos);
        Assert.Equal([Martes], agenda.FechasConCustom());
    }

    [Fact]
    public void Una_segunda_custom_en_la_misma_fecha_no_se_registra()
    {
        var agenda = Sembrada();
        agenda.RegistrarCustom(Martes, [T(16, 0, 17, 0)], "cirugía", Media);

        var rechazo = agenda.RegistrarCustom(Martes, [], "vacaciones", Media);

        Assert.Equal(RechazoDeAgendaAtencion.FechaConCustom, rechazo);
        Assert.Equal("cirugía", Assert.Single(agenda.Customs).Motivo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Una_custom_sin_motivo_no_se_registra(string motivo)
    {
        var agenda = Sembrada();

        Assert.Equal(RechazoDeAgendaAtencion.SinMotivo, agenda.RegistrarCustom(Martes, [], motivo, Media));
        Assert.Empty(agenda.FechasConCustom());
    }

    [Fact]
    public void Cambiar_una_custom_reemplaza_sus_tramos_y_su_motivo()
    {
        var agenda = Sembrada();
        agenda.RegistrarCustom(Martes, [T(16, 0, 17, 0)], "cirugía", Media);

        Assert.Null(agenda.CambiarCustom(Martes, [T(15, 30, 16, 30)], "  consulta externa ", Media));

        var custom = Assert.Single(agenda.Customs);
        Assert.Equal("consulta externa", custom.Motivo);
        Assert.Equal([T(15, 30, 16, 30)], custom.Tramos);
    }

    [Fact]
    public void Cambiar_una_custom_sigue_las_invariantes()
    {
        var agenda = Sembrada();
        agenda.RegistrarCustom(Martes, [T(16, 0, 17, 0)], "cirugía", Media);

        Assert.Equal(RechazoDeAgendaAtencion.SinMotivo, agenda.CambiarCustom(Martes, [], " ", Media));
        Assert.Equal(
            RechazoDeAgendaAtencion.TramosEncimados,
            agenda.CambiarCustom(Martes, [T(15, 30, 17, 0), T(16, 30, 18, 0)], "cirugía", Media));
        Assert.Equal(
            RechazoDeAgendaAtencion.TramoMasCortoQueLaDuracionMinima,
            agenda.CambiarCustom(Martes, [T(16, 0, 16, 15)], "cirugía", Media));

        Assert.Equal([T(16, 0, 17, 0)], agenda.TramosVigentes(Martes).Tramos);
    }

    [Fact]
    public void Cambiar_o_borrar_una_custom_que_no_existe_se_rechaza()
    {
        var agenda = Sembrada();

        Assert.Equal(RechazoDeAgendaAtencion.FechaSinCustom, agenda.CambiarCustom(Martes, [], "cirugía", Media));
        Assert.Equal(RechazoDeAgendaAtencion.FechaSinCustom, agenda.BorrarCustom(Martes));
    }

    [Fact]
    public void Borrar_una_custom_devuelve_la_fecha_a_la_base()
    {
        var agenda = Sembrada();
        agenda.RegistrarCustom(Martes, [], "vacaciones", Media);

        Assert.Null(agenda.BorrarCustom(Martes));

        var vigentes = agenda.TramosVigentes(Martes);
        Assert.Equal([T(15, 30, 18, 0)], vigentes.Tramos);
        Assert.Equal(OrigenDeTramos.Base, vigentes.Origen);
        Assert.Empty(agenda.FechasConCustom());
    }

    [Fact]
    public void Las_fechas_con_custom_salen_en_orden()
    {
        var agenda = Sembrada();
        agenda.RegistrarCustom(Sabado, [T(10, 0, 13, 0)], "sábado abierto", Media);
        agenda.RegistrarCustom(Martes, [], "vacaciones", Media);

        Assert.Equal([Martes, Sabado], agenda.FechasConCustom());
    }

    [Fact]
    public void La_duracion_minima_debe_ser_positiva()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Sembrada().CambiarBase(DayOfWeek.Monday, [T(15, 30, 18, 0)], TimeSpan.Zero));
    }

    /// <summary>La agenda sembrada: lunes a viernes de 15:30 a 18:00.</summary>
    private static AgendaAtencion Sembrada()
    {
        var agenda = AgendaAtencion.Crear();
        foreach (var dia in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            Assert.Null(agenda.CambiarBase(dia, [T(15, 30, 18, 0)], Media));
        }

        return agenda;
    }
}
