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

    /// <summary><c>[Inicio, Fin)</c> en UTC. El fin se fija al crear o reprogramar y no se recalcula (INV-C3).</summary>
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

    /// <summary>
    /// Agenda una Cita del consultorio: la registra el Administrador, por teléfono o WhatsApp, o porque Selina la aceptó
    /// fuera de horario (CAP-13, FR-15). No aplica Agenda base ni custom, rejilla, ventana ni tope semanal, ni en un día
    /// que no se atiende (RN-13, AD-7): cualquier tipo de visita activo, a cualquier hora. Cumple INV-C1 a C4, C6 y C9.
    /// Lo único que la impide, además del pasado, es traslapar otra Cita, y eso lo decide la base (AD-8). Si no cabe
    /// completa en ningún Tramo vigente de su fecha, la nota es obligatoria (INV-C6); los Tramos los pasa el llamador
    /// (T-1). Sin solicitud (AD-27 es solo en línea); con Enlace de gestión, como la en línea (AD-19).
    /// </summary>
    /// <param name="paciente">El Paciente, ya reconocido o registrado por Pacientes.</param>
    /// <param name="tipo">El tipo de visita: fija la duración, y por lo tanto el fin (INV-C3). Reservable en línea o no.</param>
    /// <param name="inicio">El instante de inicio; cualquier hora, fuera de la rejilla incluso.</param>
    /// <param name="nota">La nota del Administrador. Obligatoria fuera de horario (INV-C6); vacía cuenta como no escrita.</param>
    /// <param name="motivoDeConsulta">Opcional; vacío cuenta como no escrito.</param>
    /// <param name="consentimiento">El Consentimiento obtenido por fuera del sitio (AD-24, INV-C9).</param>
    /// <param name="tramosVigentes">Los Tramos vigentes de la fecha local de <paramref name="inicio"/> (T-1).</param>
    /// <param name="now">El instante actual, del reloj del llamador (T-1, T-20).</param>
    /// <param name="clave">La clave del host con la que se deriva su Enlace de gestión (AD-19).</param>
    /// <param name="cita">La Cita, si no hubo rechazo.</param>
    /// <returns>Nulo si se agendó; si no, por qué no.</returns>
    /// <exception cref="ArgumentException">Si falta un dato que el llamador debía haber validado antes, si el
    /// Consentimiento no es externo o si los Tramos vigentes no son de la fecha local de la Cita.</exception>
    public static RechazoAlAgendar? AgendarPorConsultorio(
        PatientId paciente,
        TipoDeVisita tipo,
        DateTimeOffset inicio,
        string? nota,
        string? motivoDeConsulta,
        Consentimiento consentimiento,
        TramosVigentes tramosVigentes,
        DateTimeOffset now,
        ClaveDeGestion clave,
        out Cita? cita)
    {
        // INV-C1, C9: datos que la validación de forma ya exigió. Faltar uno aquí es un error de programación.
        ArgumentNullException.ThrowIfNull(tipo);
        ArgumentNullException.ThrowIfNull(consentimiento);
        ArgumentNullException.ThrowIfNull(tramosVigentes);
        ArgumentNullException.ThrowIfNull(clave);
        if (paciente == default || paciente.Valor == Guid.Empty)
        {
            throw new ArgumentException("La Cita lleva un Paciente.", nameof(paciente));
        }

        if (consentimiento.Canal != CanalDeConsentimiento.Externo)
        {
            throw new ArgumentException("Una Cita del consultorio lleva un Consentimiento externo.", nameof(consentimiento));
        }

        // Los tramos vigentes son los de la fecha local de la Cita (AD-17); otros serían un error del llamador.
        var fecha = HoraLocal.DesdeUtc(inicio).Fecha;
        if (tramosVigentes.Fecha != fecha)
        {
            throw new ArgumentException(
                $"Los tramos vigentes son del {tramosVigentes.Fecha:yyyy-MM-dd} y la Cita es del {fecha:yyyy-MM-dd}.",
                nameof(tramosVigentes));
        }

        cita = null;

        // Reservable en línea o no: el consultorio agenda cualquier tipo activo (FR-15).
        if (!tipo.Activo)
        {
            return RechazoAlAgendar.TipoNoReservable;
        }

        // INV-C4: nunca en un momento pasado. "Ya pasó" se mide desde el inicio (RN-31). Hoy más tarde sí.
        if (inicio <= now)
        {
            return RechazoAlAgendar.EnElPasado;
        }

        var rango = RangoDeTiempo.Crear(inicio, inicio + tipo.Duracion);
        var notaEscrita = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();

        // INV-C6: fuera de los Tramos vigentes de su fecha, solo con nota.
        if (notaEscrita is null && QuedaFueraDe(rango, tramosVigentes))
        {
            return RechazoAlAgendar.NotaObligatoria;
        }

        var id = CitaId.Nuevo();
        cita = new Cita
        {
            Id = id,
            PacienteId = paciente,
            TipoDeVisitaId = tipo.Id,
            Rango = rango,
            Estado = EstadoCita.Agendada,
            Origen = OrigenDeCita.Consultorio,
            Nota = notaEscrita,
            MotivoDeConsulta = string.IsNullOrWhiteSpace(motivoDeConsulta) ? null : motivoDeConsulta.Trim(),
            Consentimiento = consentimiento,
            CreadaEn = now.ToUniversalTime(),
            SolicitudId = null,
            TokenHash = EnlaceDeGestion.Derivar(clave, id).Hash,
        };

        return null;
    }

    /// <summary>
    /// Si la Cita no cabe completa en ningún Tramo de <paramref name="tramosVigentes"/>, los de la fecha local de su
    /// inicio (INV-C6). Una que cruza la medianoche local nunca cabe.
    /// </summary>
    public bool QuedaFueraDeHorario(TramosVigentes tramosVigentes)
    {
        ArgumentNullException.ThrowIfNull(tramosVigentes);
        return QuedaFueraDe(Rango, tramosVigentes);
    }

    private static bool QuedaFueraDe(RangoDeTiempo rango, TramosVigentes tramosVigentes)
    {
        var (fechaInicio, horaInicio) = HoraLocal.DesdeUtc(rango.Inicio);
        var (fechaFin, horaFin) = HoraLocal.DesdeUtc(rango.Fin);
        return fechaInicio != fechaFin || !tramosVigentes.Tramos.Any(t => t.Contiene(horaInicio, horaFin));
    }

    /// <summary>
    /// Mueve la Cita a <paramref name="nuevoRango"/>: es la misma Cita, con su id, su Enlace de gestión, su origen, su
    /// solicitud, su Consentimiento y su motivo (RN-15, AD-7). Solo si está Agendada y faltan al menos
    /// <see cref="ReglasDeAgenda.UmbralCancelacionTardiaMin"/> minutos para su inicio actual (RN-17). Sobre el rango
    /// nuevo cumple INV-C4 e INV-C5 con la rejilla y la ventana de <see cref="CalculadoraDeDisponibilidad"/> (T-21). No
    /// aplica el tope semanal (RN-15) ni revisa si el tipo se agenda en línea. El fin lo fija el llamador con la
    /// duración de hoy del tipo de visita (INV-C3): la rejilla se mide con la duración del rango.
    /// </summary>
    /// <param name="nuevoRango">El rango nuevo, en UTC.</param>
    /// <param name="reglas">Los tramos vigentes de la fecha local del inicio nuevo y los valores de las reglas de
    /// reserva (T-1).</param>
    /// <param name="now">El instante actual, del reloj del llamador (T-1, T-20).</param>
    /// <returns>Nulo si se reprogramó; si no, por qué no, y la Cita queda como estaba.</returns>
    /// <exception cref="ArgumentException">Si los tramos vigentes no son de la fecha local del inicio nuevo.</exception>
    public RechazoAlReprogramar? Reprogramar(RangoDeTiempo nuevoRango, ReglasDeAgenda reglas, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(nuevoRango);
        ArgumentNullException.ThrowIfNull(reglas);

        // Los tramos vigentes son los de la fecha local del inicio nuevo (AD-17); otros serían un error del llamador.
        var (fecha, hora) = HoraLocal.DesdeUtc(nuevoRango.Inicio);
        if (reglas.TramosVigentes.Fecha != fecha)
        {
            throw new ArgumentException(
                $"Los tramos vigentes son del {reglas.TramosVigentes.Fecha:yyyy-MM-dd} y el inicio nuevo es del {fecha:yyyy-MM-dd}.",
                nameof(reglas));
        }

        if (Estado != EstadoCita.Agendada)
        {
            return RechazoAlReprogramar.CitaNoAgendada;
        }

        // RN-17: el mismo umbral de la cancelación tardía. Con 120, a 120 minutos todavía se puede; a 119, ya no.
        if (Rango.Inicio - now < TimeSpan.FromMinutes(reglas.UmbralCancelacionTardiaMin))
        {
            return RechazoAlReprogramar.ReprogramacionCerrada;
        }

        // INV-C4: nunca en un momento pasado. "Ya pasó" se mide desde el inicio (RN-31).
        if (nuevoRango.Inicio <= now)
        {
            return RechazoAlReprogramar.EnElPasado;
        }

        // INV-C5: dentro de la ventana de reserva, la misma que ofrece la calculadora (T-21).
        if (!CalculadoraDeDisponibilidad.Ventana(reglas, now).Contiene(fecha))
        {
            return RechazoAlReprogramar.FueraDeVentana;
        }

        // INV-C5: cabe completa en un tramo vigente y empieza en su rejilla, la misma de la calculadora (T-21).
        if (!CalculadoraDeDisponibilidad.EstaEnLaRejilla(reglas.TramosVigentes, hora, nuevoRango.Fin - nuevoRango.Inicio))
        {
            return RechazoAlReprogramar.FueraDeHorario;
        }

        Rango = nuevoRango;
        return null;
    }

    /// <summary>
    /// Cancela la Cita por el Paciente (CAP-8, FR-10). Solo si está Agendada y su inicio no ha pasado (RN-16, RN-31):
    /// antes del inicio nunca se impide. Si faltan menos de <see cref="ReglasDeAgenda.UmbralCancelacionTardiaMin"/>
    /// minutos, el motivo es <see cref="Domain.MotivoDeCancelacion.Tarde"/>; si no, <see cref="Domain.MotivoDeCancelacion.Paciente"/>.
    /// Con exactamente el umbral, <see cref="Domain.MotivoDeCancelacion.Paciente"/>, el mismo corte de
    /// <see cref="Reprogramar"/> (RN-17). Fija el estado, el motivo y el instante de la cancelación (INV-C8); el rango no
    /// cambia, y la restricción de exclusión deja de contarla (AD-8).
    /// </summary>
    /// <param name="now">El instante actual, del reloj del llamador (T-1, T-20).</param>
    /// <param name="reglas">Los valores de las reglas de reserva: de ellas sale el umbral (T-1).</param>
    /// <returns>Nulo si se canceló; si no, por qué no, y la Cita queda como estaba.</returns>
    public RechazoAlCancelar? Cancelar(DateTimeOffset now, ReglasDeAgenda reglas)
    {
        ArgumentNullException.ThrowIfNull(reglas);

        if (Estado != EstadoCita.Agendada)
        {
            return RechazoAlCancelar.CitaNoAgendada;
        }

        // RN-31: "ya pasó" se mide desde el inicio. En el inicio mismo, ya no se cancela.
        if (Rango.Inicio <= now)
        {
            return RechazoAlCancelar.YaEmpezo;
        }

        // RN-16, RN-17: con 120, a 120 minutos es a tiempo; a 119, tardía.
        MotivoDeCancelacion = Rango.Inicio - now < TimeSpan.FromMinutes(reglas.UmbralCancelacionTardiaMin)
            ? Domain.MotivoDeCancelacion.Tarde
            : Domain.MotivoDeCancelacion.Paciente;
        Estado = EstadoCita.Cancelada;
        CanceladaEn = now.ToUniversalTime();
        return null;
    }
}

/// <summary>Por qué <see cref="Cita.Cancelar"/> no canceló.</summary>
public enum RechazoAlCancelar
{
    /// <summary>La Cita ya está Cancelada o No se presentó.</summary>
    CitaNoAgendada = 1,

    /// <summary>El inicio de la Cita ya llegó o pasó (RN-31).</summary>
    YaEmpezo = 2,
}

/// <summary>Por qué <see cref="Cita.Reprogramar"/> no reprogramó.</summary>
public enum RechazoAlReprogramar
{
    /// <summary>La Cita está Cancelada o No se presentó.</summary>
    CitaNoAgendada = 1,

    /// <summary>Falta menos que el umbral de cancelación tardía para su inicio actual (RN-17).</summary>
    ReprogramacionCerrada = 2,

    /// <summary>El inicio nuevo ya pasó (INV-C4).</summary>
    EnElPasado = 3,

    /// <summary>El rango nuevo no cabe completo en un tramo vigente de su fecha o no empieza en la rejilla (INV-C5).</summary>
    FueraDeHorario = 4,

    /// <summary>El inicio nuevo cae antes de la anticipación mínima o después de la ventana de reserva (INV-C5).</summary>
    FueraDeVentana = 5,
}

/// <summary>Por qué <see cref="Cita.Agendar"/> o <see cref="Cita.AgendarPorConsultorio"/> no agendó.</summary>
public enum RechazoAlAgendar
{
    /// <summary>El tipo de visita no está activo o, en línea, no se agenda en línea.</summary>
    TipoNoReservable = 1,

    /// <summary>El inicio ya pasó (INV-C4).</summary>
    EnElPasado = 2,

    /// <summary>No cabe completa en un tramo vigente de su fecha o no empieza en la rejilla (INV-C5).</summary>
    FueraDeHorario = 3,

    /// <summary>Cae antes de la anticipación mínima o después de la ventana de reserva (INV-C5).</summary>
    FueraDeVentana = 4,

    /// <summary>Las Citas de la semana con ese teléfono ya alcanzaron el tope semanal (RN-18).</summary>
    TopeSemanal = 5,

    /// <summary>Una Cita del consultorio fuera de los Tramos vigentes de su fecha va con nota (INV-C6).</summary>
    NotaObligatoria = 6,
}
