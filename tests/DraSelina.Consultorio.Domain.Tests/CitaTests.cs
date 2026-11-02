using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>
/// <see cref="Cita.Agendar"/>: INV-C1 a C4, C7, C9 a C11, sin base de datos. INV-C5 y el tope semanal llegan con
/// las entradas 4 y 7 de la épica.
/// </summary>
public class CitaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Manana16 = new(2026, 11, 3, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Agendar_crea_una_Cita_Agendada_en_linea_con_su_fin_fijado_por_la_duracion()
    {
        var paciente = PatientId.Nuevo();
        var tipo = TipoDeVisita.Crear("Primera consulta", 30, reservableEnLinea: true, orden: 1);

        var rechazo = Cita.Agendar(paciente, tipo, Manana16, "Dolor de rodilla", Consentimiento(), Ahora, out var cita);

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
    }

    [Fact]
    public void La_Cita_lleva_su_Consentimiento_desde_que_se_crea()
    {
        var consentimiento = Consentimiento();

        Cita.Agendar(PatientId.Nuevo(), Tipo(), Manana16, "Dolor", consentimiento, Ahora, out var cita);

        Assert.Same(consentimiento, cita!.Consentimiento);
        Assert.Equal(CanalDeConsentimiento.EnLinea, cita.Consentimiento.Canal);
        Assert.Equal("2026-11-borrador", cita.Consentimiento.VersionDelAviso);
        Assert.Equal(Ahora, cita.Consentimiento.En);
    }

    [Fact]
    public void Los_instantes_quedan_en_UTC_aunque_lleguen_con_otra_zona()
    {
        var local = new DateTimeOffset(2026, 11, 3, 16, 0, 0, TimeSpan.FromHours(-6));

        Cita.Agendar(PatientId.Nuevo(), Tipo(), local, "Dolor", Consentimiento(), Ahora, out var cita);

        Assert.Equal(TimeSpan.Zero, cita!.Rango.Inicio.Offset);
        Assert.Equal(Manana16, cita.Rango.Inicio);
    }

    [Fact]
    public void Una_Cita_en_el_pasado_se_rechaza()
    {
        var rechazo = Cita.Agendar(PatientId.Nuevo(), Tipo(), Ahora.AddMinutes(-30), "Dolor", Consentimiento(), Ahora, out var cita);

        Assert.Equal(RechazoAlAgendar.EnElPasado, rechazo);
        Assert.Null(cita);
    }

    [Fact]
    public void Una_Cita_que_empieza_justo_ahora_ya_paso()
    {
        // RN-31: "ya pasó" se mide desde el inicio.
        var rechazo = Cita.Agendar(PatientId.Nuevo(), Tipo(), Ahora, "Dolor", Consentimiento(), Ahora, out _);

        Assert.Equal(RechazoAlAgendar.EnElPasado, rechazo);
    }

    [Fact]
    public void Un_tipo_que_no_se_reserva_en_linea_se_rechaza()
    {
        var tipo = TipoDeVisita.Crear("Cirugía menor", 60, reservableEnLinea: false, orden: 3);

        var rechazo = Cita.Agendar(PatientId.Nuevo(), tipo, Manana16, "Dolor", Consentimiento(), Ahora, out var cita);

        Assert.Equal(RechazoAlAgendar.TipoNoReservable, rechazo);
        Assert.Null(cita);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_motivo_de_consulta_no_hay_Cita_en_linea(string motivo)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Cita.Agendar(PatientId.Nuevo(), Tipo(), Manana16, motivo, Consentimiento(), Ahora, out _));
    }

    [Fact]
    public void Sin_paciente_no_hay_Cita()
    {
        Assert.Throws<ArgumentException>(
            () => Cita.Agendar(default, Tipo(), Manana16, "Dolor", Consentimiento(), Ahora, out _));
    }

    [Fact]
    public void Sin_Consentimiento_no_hay_Cita()
    {
        Assert.Throws<ArgumentNullException>(
            () => Cita.Agendar(PatientId.Nuevo(), Tipo(), Manana16, "Dolor", null!, Ahora, out _));
    }

    [Fact]
    public void Una_Cita_en_linea_no_lleva_un_Consentimiento_externo()
    {
        var externo = Agenda.Domain.Consentimiento.Crear(Ahora, "2026-11-borrador", CanalDeConsentimiento.Externo);

        Assert.Throws<ArgumentException>(
            () => Cita.Agendar(PatientId.Nuevo(), Tipo(), Manana16, "Dolor", externo, Ahora, out _));
    }

    [Fact]
    public void El_Consentimiento_lleva_version_del_aviso()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Agenda.Domain.Consentimiento.Crear(Ahora, " ", CanalDeConsentimiento.EnLinea));
    }

    private static TipoDeVisita Tipo() => TipoDeVisita.Crear("Primera consulta", 30, reservableEnLinea: true, orden: 1);

    private static Consentimiento Consentimiento() =>
        Agenda.Domain.Consentimiento.Crear(Ahora, "2026-11-borrador", CanalDeConsentimiento.EnLinea);
}
