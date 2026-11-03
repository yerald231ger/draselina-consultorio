using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>
/// <see cref="CalculadoraDeDisponibilidad"/>: el algoritmo de la Parte I, 2.2, sin base de datos. La agenda de partida
/// es la sembrada (lunes a viernes de 15:30 a 18:00), las reglas las sembradas (anticipación 1 día, ventana 2 meses) y
/// el reloj marca el lunes 2 de noviembre de 2026 a las 10:00 en Ciudad de México.
/// </summary>
public class CalculadoraDeDisponibilidadTests
{
    private static readonly TimeSpan Media = TimeSpan.FromMinutes(30);

    /// <summary>Lunes 2 de noviembre de 2026: hoy.</summary>
    private static readonly DateOnly Lunes = new(2026, 11, 2);

    /// <summary>Martes 3 de noviembre de 2026: mañana.</summary>
    private static readonly DateOnly Martes = new(2026, 11, 3);

    /// <summary>Miércoles 4 de noviembre de 2026.</summary>
    private static readonly DateOnly Miercoles = new(2026, 11, 4);

    /// <summary>Sábado 7 de noviembre de 2026.</summary>
    private static readonly DateOnly Sabado = new(2026, 11, 7);

    /// <summary>Hoy a las 10:00 local.</summary>
    private static readonly DateTimeOffset Ahora = HoraLocal.AUtc(Lunes, new TimeOnly(10, 0));

    private static readonly TimeOnly[] TardeCompleta = [H(15, 30), H(16, 0), H(16, 30), H(17, 0), H(17, 30)];

    [Fact]
    public void Un_dia_habil_libre_ofrece_la_rejilla_completa_del_tramo()
    {
        var dias = Calcular(Sembrada());

        Assert.Equal(TardeCompleta, Horas(dias, Martes));
    }

    [Fact]
    public void Solo_entran_los_horarios_que_caben_completos_en_el_tramo()
    {
        var agenda = Sembrada();
        Assert.Null(agenda.CambiarBase(DayOfWeek.Tuesday, [T(15, 30, 17, 45)], Media));

        Assert.Equal([H(15, 30), H(16, 0), H(16, 30), H(17, 0)], Horas(Calcular(agenda), Martes));
    }

    [Fact]
    public void La_rejilla_sigue_la_duracion_del_tipo_de_visita()
    {
        var dias = CalculadoraDeDisponibilidad.Calcular(Sembrada(), [], TimeSpan.FromMinutes(45), Reglas(), Ahora);

        Assert.Equal([H(15, 30), H(16, 15), H(17, 0)], Horas(dias, Martes));
    }

    [Fact]
    public void Una_Cita_en_la_rejilla_quita_solo_su_horario()
    {
        var dias = Calcular(Sembrada(), Cita(Martes, 16, 0));

        Assert.Equal([H(15, 30), H(16, 30), H(17, 0), H(17, 30)], Horas(dias, Martes));
    }

    [Fact]
    public void Una_Cita_del_consultorio_fuera_de_la_rejilla_quita_los_dos_horarios_que_traslapa()
    {
        var dias = Calcular(Sembrada(), Cita(Martes, 16, 10));

        Assert.Equal([H(15, 30), H(17, 0), H(17, 30)], Horas(dias, Martes));
    }

    [Fact]
    public void Una_Cita_de_otra_fecha_no_quita_nada()
    {
        var dias = Calcular(Sembrada(), Cita(Miercoles, 16, 0));

        Assert.Equal(TardeCompleta, Horas(dias, Martes));
    }

    [Fact]
    public void Con_anticipacion_de_un_dia_hoy_no_aparece_aunque_tenga_horarios_futuros()
    {
        var dias = Calcular(Sembrada());

        Assert.DoesNotContain(dias, d => d.Fecha == Lunes);
        Assert.Equal(Martes, dias[0].Fecha);
    }

    [Fact]
    public void Con_anticipacion_de_dos_dias_la_primera_fecha_es_pasado_manana()
    {
        var dias = CalculadoraDeDisponibilidad.Calcular(Sembrada(), [], Media, Reglas(anticipacion: 2), Ahora);

        Assert.Equal(Miercoles, dias[0].Fecha);
    }

    [Fact]
    public void Con_anticipacion_cero_hoy_aparece_solo_con_los_horarios_posteriores_a_ahora()
    {
        var ahora = HoraLocal.AUtc(Lunes, new TimeOnly(16, 10));

        var dias = CalculadoraDeDisponibilidad.Calcular(Sembrada(), [], Media, Reglas(anticipacion: 0), ahora);

        Assert.Equal(Lunes, dias[0].Fecha);
        Assert.Equal([H(16, 30), H(17, 0), H(17, 30)], dias[0].Horas());
        Assert.Equal(TardeCompleta, Horas(dias, Martes));
    }

    [Fact]
    public void Con_anticipacion_cero_un_horario_que_empieza_justo_ahora_ya_no_se_ofrece()
    {
        var ahora = HoraLocal.AUtc(Lunes, new TimeOnly(16, 30));

        var dias = CalculadoraDeDisponibilidad.Calcular(Sembrada(), [], Media, Reglas(anticipacion: 0), ahora);

        Assert.Equal([H(17, 0), H(17, 30)], Horas(dias, Lunes));
    }

    [Fact]
    public void La_ventana_termina_hoy_mas_los_meses_de_la_ventana_inclusive()
    {
        var dias = CalculadoraDeDisponibilidad.Calcular(TodosLosDias(), [], Media, Reglas(), Ahora);

        Assert.Equal(Martes, dias[0].Fecha);
        Assert.Equal(new DateOnly(2027, 1, 2), dias[^1].Fecha);
        Assert.Equal(TardeCompleta, dias[^1].Horas());
        Assert.Equal(61, dias.Count);
    }

    [Fact]
    public void La_ventana_es_la_de_las_reglas()
    {
        var ventana = CalculadoraDeDisponibilidad.Ventana(Reglas(anticipacion: 1, ventana: 1), Ahora);

        Assert.Equal(new VentanaDeReserva(Martes, new DateOnly(2026, 12, 2)), ventana);
        Assert.Equal(
            (HoraLocal.AUtc(Martes, TimeOnly.MinValue), HoraLocal.AUtc(new DateOnly(2026, 12, 3), TimeOnly.MinValue)),
            ventana.EnUtc());
    }

    [Fact]
    public void Hoy_es_la_fecha_local_aunque_en_UTC_ya_sea_manana()
    {
        // Lunes 23:00 en Ciudad de México es martes 05:00 UTC: mañana sigue siendo el martes.
        var ahora = HoraLocal.AUtc(Lunes, new TimeOnly(23, 0));

        var dias = CalculadoraDeDisponibilidad.Calcular(Sembrada(), [], Media, Reglas(), ahora);

        Assert.Equal(Martes, dias[0].Fecha);
    }

    [Fact]
    public void Un_sabado_sin_custom_y_una_custom_de_cero_tramos_no_aparecen()
    {
        var agenda = Sembrada();
        Assert.Null(agenda.RegistrarCustom(Miercoles, [], "vacaciones", Media));

        var fechas = Calcular(agenda).Select(d => d.Fecha).ToList();

        Assert.DoesNotContain(Miercoles, fechas);
        Assert.DoesNotContain(Sabado, fechas);
        Assert.DoesNotContain(Sabado.AddDays(1), fechas);
        Assert.Contains(Martes, fechas);
    }

    [Fact]
    public void Una_custom_que_abre_un_sabado_ofrece_su_rejilla()
    {
        var agenda = Sembrada();
        Assert.Null(agenda.RegistrarCustom(Sabado, [T(10, 0, 11, 0)], "sábado abierto", Media));

        Assert.Equal([H(10, 0), H(10, 30)], Horas(Calcular(agenda), Sabado));
    }

    [Fact]
    public void Una_custom_con_dos_tramos_ofrece_la_rejilla_de_cada_uno_en_orden()
    {
        var agenda = Sembrada();
        Assert.Null(agenda.RegistrarCustom(Martes, [T(17, 0, 18, 0), T(9, 0, 10, 0)], "doble turno", Media));

        Assert.Equal([H(9, 0), H(9, 30), H(17, 0), H(17, 30)], Horas(Calcular(agenda), Martes));
    }

    [Fact]
    public void Un_dia_lleno_aparece_con_la_lista_vacia()
    {
        var citas = TardeCompleta.Select(h => Cita(Martes, h.Hour, h.Minute)).ToArray();

        var dias = Calcular(Sembrada(), citas);

        var martes = Assert.Single(dias, d => d.Fecha == Martes);
        Assert.Empty(martes.Horarios);
    }

    [Fact]
    public void Una_sola_Cita_larga_que_cubre_todo_el_tramo_deja_el_dia_lleno_y_vacio()
    {
        var inicio = HoraLocal.AUtc(Martes, H(15, 30));
        var tarde = RangoDeTiempo.Crear(inicio, inicio + TimeSpan.FromMinutes(150));

        var martes = Assert.Single(Calcular(Sembrada(), tarde), d => d.Fecha == Martes);

        Assert.Empty(martes.Horarios);
    }

    [Fact]
    public void Con_anticipacion_cero_hoy_no_aparece_si_su_ultimo_horario_ya_empezo()
    {
        var ahora = HoraLocal.AUtc(Lunes, new TimeOnly(17, 30));

        var dias = CalculadoraDeDisponibilidad.Calcular(Sembrada(), [], Media, Reglas(anticipacion: 0), ahora);

        Assert.DoesNotContain(dias, d => d.Fecha == Lunes);
        Assert.Equal(Martes, dias[0].Fecha);
    }

    [Fact]
    public void Con_anticipacion_cero_hoy_lleno_por_Citas_aparece_vacio()
    {
        var ahora = HoraLocal.AUtc(Lunes, new TimeOnly(16, 10));
        RangoDeTiempo[] citas = [Cita(Lunes, 16, 30), Cita(Lunes, 17, 0), Cita(Lunes, 17, 30)];

        var dias = CalculadoraDeDisponibilidad.Calcular(Sembrada(), citas, Media, Reglas(anticipacion: 0), ahora);

        Assert.Empty(Assert.Single(dias, d => d.Fecha == Lunes).Horarios);
    }

    [Fact]
    public void Un_tipo_que_no_cabe_en_el_tramo_de_una_custom_no_ve_esa_fecha()
    {
        var agenda = Sembrada();
        Assert.Null(agenda.RegistrarCustom(Martes, [T(15, 30, 16, 0)], "tarde corta", Media));

        var deTreinta = Calcular(agenda);
        var deCuarentaYCinco = CalculadoraDeDisponibilidad.Calcular(agenda, [], TimeSpan.FromMinutes(45), Reglas(), Ahora);

        Assert.Equal([H(15, 30)], Horas(deTreinta, Martes));
        Assert.DoesNotContain(deCuarentaYCinco, d => d.Fecha == Martes);
        Assert.Contains(deCuarentaYCinco, d => d.Fecha == Miercoles);
    }

    [Fact]
    public void Los_horarios_libres_llevan_su_fecha()
    {
        var martes = Assert.Single(Calcular(Sembrada()), d => d.Fecha == Martes);

        Assert.All(martes.Horarios, h => Assert.Equal(Martes, h.Fecha));
    }

    [Fact]
    public void La_duracion_debe_ser_positiva()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CalculadoraDeDisponibilidad.Calcular(Sembrada(), [], TimeSpan.Zero, Reglas(), Ahora));
    }

    [Theory]
    [InlineData(15, 30, true)]
    [InlineData(16, 0, true)]
    [InlineData(17, 0, true)]
    [InlineData(16, 10, false)]
    [InlineData(17, 30, false)]
    [InlineData(15, 0, false)]
    [InlineData(17, 45, false)]
    public void Un_inicio_esta_en_la_rejilla_si_sale_de_los_pasos_del_tramo_y_cabe_completo(int hora, int minuto, bool esperado)
    {
        var agenda = Sembrada();
        Assert.Null(agenda.CambiarBase(DayOfWeek.Tuesday, [T(15, 30, 17, 45)], Media));

        Assert.Equal(esperado, CalculadoraDeDisponibilidad.EstaEnLaRejilla(agenda.TramosVigentes(Martes), H(hora, minuto), Media));
    }

    [Fact]
    public void En_una_fecha_sin_tramos_ningun_inicio_esta_en_la_rejilla()
    {
        Assert.False(CalculadoraDeDisponibilidad.EstaEnLaRejilla(Sembrada().TramosVigentes(Sabado), H(16, 0), Media));
    }

    private static IReadOnlyList<DisponibilidadDelDia> Calcular(AgendaAtencion agenda, params RangoDeTiempo[] citas) =>
        CalculadoraDeDisponibilidad.Calcular(agenda, citas, Media, Reglas(), Ahora);

    private static IReadOnlyList<TimeOnly> Horas(IReadOnlyList<DisponibilidadDelDia> dias, DateOnly fecha) =>
        Assert.Single(dias, d => d.Fecha == fecha).Horas();

    /// <summary>Una Cita activa de 30 minutos que empieza a esa hora local.</summary>
    private static RangoDeTiempo Cita(DateOnly fecha, int hora, int minuto)
    {
        var inicio = HoraLocal.AUtc(fecha, H(hora, minuto));
        return RangoDeTiempo.Crear(inicio, inicio + Media);
    }

    private static ReglasDeReserva Reglas(int anticipacion = 1, int ventana = 2)
    {
        Assert.Null(ReglasDeReserva.Crear(anticipacion, ventana, 120, 3, out var reglas));
        return reglas!;
    }

    private static TimeOnly H(int hora, int minuto) => new(hora, minuto);

    private static Tramo T(int hi, int mi, int hf, int mf) => Tramo.Crear(new TimeOnly(hi, mi), new TimeOnly(hf, mf));

    /// <summary>La agenda sembrada: lunes a viernes de 15:30 a 18:00.</summary>
    private static AgendaAtencion Sembrada() => Abierta(
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);

    /// <summary>De 15:30 a 18:00 los siete días.</summary>
    private static AgendaAtencion TodosLosDias() => Abierta(Enum.GetValues<DayOfWeek>());

    private static AgendaAtencion Abierta(params DayOfWeek[] dias)
    {
        var agenda = AgendaAtencion.Crear();
        foreach (var dia in dias)
        {
            Assert.Null(agenda.CambiarBase(dia, [T(15, 30, 18, 0)], Media));
        }

        return agenda;
    }
}

internal static class DisponibilidadDelDiaDePrueba
{
    public static IReadOnlyList<TimeOnly> Horas(this DisponibilidadDelDia dia) => [.. dia.Horarios.Select(h => h.Hora)];
}
