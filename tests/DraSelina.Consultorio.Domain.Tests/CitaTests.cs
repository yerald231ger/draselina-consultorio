using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>
/// <see cref="Cita.Agendar"/>: INV-C1 a C5, C7, C9 a C11, <see cref="Cita.Reprogramar"/>: RN-15, RN-17, y <see cref="Cita.Cancelar"/>: RN-16, RN-31, INV-C8, sin base de datos. Las reglas son las sembradas: lunes a
/// viernes de 15:30 a 18:00, anticipación de 1 día y ventana de 2 meses, tope semanal de 3.
/// </summary>
public class CitaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Manana16 = new(2026, 11, 3, 22, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 11, 2);
    private static readonly DateOnly Martes = new(2026, 11, 3);

    /// <summary>Una clave de prueba de 32 bytes para el Enlace de gestión.</summary>
    private static readonly ClaveDeGestion Clave = ClaveDeGestion.Crear("clave-de-prueba-de-32-bytes-o-mas"u8);

    [Fact]
    public void Agendar_crea_una_Cita_Agendada_en_linea_con_su_fin_fijado_por_la_duracion()
    {
        var solicitud = Guid.CreateVersion7();
        var paciente = PatientId.Nuevo();
        var tipo = TipoDeVisita.Crear("Primera consulta", 30, reservableEnLinea: true, orden: 1);

        var rechazo = Cita.Agendar(solicitud, paciente, tipo, Manana16, "Dolor de rodilla", Consentimiento(), Reglas(Manana16), 0, Ahora, Clave, out var cita);

        Assert.Null(rechazo);
        Assert.NotNull(cita);
        Assert.Equal(7, cita.Id.Valor.Version);
        Assert.Equal(paciente, cita.PacienteId);
        Assert.Equal(tipo.Id, cita.TipoDeVisitaId);
        Assert.Equal(Manana16, cita.Rango.Inicio);
        Assert.Equal(Manana16.AddMinutes(30), cita.Rango.Fin);
        Assert.Equal(EstadoCita.Agendada, cita.Estado);
        Assert.Equal(OrigenDeCita.EnLinea, cita.Origen);
        Assert.Null(cita.MotivoDeCancelacion);
        Assert.Null(cita.CanceladaEn);
        Assert.Equal("Dolor de rodilla", cita.MotivoDeConsulta);
        Assert.Equal(Ahora, cita.CreadaEn);
        Assert.Equal(solicitud, cita.SolicitudId);
        Assert.Equal(EnlaceDeGestion.Derivar(Clave, cita.Id).Hash, cita.TokenHash);
    }

    [Fact]
    public void La_Cita_lleva_su_Consentimiento_desde_que_se_crea()
    {
        var consentimiento = Consentimiento();

        Cita.Agendar(Guid.CreateVersion7(), PatientId.Nuevo(), Tipo(), Manana16, "Dolor", consentimiento, Reglas(Manana16), 0, Ahora, Clave, out var cita);

        Assert.Same(consentimiento, cita!.Consentimiento);
        Assert.Equal(CanalDeConsentimiento.EnLinea, cita.Consentimiento.Canal);
        Assert.Equal("2026-11-borrador", cita.Consentimiento.VersionDelAviso);
        Assert.Equal(Ahora, cita.Consentimiento.En);
    }

    [Fact]
    public void Los_instantes_quedan_en_UTC_aunque_lleguen_con_otra_zona()
    {
        var local = new DateTimeOffset(2026, 11, 3, 16, 0, 0, TimeSpan.FromHours(-6));

        Cita.Agendar(Guid.CreateVersion7(), PatientId.Nuevo(), Tipo(), local, "Dolor", Consentimiento(), Reglas(local), 0, Ahora, Clave, out var cita);

        Assert.Equal(TimeSpan.Zero, cita!.Rango.Inicio.Offset);
        Assert.Equal(Manana16, cita.Rango.Inicio);
    }

    [Fact]
    public void Una_Cita_en_el_pasado_se_rechaza()
    {
        var antes = Ahora.AddMinutes(-30);

        var rechazo = Cita.Agendar(Guid.CreateVersion7(), PatientId.Nuevo(), Tipo(), antes, "Dolor", Consentimiento(), Reglas(antes), 0, Ahora, Clave, out var cita);

        Assert.Equal(RechazoAlAgendar.EnElPasado, rechazo);
        Assert.Null(cita);
    }

    [Fact]
    public void Una_Cita_que_empieza_justo_ahora_ya_paso()
    {
        // RN-31: "ya pasó" se mide desde el inicio.
        var rechazo = Cita.Agendar(Guid.CreateVersion7(), PatientId.Nuevo(), Tipo(), Ahora, "Dolor", Consentimiento(), Reglas(Ahora), 0, Ahora, Clave, out _);

        Assert.Equal(RechazoAlAgendar.EnElPasado, rechazo);
    }

    [Fact]
    public void Un_tipo_que_no_se_reserva_en_linea_se_rechaza()
    {
        var tipo = TipoDeVisita.Crear("Cirugía menor", 60, reservableEnLinea: false, orden: 3);

        var rechazo = Cita.Agendar(Guid.CreateVersion7(), PatientId.Nuevo(), tipo, Manana16, "Dolor", Consentimiento(), Reglas(Manana16), 0, Ahora, Clave, out var cita);

        Assert.Equal(RechazoAlAgendar.TipoNoReservable, rechazo);
        Assert.Null(cita);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_motivo_de_consulta_no_hay_Cita_en_linea(string motivo)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Cita.Agendar(Guid.CreateVersion7(), PatientId.Nuevo(), Tipo(), Manana16, motivo, Consentimiento(), Reglas(Manana16), 0, Ahora, Clave, out _));
    }

    [Fact]
    public void Sin_paciente_no_hay_Cita()
    {
        Assert.Throws<ArgumentException>(
            () => Cita.Agendar(Guid.CreateVersion7(), default, Tipo(), Manana16, "Dolor", Consentimiento(), Reglas(Manana16), 0, Ahora, Clave, out _));
    }

    [Fact]
    public void Sin_solicitud_no_hay_Cita_en_linea()
    {
        Assert.Throws<ArgumentException>(
            () => Cita.Agendar(Guid.Empty, PatientId.Nuevo(), Tipo(), Manana16, "Dolor", Consentimiento(), Reglas(Manana16), 0, Ahora, Clave, out _));
    }

    [Fact]
    public void Sin_Consentimiento_no_hay_Cita()
    {
        Assert.Throws<ArgumentNullException>(
            () => Cita.Agendar(Guid.CreateVersion7(), PatientId.Nuevo(), Tipo(), Manana16, "Dolor", null!, Reglas(Manana16), 0, Ahora, Clave, out _));
    }

    [Fact]
    public void Una_Cita_en_linea_no_lleva_un_Consentimiento_externo()
    {
        var externo = Agenda.Domain.Consentimiento.Crear(Ahora, "2026-11-borrador", CanalDeConsentimiento.Externo);

        Assert.Throws<ArgumentException>(
            () => Cita.Agendar(Guid.CreateVersion7(), PatientId.Nuevo(), Tipo(), Manana16, "Dolor", externo, Reglas(Manana16), 0, Ahora, Clave, out _));
    }

    [Fact]
    public void El_Consentimiento_lleva_version_del_aviso()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Agenda.Domain.Consentimiento.Crear(Ahora, " ", CanalDeConsentimiento.EnLinea));
    }

    // INV-C5: tramos vigentes, rejilla y ventana.

    [Theory]
    [InlineData(15, 30)]
    [InlineData(16, 0)]
    [InlineData(17, 30)]
    public void Una_Cita_en_la_rejilla_de_un_tramo_vigente_se_agenda(int hora, int minuto)
    {
        var inicio = Local(Martes, hora, minuto);

        Assert.Null(Agendar(inicio, Reglas(inicio), out var cita));
        Assert.NotNull(cita);
    }

    [Fact]
    public void Una_Cita_fuera_de_la_rejilla_queda_fuera_de_horario()
    {
        var inicio = Local(Martes, 15, 45);

        Assert.Equal(RechazoAlAgendar.FueraDeHorario, Agendar(inicio, Reglas(inicio), out var cita));
        Assert.Null(cita);
    }

    [Fact]
    public void Una_Cita_que_no_cabe_completa_en_el_tramo_queda_fuera_de_horario()
    {
        // 17:45 a 18:15: el tramo termina a las 18:00.
        var inicio = Local(Martes, 17, 45);

        Assert.Equal(RechazoAlAgendar.FueraDeHorario, Agendar(inicio, Reglas(inicio), out _));
    }

    [Fact]
    public void Una_Cita_antes_del_tramo_queda_fuera_de_horario()
    {
        var inicio = Local(Martes, 15, 0);

        Assert.Equal(RechazoAlAgendar.FueraDeHorario, Agendar(inicio, Reglas(inicio), out _));
    }

    [Fact]
    public void Un_sabado_sin_custom_queda_fuera_de_horario()
    {
        var inicio = Local(new DateOnly(2026, 11, 7), 16, 0);

        Assert.Equal(RechazoAlAgendar.FueraDeHorario, Agendar(inicio, Reglas(inicio), out _));
    }

    [Fact]
    public void Un_dia_con_custom_que_no_se_atiende_queda_fuera_de_horario()
    {
        var inicio = Local(Martes, 16, 0);

        Assert.Equal(RechazoAlAgendar.FueraDeHorario, Agendar(inicio, Reglas(inicio, []), out _));
    }

    [Fact]
    public void Una_custom_que_abre_un_sabado_deja_agendar_en_su_rejilla()
    {
        var sabado = new DateOnly(2026, 11, 7);
        var inicio = Local(sabado, 10, 30);

        var rechazo = Agendar(inicio, Reglas(inicio, [Tramo.Crear(new TimeOnly(10, 0), new TimeOnly(11, 0))]), out var cita);

        Assert.Null(rechazo);
        Assert.Equal(inicio, cita!.Rango.Inicio);
    }

    [Fact]
    public void Una_custom_reemplaza_a_la_base_y_la_hora_de_la_base_queda_fuera_de_horario()
    {
        var inicio = Local(Martes, 16, 0);

        var rechazo = Agendar(inicio, Reglas(inicio, [Tramo.Crear(new TimeOnly(10, 0), new TimeOnly(11, 0))]), out _);

        Assert.Equal(RechazoAlAgendar.FueraDeHorario, rechazo);
    }

    [Fact]
    public void Hoy_con_anticipacion_de_un_dia_queda_fuera_de_ventana()
    {
        // Hoy lunes a las 16:00: futuro, en la rejilla, pero antes de mañana.
        var inicio = Local(Hoy, 16, 0);

        Assert.Equal(RechazoAlAgendar.FueraDeVentana, Agendar(inicio, Reglas(inicio), out var cita));
        Assert.Null(cita);
    }

    [Fact]
    public void Con_anticipacion_cero_hoy_si_se_agenda()
    {
        var inicio = Local(Hoy, 16, 0);
        var reglas = Reglas(inicio, anticipacionMinimaDias: 0);

        Assert.Null(Agendar(inicio, reglas, out _));
    }

    [Fact]
    public void Un_dia_despues_de_la_ventana_queda_fuera_de_ventana_aunque_tenga_tramos()
    {
        // Hoy + 2 meses + 1 día: domingo 3 de enero de 2027, abierto por una custom.
        var inicio = Local(new DateOnly(2027, 1, 3), 10, 0);

        var rechazo = Agendar(inicio, Reglas(inicio, [Tramo.Crear(new TimeOnly(10, 0), new TimeOnly(11, 0))]), out _);

        Assert.Equal(RechazoAlAgendar.FueraDeVentana, rechazo);
    }

    [Fact]
    public void El_ultimo_dia_de_la_ventana_se_agenda()
    {
        // Hoy + 2 meses: sábado 2 de enero de 2027, abierto por una custom.
        var inicio = Local(new DateOnly(2027, 1, 2), 10, 30);

        var rechazo = Agendar(inicio, Reglas(inicio, [Tramo.Crear(new TimeOnly(10, 0), new TimeOnly(11, 0))]), out var cita);

        Assert.Null(rechazo);
        Assert.NotNull(cita);
    }

    [Fact]
    public void En_el_pasado_se_evalua_antes_que_la_ventana_y_el_horario()
    {
        // Hoy a las 11:45: pasado, fuera de la ventana y fuera de la rejilla; gana el pasado.
        var inicio = Local(Hoy, 11, 45);

        Assert.Equal(RechazoAlAgendar.EnElPasado, Agendar(inicio, Reglas(inicio), out _));
    }

    [Fact]
    public void Unos_tramos_vigentes_de_otra_fecha_son_un_error_del_llamador()
    {
        var inicio = Local(Martes, 16, 0);
        var deOtraFecha = Reglas(Local(Martes.AddDays(1), 16, 0));

        Assert.Throws<ArgumentException>(() => Agendar(inicio, deOtraFecha, out _));
    }

    // RN-18: el tope semanal por teléfono. El conteo lo hace el llamador; la Cita solo compara.

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Bajo_el_tope_semanal_se_agenda(int citasDeLaSemana)
    {
        var inicio = Local(Martes, 16, 0);

        Assert.Null(Agendar(inicio, Reglas(inicio), out var cita, citasDeLaSemana));
        Assert.NotNull(cita);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void Con_el_tope_semanal_alcanzado_se_rechaza(int citasDeLaSemana)
    {
        var inicio = Local(Martes, 16, 0);

        Assert.Equal(RechazoAlAgendar.TopeSemanal, Agendar(inicio, Reglas(inicio), out var cita, citasDeLaSemana));
        Assert.Null(cita);
    }

    [Fact]
    public void Con_tope_1_una_Cita_en_la_semana_ya_lo_alcanza()
    {
        var inicio = Local(Martes, 16, 0);
        var reglas = Reglas(inicio, topeSemanal: 1);

        Assert.Null(Agendar(inicio, reglas, out _, citasDeLaSemana: 0));
        Assert.Equal(RechazoAlAgendar.TopeSemanal, Agendar(inicio, reglas, out _, citasDeLaSemana: 1));
    }

    [Fact]
    public void El_horario_se_evalua_antes_que_el_tope_semanal()
    {
        var inicio = Local(Martes, 15, 45);

        Assert.Equal(RechazoAlAgendar.FueraDeHorario, Agendar(inicio, Reglas(inicio), out _, citasDeLaSemana: 3));
    }

    [Fact]
    public void Un_conteo_negativo_es_un_error_del_llamador()
    {
        var inicio = Local(Martes, 16, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => Agendar(inicio, Reglas(inicio), out _, citasDeLaSemana: -1));
    }

    [Theory]
    [InlineData("2026-11-02")]
    [InlineData("2026-11-05")]
    [InlineData("2026-11-08")]
    public void La_semana_local_va_del_lunes_a_las_0_al_lunes_siguiente_a_las_0(string fecha)
    {
        var (desde, hasta) = HoraLocal.SemanaDe(DateOnly.Parse(fecha));

        Assert.Equal(HoraLocal.AUtc(new DateOnly(2026, 11, 2), TimeOnly.MinValue), desde);
        Assert.Equal(HoraLocal.AUtc(new DateOnly(2026, 11, 9), TimeOnly.MinValue), hasta);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 6, 0, 0, TimeSpan.Zero), desde);
    }

    [Fact]
    public void ReglasDeAgenda_lleva_los_valores_de_las_reglas_de_reserva()
    {
        var reglas = Reglas(Manana16);

        Assert.Equal(Martes, reglas.TramosVigentes.Fecha);
        Assert.Equal(1, reglas.AnticipacionMinimaDias);
        Assert.Equal(2, reglas.VentanaMeses);
        Assert.Equal(120, reglas.UmbralCancelacionTardiaMin);
        Assert.Equal(3, reglas.TopeSemanalPorTelefono);
    }

    // Cita.Reprogramar: la Cita Agendada el martes 3 a las 16:00 local, con el reloj en el lunes 2 a las 12:00 local.
    // RN-15, RN-17, INV-C4 e INV-C5 sobre el rango nuevo.

    private static readonly DateOnly Jueves = new(2026, 11, 5);

    [Fact]
    public void Reprogramar_mueve_la_misma_Cita_y_conserva_lo_demas()
    {
        var cita = Agendada();
        var (id, token, origen, solicitud, consentimiento, motivo) =
            (cita.Id, cita.TokenHash, cita.Origen, cita.SolicitudId, cita.Consentimiento, cita.MotivoDeConsulta);
        var nuevo = Local(Jueves, 17, 0);

        var rechazo = cita.Reprogramar(Rango(nuevo), Reglas(nuevo), Ahora);

        Assert.Null(rechazo);
        Assert.Equal(nuevo, cita.Rango.Inicio);
        Assert.Equal(nuevo.AddMinutes(30), cita.Rango.Fin);
        Assert.Equal(EstadoCita.Agendada, cita.Estado);
        Assert.Equal(id, cita.Id);
        Assert.Equal(token, cita.TokenHash);
        Assert.Equal(EnlaceDeGestion.Derivar(Clave, cita.Id).Hash, cita.TokenHash);
        Assert.Equal(origen, cita.Origen);
        Assert.Equal(solicitud, cita.SolicitudId);
        Assert.Same(consentimiento, cita.Consentimiento);
        Assert.Equal(motivo, cita.MotivoDeConsulta);
    }

    [Fact]
    public void Reprogramar_a_120_minutos_del_inicio_actual_todavia_se_puede()
    {
        var cita = Agendada();
        var nuevo = Local(Jueves, 17, 0);
        var aLas120 = Manana16.AddMinutes(-120);

        Assert.Null(cita.Reprogramar(Rango(nuevo), Reglas(nuevo), aLas120));
        Assert.Equal(nuevo, cita.Rango.Inicio);
    }

    [Fact]
    public void Reprogramar_a_119_minutos_del_inicio_actual_esta_cerrado_y_no_cambia_nada()
    {
        var cita = Agendada();
        var nuevo = Local(Jueves, 17, 0);
        var aLas119 = Manana16.AddMinutes(-119);

        Assert.Equal(RechazoAlReprogramar.ReprogramacionCerrada, cita.Reprogramar(Rango(nuevo), Reglas(nuevo), aLas119));
        Assert.Equal(Manana16, cita.Rango.Inicio);
    }

    [Fact]
    public void El_umbral_se_lee_de_las_reglas()
    {
        var cita = Agendada();
        var nuevo = Local(Jueves, 17, 0);

        // Con un umbral de 60, a 90 minutos todavía se puede.
        Assert.Null(cita.Reprogramar(Rango(nuevo), Reglas(nuevo, umbralMin: 60), Manana16.AddMinutes(-90)));
    }

    [Fact]
    public void Reprogramar_fuera_de_la_rejilla_queda_fuera_de_horario()
    {
        var cita = Agendada();
        var nuevo = Local(Jueves, 16, 15);

        Assert.Equal(RechazoAlReprogramar.FueraDeHorario, cita.Reprogramar(Rango(nuevo), Reglas(nuevo), Ahora));
        Assert.Equal(Manana16, cita.Rango.Inicio);
    }

    [Fact]
    public void Un_rango_nuevo_que_no_cabe_completo_en_el_tramo_queda_fuera_de_horario()
    {
        // 60 minutos desde las 17:30: el tramo termina a las 18:00. La rejilla se mide con la duración del rango.
        var cita = Agendada();
        var nuevo = Local(Jueves, 17, 30);

        Assert.Equal(
            RechazoAlReprogramar.FueraDeHorario,
            cita.Reprogramar(RangoDeTiempo.Crear(nuevo, nuevo.AddMinutes(60)), Reglas(nuevo), Ahora));
    }

    [Fact]
    public void Reprogramar_a_un_sabado_sin_custom_queda_fuera_de_horario()
    {
        var cita = Agendada();
        var nuevo = Local(new DateOnly(2026, 11, 7), 16, 0);

        Assert.Equal(RechazoAlReprogramar.FueraDeHorario, cita.Reprogramar(Rango(nuevo), Reglas(nuevo), Ahora));
    }

    [Fact]
    public void Reprogramar_a_hoy_queda_fuera_de_ventana()
    {
        var cita = Agendada();
        var nuevo = Local(Hoy, 16, 0);

        Assert.Equal(RechazoAlReprogramar.FueraDeVentana, cita.Reprogramar(Rango(nuevo), Reglas(nuevo), Ahora));
        Assert.Equal(Manana16, cita.Rango.Inicio);
    }

    [Fact]
    public void Reprogramar_mas_alla_de_la_ventana_queda_fuera_de_ventana()
    {
        // Hoy + 2 meses + 1 día: domingo 3 de enero de 2027, abierto por una custom para que solo falle la ventana.
        var cita = Agendada();
        var nuevo = Local(new DateOnly(2027, 1, 3), 10, 0);

        var rechazo = cita.Reprogramar(Rango(nuevo), Reglas(nuevo, [Tramo.Crear(new TimeOnly(10, 0), new TimeOnly(11, 0))]), Ahora);

        Assert.Equal(RechazoAlReprogramar.FueraDeVentana, rechazo);
    }

    [Fact]
    public void Reprogramar_a_un_momento_pasado_se_rechaza()
    {
        var cita = Agendada();
        var nuevo = Local(Hoy, 11, 0);

        Assert.Equal(RechazoAlReprogramar.EnElPasado, cita.Reprogramar(Rango(nuevo), Reglas(nuevo), Ahora));
    }

    [Theory]
    [InlineData(EstadoCita.Cancelada)]
    [InlineData(EstadoCita.NoSePresento)]
    public void Una_Cita_que_no_esta_Agendada_no_se_reprograma(EstadoCita estado)
    {
        var cita = Agendada();
        typeof(Cita).GetProperty(nameof(Cita.Estado))!.SetValue(cita, estado);
        var nuevo = Local(Jueves, 17, 0);

        Assert.Equal(RechazoAlReprogramar.CitaNoAgendada, cita.Reprogramar(Rango(nuevo), Reglas(nuevo), Ahora));
        Assert.Equal(Manana16, cita.Rango.Inicio);
    }

    [Fact]
    public void Reprogramar_no_aplica_el_tope_semanal()
    {
        // Con tope 1 y la Cita ya en la semana, moverla dentro de la misma semana sigue pudiéndose (RN-15).
        var cita = Agendada();
        var nuevo = Local(Jueves, 17, 0);

        Assert.Null(cita.Reprogramar(Rango(nuevo), Reglas(nuevo, topeSemanal: 1), Ahora));
    }

    [Fact]
    public void Reprogramar_con_tramos_vigentes_de_otra_fecha_es_un_error_del_llamador()
    {
        var cita = Agendada();
        var nuevo = Local(Jueves, 17, 0);

        Assert.Throws<ArgumentException>(() => cita.Reprogramar(Rango(nuevo), Reglas(Manana16), Ahora));
    }

    // Cita.Cancelar: la Cita Agendada el martes 3 a las 16:00 local (22:00 UTC), con umbral de 120. RN-16, RN-31, INV-C8.

    [Fact]
    public void Cancelar_con_tiempo_es_por_el_Paciente_y_fija_estado_motivo_e_instante()
    {
        var cita = Agendada();
        var tresHorasAntes = Manana16.AddHours(-3);

        Assert.Null(cita.Cancelar(tresHorasAntes, Reglas(Manana16)));

        Assert.Equal(EstadoCita.Cancelada, cita.Estado);
        Assert.Equal(MotivoDeCancelacion.Paciente, cita.MotivoDeCancelacion);
        Assert.Equal(tresHorasAntes, cita.CanceladaEn);
        Assert.Equal(TimeSpan.Zero, cita.CanceladaEn!.Value.Offset);
        Assert.Equal(Manana16, cita.Rango.Inicio);
        Assert.Equal(Manana16.AddMinutes(30), cita.Rango.Fin);
    }

    [Fact]
    public void Cancelar_justo_en_el_umbral_todavia_es_por_el_Paciente()
    {
        var cita = Agendada();

        Assert.Null(cita.Cancelar(Manana16.AddMinutes(-120), Reglas(Manana16)));
        Assert.Equal(MotivoDeCancelacion.Paciente, cita.MotivoDeCancelacion);
    }

    [Theory]
    [InlineData(119)]
    [InlineData(60)]
    [InlineData(1)]
    public void Cancelar_dentro_del_umbral_es_tardia_y_se_permite(int minutosAntes)
    {
        var cita = Agendada();
        var ahora = Manana16.AddMinutes(-minutosAntes);

        Assert.Null(cita.Cancelar(ahora, Reglas(Manana16)));
        Assert.Equal(EstadoCita.Cancelada, cita.Estado);
        Assert.Equal(MotivoDeCancelacion.Tarde, cita.MotivoDeCancelacion);
        Assert.Equal(ahora, cita.CanceladaEn);
    }

    [Fact]
    public void El_umbral_de_la_cancelacion_tardia_se_lee_de_las_reglas()
    {
        // Con un umbral de 60, a 90 minutos todavía es a tiempo.
        var cita = Agendada();

        Assert.Null(cita.Cancelar(Manana16.AddMinutes(-90), Reglas(Manana16, umbralMin: 60)));
        Assert.Equal(MotivoDeCancelacion.Paciente, cita.MotivoDeCancelacion);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Una_Cita_que_ya_empezo_no_se_cancela_y_no_cambia_nada(int minutosDespues)
    {
        var cita = Agendada();

        Assert.Equal(RechazoAlCancelar.YaEmpezo, cita.Cancelar(Manana16.AddMinutes(minutosDespues), Reglas(Manana16)));
        Assert.Equal(EstadoCita.Agendada, cita.Estado);
        Assert.Null(cita.MotivoDeCancelacion);
        Assert.Null(cita.CanceladaEn);
    }

    [Theory]
    [InlineData(EstadoCita.Cancelada)]
    [InlineData(EstadoCita.NoSePresento)]
    public void Una_Cita_que_no_esta_Agendada_no_se_cancela(EstadoCita estado)
    {
        var cita = Agendada();
        typeof(Cita).GetProperty(nameof(Cita.Estado))!.SetValue(cita, estado);

        Assert.Equal(RechazoAlCancelar.CitaNoAgendada, cita.Cancelar(Manana16.AddHours(-3), Reglas(Manana16)));
        Assert.Equal(estado, cita.Estado);
        Assert.Null(cita.MotivoDeCancelacion);
        Assert.Null(cita.CanceladaEn);
    }

    [Fact]
    public void Cancelar_dos_veces_no_cambia_lo_guardado_la_primera()
    {
        var cita = Agendada();
        var primera = Manana16.AddHours(-3);

        Assert.Null(cita.Cancelar(primera, Reglas(Manana16)));
        Assert.Equal(RechazoAlCancelar.CitaNoAgendada, cita.Cancelar(Manana16.AddMinutes(-30), Reglas(Manana16)));
        Assert.Equal(MotivoDeCancelacion.Paciente, cita.MotivoDeCancelacion);
        Assert.Equal(primera, cita.CanceladaEn);
    }

    [Fact]
    public void Una_Cita_cancelada_ya_no_se_reprograma()
    {
        var cita = Agendada();
        Assert.Null(cita.Cancelar(Ahora, Reglas(Manana16)));
        var nuevo = Local(Jueves, 17, 0);

        Assert.Equal(RechazoAlReprogramar.CitaNoAgendada, cita.Reprogramar(Rango(nuevo), Reglas(nuevo), Ahora));
    }

    private static Cita Agendada()
    {
        Assert.Null(Agendar(Manana16, Reglas(Manana16), out var cita));
        return cita!;
    }

    private static RangoDeTiempo Rango(DateTimeOffset inicio) => RangoDeTiempo.Crear(inicio, inicio.AddMinutes(30));

    private static DateTimeOffset Local(DateOnly fecha, int hora, int minuto) => HoraLocal.AUtc(fecha, new TimeOnly(hora, minuto));

    private static RechazoAlAgendar? Agendar(DateTimeOffset inicio, ReglasDeAgenda reglas, out Cita? cita, int citasDeLaSemana = 0) =>
        Cita.Agendar(Guid.CreateVersion7(), PatientId.Nuevo(), Tipo(), inicio, "Dolor", Consentimiento(), reglas, citasDeLaSemana, Ahora, Clave, out cita);

    /// <summary>
    /// Las reglas sembradas con los tramos vigentes de la fecha local de <paramref name="inicio"/>: los de la base
    /// (lunes a viernes, 15:30 a 18:00) o, si se dan, los de una custom de esa fecha.
    /// </summary>
    private static ReglasDeAgenda Reglas(
        DateTimeOffset inicio,
        Tramo[]? custom = null,
        int anticipacionMinimaDias = 1,
        int topeSemanal = 3,
        int umbralMin = 120)
    {
        var fecha = HoraLocal.DesdeUtc(inicio).Fecha;
        var media = TimeSpan.FromMinutes(30);
        var agenda = AgendaAtencion.Crear();
        foreach (var dia in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            Assert.Null(agenda.CambiarBase(dia, [Tramo.Crear(new TimeOnly(15, 30), new TimeOnly(18, 0))], media));
        }

        if (custom is not null)
        {
            Assert.Null(agenda.RegistrarCustom(fecha, custom, "custom", media));
        }

        Assert.Null(ReglasDeReserva.Crear(anticipacionMinimaDias, 2, umbralMin, topeSemanal, out var reserva));
        return ReglasDeAgenda.Crear(agenda.TramosVigentes(fecha), reserva!);
    }

    private static TipoDeVisita Tipo() => TipoDeVisita.Crear("Primera consulta", 30, reservableEnLinea: true, orden: 1);

    private static Consentimiento Consentimiento() =>
        Agenda.Domain.Consentimiento.Crear(Ahora, "2026-11-borrador", CanalDeConsentimiento.EnLinea);
}
