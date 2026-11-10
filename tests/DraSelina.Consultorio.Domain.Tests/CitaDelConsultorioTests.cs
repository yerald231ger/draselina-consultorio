using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>
/// <see cref="Cita.AgendarPorConsultorio"/> sin base de datos (AD-7, RN-13, INV-C3, C4, C6, C9): ignora rejilla,
/// ventana y tope; solo la impiden el pasado y, fuera de los Tramos vigentes, la falta de nota. Los Tramos son los
/// sembrados: lunes a viernes de 15:30 a 18:00. El reloj, el lunes 2 de noviembre de 2026 a las 12:00 local.
/// </summary>
public class CitaDelConsultorioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 11, 2);
    private static readonly DateOnly Martes = new(2026, 11, 3);
    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static readonly ClaveDeGestion Clave = ClaveDeGestion.Crear("clave-de-prueba-de-32-bytes-o-mas"u8);

    [Fact]
    public void Dentro_de_horario_y_sin_nota_se_agenda_del_consultorio_con_su_fin_por_la_duracion()
    {
        var paciente = PatientId.Nuevo();
        var tipo = Tipo();
        var consentimiento = Externo();
        var inicio = Local(Martes, 16, 0);

        var rechazo = Cita.AgendarPorConsultorio(
            paciente, tipo, inicio, nota: null, motivoDeConsulta: null, consentimiento, Tramos(Martes), Ahora, Clave, out var cita);

        Assert.Null(rechazo);
        Assert.NotNull(cita);
        Assert.Equal(7, cita.Id.Valor.Version);
        Assert.Equal(paciente, cita.PacienteId);
        Assert.Equal(tipo.Id, cita.TipoDeVisitaId);
        Assert.Equal(inicio, cita.Rango.Inicio);
        Assert.Equal(inicio.AddMinutes(30), cita.Rango.Fin);
        Assert.Equal(EstadoCita.Agendada, cita.Estado);
        Assert.Equal(OrigenDeCita.Consultorio, cita.Origen);
        Assert.Null(cita.Nota);
        Assert.Null(cita.MotivoDeConsulta);
        Assert.Same(consentimiento, cita.Consentimiento);
        Assert.Equal(Ahora, cita.CreadaEn);
        Assert.Null(cita.SolicitudId);
        Assert.Equal(EnlaceDeGestion.Derivar(Clave, cita.Id).Hash, cita.TokenHash);
        Assert.False(cita.QuedaFueraDeHorario(Tramos(Martes)));
    }

    [Fact]
    public void Fuera_de_la_rejilla_se_agenda()
    {
        var inicio = Local(Martes, 16, 10);

        Assert.Null(Agendar(inicio, null, out var cita));
        Assert.Equal(inicio, cita!.Rango.Inicio);
        Assert.False(cita.QuedaFueraDeHorario(Tramos(Martes)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_sabado_cerrado_sin_nota_pide_la_nota(string? nota)
    {
        Assert.Equal(RechazoAlAgendar.NotaObligatoria, Agendar(Local(Sabado, 10, 15), nota, out var cita));
        Assert.Null(cita);
    }

    [Fact]
    public void Un_sabado_cerrado_con_nota_se_agenda_fuera_de_horario()
    {
        Assert.Null(Agendar(Local(Sabado, 10, 15), "  lo pidió Selina ", out var cita));

        Assert.Equal("lo pidió Selina", cita!.Nota);
        Assert.True(cita.QuedaFueraDeHorario(Tramos(Sabado)));
    }

    [Fact]
    public void Una_Cita_que_cabe_a_medias_en_el_Tramo_pide_la_nota()
    {
        // 17:45 a 18:15: el Tramo termina a las 18:00.
        Assert.Equal(RechazoAlAgendar.NotaObligatoria, Agendar(Local(Martes, 17, 45), null, out _));
    }

    [Fact]
    public void Dentro_de_horario_la_nota_es_opcional_y_se_guarda_si_llega()
    {
        Assert.Null(Agendar(Local(Martes, 16, 0), "Pidió por WhatsApp", out var cita));

        Assert.Equal("Pidió por WhatsApp", cita!.Nota);
    }

    [Fact]
    public void Una_custom_sin_Tramos_deja_el_dia_fuera_de_horario()
    {
        var sinAtencion = new TramosVigentes(Martes, [], OrigenDeTramos.Custom);

        var rechazo = Cita.AgendarPorConsultorio(
            PatientId.Nuevo(), Tipo(), Local(Martes, 16, 0), null, null, Externo(), sinAtencion, Ahora, Clave, out _);

        Assert.Equal(RechazoAlAgendar.NotaObligatoria, rechazo);
    }

    [Fact]
    public void Hoy_mas_tarde_fuera_de_la_ventana_se_agenda()
    {
        // La ventana en línea pide un día de anticipación; el consultorio no.
        Assert.Null(Agendar(Local(Hoy, 19, 0), "Urgencia", out _));
        Assert.Null(Agendar(Local(Hoy, 16, 0), null, out _));
    }

    [Fact]
    public void Despues_de_la_ventana_de_reserva_se_agenda()
    {
        var lejos = new DateOnly(2027, 6, 1);

        Assert.Null(Agendar(Local(lejos, 16, 0), null, out _));
    }

    [Fact]
    public void En_el_pasado_se_rechaza_antes_que_la_nota()
    {
        Assert.Equal(RechazoAlAgendar.EnElPasado, Agendar(Local(Hoy, 11, 0), null, out var cita));
        Assert.Null(cita);
    }

    [Fact]
    public void Una_Cita_que_empieza_justo_ahora_ya_paso()
    {
        // RN-31: "ya pasó" se mide desde el inicio.
        Assert.Equal(RechazoAlAgendar.EnElPasado, Agendar(Ahora, "nota", out _));
    }

    [Fact]
    public void Un_tipo_que_no_se_reserva_en_linea_se_agenda_con_su_duracion()
    {
        var tipo = TipoDeVisita.Crear("Cirugía menor", 60, reservableEnLinea: false, orden: 3);
        var inicio = Local(Martes, 16, 0);

        var rechazo = Cita.AgendarPorConsultorio(
            PatientId.Nuevo(), tipo, inicio, null, null, Externo(), Tramos(Martes), Ahora, Clave, out var cita);

        Assert.Null(rechazo);
        Assert.Equal(inicio.AddMinutes(60), cita!.Rango.Fin);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Dolor de rodilla ", "Dolor de rodilla")]
    public void El_motivo_de_consulta_es_opcional(string? motivo, string? guardado)
    {
        Cita.AgendarPorConsultorio(
            PatientId.Nuevo(), Tipo(), Local(Martes, 16, 0), null, motivo, Externo(), Tramos(Martes), Ahora, Clave, out var cita);

        Assert.Equal(guardado, cita!.MotivoDeConsulta);
    }

    [Fact]
    public void Una_Cita_del_consultorio_no_lleva_un_Consentimiento_en_linea()
    {
        var enLinea = Consentimiento.Crear(Ahora, "2026-11-borrador", CanalDeConsentimiento.EnLinea);

        Assert.Throws<ArgumentException>(() => Cita.AgendarPorConsultorio(
            PatientId.Nuevo(), Tipo(), Local(Martes, 16, 0), null, null, enLinea, Tramos(Martes), Ahora, Clave, out _));
    }

    [Fact]
    public void Sin_Consentimiento_no_hay_Cita()
    {
        Assert.Throws<ArgumentNullException>(() => Cita.AgendarPorConsultorio(
            PatientId.Nuevo(), Tipo(), Local(Martes, 16, 0), null, null, null!, Tramos(Martes), Ahora, Clave, out _));
    }

    [Fact]
    public void Sin_paciente_no_hay_Cita()
    {
        Assert.Throws<ArgumentException>(() => Cita.AgendarPorConsultorio(
            default, Tipo(), Local(Martes, 16, 0), null, null, Externo(), Tramos(Martes), Ahora, Clave, out _));
    }

    [Fact]
    public void Unos_Tramos_vigentes_de_otra_fecha_son_un_error_del_llamador()
    {
        Assert.Throws<ArgumentException>(() => Cita.AgendarPorConsultorio(
            PatientId.Nuevo(), Tipo(), Local(Martes, 16, 0), null, null, Externo(), Tramos(Hoy), Ahora, Clave, out _));
    }

    [Fact]
    public void Una_Cita_que_cruza_la_medianoche_local_queda_fuera_de_horario()
    {
        // Un Tramo que llega hasta la última hora no alcanza a contener una Cita que termina al día siguiente.
        var hastaLaNoche = new TramosVigentes(Martes, [Tramo.Crear(new TimeOnly(20, 0), new TimeOnly(23, 59))], OrigenDeTramos.Custom);

        var rechazo = Cita.AgendarPorConsultorio(
            PatientId.Nuevo(), Tipo(), Local(Martes, 23, 45), null, null, Externo(), hastaLaNoche, Ahora, Clave, out _);

        Assert.Equal(RechazoAlAgendar.NotaObligatoria, rechazo);
    }

    private static RechazoAlAgendar? Agendar(DateTimeOffset inicio, string? nota, out Cita? cita) =>
        Cita.AgendarPorConsultorio(
            PatientId.Nuevo(), Tipo(), inicio, nota, null, Externo(), Tramos(HoraLocal.DesdeUtc(inicio).Fecha), Ahora, Clave, out cita);

    private static DateTimeOffset Local(DateOnly fecha, int hora, int minuto) => HoraLocal.AUtc(fecha, new TimeOnly(hora, minuto));

    /// <summary>Los Tramos vigentes de la base sembrada: lunes a viernes, 15:30 a 18:00; sábado y domingo, ninguno.</summary>
    private static TramosVigentes Tramos(DateOnly fecha)
    {
        var agenda = AgendaAtencion.Crear();
        foreach (var dia in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            Assert.Null(agenda.CambiarBase(dia, [Tramo.Crear(new TimeOnly(15, 30), new TimeOnly(18, 0))], TimeSpan.FromMinutes(30)));
        }

        return agenda.TramosVigentes(fecha);
    }

    private static TipoDeVisita Tipo() => TipoDeVisita.Crear("Primera consulta", 30, reservableEnLinea: true, orden: 1);

    private static Consentimiento Externo() => Consentimiento.Crear(Ahora, "2026-11-borrador", CanalDeConsentimiento.Externo);
}
