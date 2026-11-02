using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// Un espacio reservado en la agenda para un Paciente. Protege su tiempo, su estado y las reglas para crearla. Que no
/// se traslape con otra no es invariante suya: lo garantiza la base de datos (RN-1, AD-8).
/// </summary>
public sealed class Cita
{
    // Para la reconstitución desde la base (T-6).
    private Cita()
    {
        Rango = null!;
        Consentimiento = null!;
    }

    public CitaId Id { get; private set; }

    /// <summary>Solo el id: el nombre y el contacto viven en Pacientes (E-9, E-12).</summary>
    public PatientId PacienteId { get; private set; }

    public TipoDeVisitaId TipoDeVisitaId { get; private set; }

    /// <summary><c>[Inicio, Fin)</c> en UTC. El fin se fija al crear y no se recalcula (INV-C3).</summary>
    public RangoDeTiempo Rango { get; private set; }

    public EstadoCita Estado { get; private set; }

    /// <summary>Solo en una Cita cancelada (INV-C8).</summary>
    public MotivoDeCancelacion? MotivoDeCancelacion { get; private set; }

    /// <summary>Solo en una Cita cancelada (INV-C8).</summary>
    public DateTimeOffset? CanceladaEn { get; private set; }

    public OrigenDeCita Origen { get; private set; }

    /// <summary>Solo en una Cita del consultorio.</summary>
    public string? Nota { get; private set; }

    /// <summary>Dato de salud (AD-23): nunca en eventos, registros ni telemetría. Obligatorio en línea (INV-C11).</summary>
    public string? MotivoDeConsulta { get; private set; }

    /// <summary>Desde que se crea (INV-C9).</summary>
    public Consentimiento Consentimiento { get; private set; }

    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>
    /// Agenda una Cita en línea: la crea el Paciente desde el sitio. Cumple INV-C1 a C4, C7, C9 a C11. Los tramos
    /// vigentes, la rejilla y la ventana (INV-C5) y el tope semanal (RN-18) llegan después como parámetros nuevos de
    /// este mismo método.
    /// </summary>
    /// <param name="paciente">El Paciente, ya reconocido o registrado por Pacientes.</param>
    /// <param name="tipo">El tipo de visita: fija la duración, y por lo tanto el fin (INV-C3).</param>
    /// <param name="inicio">El instante de inicio.</param>
    /// <param name="motivoDeConsulta">Obligatorio (INV-C11).</param>
    /// <param name="consentimiento">El Consentimiento dado en línea (INV-C9).</param>
    /// <param name="now">El instante actual, del reloj del llamador (T-1, T-20).</param>
    /// <param name="cita">La Cita, si no hubo rechazo.</param>
    /// <returns>Nulo si se agendó; si no, por qué no.</returns>
    /// <exception cref="ArgumentException">Si falta un dato que el llamador debía haber validado antes.</exception>
    public static RechazoAlAgendar? Agendar(
        PatientId paciente,
        TipoDeVisita tipo,
        DateTimeOffset inicio,
        string motivoDeConsulta,
        Consentimiento consentimiento,
        DateTimeOffset now,
        out Cita? cita)
    {
        // INV-C1, C9, C11: datos que la validación de forma ya exigió. Faltar uno aquí es un error de programación.
        ArgumentNullException.ThrowIfNull(tipo);
        ArgumentNullException.ThrowIfNull(consentimiento);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivoDeConsulta);
        if (paciente == default || paciente.Valor == Guid.Empty)
        {
            throw new ArgumentException("La Cita lleva un Paciente.", nameof(paciente));
        }

        if (consentimiento.Canal != CanalDeConsentimiento.EnLinea)
        {
            throw new ArgumentException("Una Cita en línea lleva un Consentimiento dado en línea.", nameof(consentimiento));
        }

        cita = null;

        if (!tipo.SeAgendaEnLinea)
        {
            return RechazoAlAgendar.TipoNoReservable;
        }

        // INV-C4: nunca en un momento pasado. "Ya pasó" se mide desde el inicio (RN-31).
        if (inicio <= now)
        {
            return RechazoAlAgendar.EnElPasado;
        }

        cita = new Cita
        {
            Id = CitaId.Nuevo(),
            PacienteId = paciente,
            TipoDeVisitaId = tipo.Id,
            Rango = RangoDeTiempo.Crear(inicio, inicio + tipo.Duracion),
            Estado = EstadoCita.Agendada,
            Origen = OrigenDeCita.EnLinea,
            MotivoDeConsulta = motivoDeConsulta.Trim(),
            Consentimiento = consentimiento,
            CreadaEn = now.ToUniversalTime(),
        };

        return null;
    }
}

/// <summary>Por qué <see cref="Cita.Agendar"/> no agendó.</summary>
public enum RechazoAlAgendar
{
    /// <summary>El tipo de visita no está activo o no se agenda en línea.</summary>
    TipoNoReservable = 1,

    /// <summary>El inicio ya pasó (INV-C4).</summary>
    EnElPasado = 2,
}
