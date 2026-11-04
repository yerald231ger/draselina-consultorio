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
        TokenHash = null!;
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
    /// La solicitud del formulario que la creó; solo en una Cita en línea. Confirmar dos veces la misma solicitud no
    /// crea otra Cita (AD-27, RN-21): lo garantiza el índice único de la base.
    /// </summary>
    public Guid? SolicitudId { get; private set; }

    /// <summary>
    /// SHA-256 del token de su Enlace de gestión, 32 bytes (AD-19, RN-22). El token no se guarda: se deriva de la
    /// clave del host y del id cada vez que hace falta (<see cref="EnlaceDeGestion.Derivar"/>).
    /// </summary>
    public byte[] TokenHash { get; private set; }

    /// <summary>
    /// Agenda una Cita en línea: la crea el Paciente desde el sitio. Cumple INV-C1 a C5, C7, C9 a C11. La rejilla y la
    /// ventana son las de <see cref="CalculadoraDeDisponibilidad"/> (T-21). Rechaza también si el teléfono ya alcanzó el
    /// tope semanal (RN-18): el conteo lo hace el llamador; la Cita solo compara (T-1).
    /// </summary>
    /// <param name="solicitud">La solicitud del formulario de Tus datos (AD-27).</param>
    /// <param name="paciente">El Paciente, ya reconocido o registrado por Pacientes.</param>
    /// <param name="tipo">El tipo de visita: fija la duración, y por lo tanto el fin (INV-C3).</param>
    /// <param name="inicio">El instante de inicio.</param>
    /// <param name="motivoDeConsulta">Obligatorio (INV-C11).</param>
    /// <param name="consentimiento">El Consentimiento dado en línea (INV-C9).</param>
    /// <param name="reglas">Los tramos vigentes de la fecha local de <paramref name="inicio"/> y los valores de las
    /// reglas de reserva (T-1).</param>
    /// <param name="citasDeLaSemana">Las Citas Agendadas, de cualquier origen, de los Pacientes que comparten el
    /// teléfono, en la semana de calendario local (lunes a domingo) de <paramref name="inicio"/> (RN-18).</param>
    /// <param name="now">El instante actual, del reloj del llamador (T-1, T-20).</param>
    /// <param name="clave">La clave del host con la que se deriva su Enlace de gestión; la Cita guarda solo el hash del
    /// token (AD-19).</param>
    /// <param name="cita">La Cita, si no hubo rechazo.</param>
    /// <returns>Nulo si se agendó; si no, por qué no.</returns>
    /// <exception cref="ArgumentException">Si falta un dato que el llamador debía haber validado antes, o si los
    /// tramos vigentes no son de la fecha local de la Cita.</exception>
    public static RechazoAlAgendar? Agendar(
        Guid solicitud,
        PatientId paciente,
        TipoDeVisita tipo,
        DateTimeOffset inicio,
        string motivoDeConsulta,
        Consentimiento consentimiento,
        ReglasDeAgenda reglas,
        int citasDeLaSemana,
        DateTimeOffset now,
        ClaveDeGestion clave,
        out Cita? cita)
    {
        // INV-C1, C9, C11: datos que la validación de forma ya exigió. Faltar uno aquí es un error de programación.
        ArgumentNullException.ThrowIfNull(tipo);
        ArgumentNullException.ThrowIfNull(consentimiento);
        ArgumentNullException.ThrowIfNull(reglas);
        ArgumentNullException.ThrowIfNull(clave);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivoDeConsulta);
        ArgumentOutOfRangeException.ThrowIfNegative(citasDeLaSemana);
        if (solicitud == Guid.Empty)
        {
            throw new ArgumentException("La Cita en línea lleva su solicitud.", nameof(solicitud));
        }

        if (paciente == default || paciente.Valor == Guid.Empty)
        {
            throw new ArgumentException("La Cita lleva un Paciente.", nameof(paciente));
        }

        if (consentimiento.Canal != CanalDeConsentimiento.EnLinea)
        {
            throw new ArgumentException("Una Cita en línea lleva un Consentimiento dado en línea.", nameof(consentimiento));
        }

        // Los tramos vigentes son los de la fecha local de la Cita (AD-17); otros serían un error del llamador.
        var (fecha, hora) = HoraLocal.DesdeUtc(inicio);
        if (reglas.TramosVigentes.Fecha != fecha)
        {
            throw new ArgumentException(
                $"Los tramos vigentes son del {reglas.TramosVigentes.Fecha:yyyy-MM-dd} y la Cita es del {fecha:yyyy-MM-dd}.",
                nameof(reglas));
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

        // INV-C5: dentro de la ventana de reserva, la misma que ofrece la calculadora (T-21).
        if (!CalculadoraDeDisponibilidad.Ventana(reglas, now).Contiene(fecha))
        {
            return RechazoAlAgendar.FueraDeVentana;
        }

        // INV-C5: cabe completa en un tramo vigente y empieza en su rejilla, la misma de la calculadora (T-21).
        if (!CalculadoraDeDisponibilidad.EstaEnLaRejilla(reglas.TramosVigentes, hora, tipo.Duracion))
        {
            return RechazoAlAgendar.FueraDeHorario;
        }

        // RN-18: con tope 3, la cuarta de la semana se rechaza. Es un límite blando, no transaccional (AD-13).
        if (citasDeLaSemana >= reglas.TopeSemanalPorTelefono)
        {
            return RechazoAlAgendar.TopeSemanal;
        }

        var id = CitaId.Nuevo();
        cita = new Cita
        {
            Id = id,
            PacienteId = paciente,
            TipoDeVisitaId = tipo.Id,
            Rango = RangoDeTiempo.Crear(inicio, inicio + tipo.Duracion),
            Estado = EstadoCita.Agendada,
            Origen = OrigenDeCita.EnLinea,
            MotivoDeConsulta = motivoDeConsulta.Trim(),
            Consentimiento = consentimiento,
            CreadaEn = now.ToUniversalTime(),
            SolicitudId = solicitud,
            TokenHash = EnlaceDeGestion.Derivar(clave, id).Hash,
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

    /// <summary>No cabe completa en un tramo vigente de su fecha o no empieza en la rejilla (INV-C5).</summary>
    FueraDeHorario = 3,

    /// <summary>Cae antes de la anticipación mínima o después de la ventana de reserva (INV-C5).</summary>
    FueraDeVentana = 4,

    /// <summary>Las Citas de la semana con ese teléfono ya alcanzaron el tope semanal (RN-18).</summary>
    TopeSemanal = 5,
}
