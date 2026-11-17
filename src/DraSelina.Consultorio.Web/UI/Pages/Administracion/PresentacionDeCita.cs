using DraSelina.Consultorio.Agenda.Contracts;

namespace DraSelina.Consultorio.UI.Pages.Administracion;

/// <summary>
/// Cómo se presenta una Cita en las pantallas del Administrador: la etiqueta de su estado, la clase de su chip, la
/// etiqueta "sin correo" y el teléfono del Paciente. Una sola fuente para la Agenda, el día de la Agenda, el Detalle de
/// Cita, Agendar cita, Pacientes y el historial del Paciente.
/// </summary>
public static class PresentacionDeCita
{
    /// <summary>El Paciente no tiene correo guardado: hay que avisarle a mano (RN-30, EXPERIENCE.md).</summary>
    public const string SinCorreo = "sin correo";

    /// <summary>La etiqueta del estado de una Cita en la Agenda.</summary>
    public static string Etiqueta(CitaDeLaAgenda cita)
    {
        ArgumentNullException.ThrowIfNull(cita);
        return Etiqueta(cita.Estado, cita.Cancelacion);
    }

    /// <summary>La etiqueta de un estado mostrado, con su cancelación; la misma en la Agenda y en Agendar cita.</summary>
    public static string Etiqueta(EstadoEnLaAgenda estado, CancelacionEnLaAgenda? cancelacion) => (estado, cancelacion) switch
    {
        (EstadoEnLaAgenda.Agendada, _) => "Agendada",
        (EstadoEnLaAgenda.Atendida, _) => "Atendida",
        (EstadoEnLaAgenda.NoSePresento, _) => "No se presentó",
        (EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.Tardia) => "Cancelada (tardía)",
        (EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElConsultorio) => "Cancelada (por el Consultorio)",
        (EstadoEnLaAgenda.Cancelada, _) => "Cancelada (por el Paciente)",
        _ => estado.ToString(),
    };

    // DESIGN.md: el ámbar (warning) es solo de la cancelación tardía y el rojo (danger) solo de la acción de cancelar;
    // las demás cancelaciones y No se presentó van en un chip neutro.
    internal static string ClaseDeEtiqueta(CitaDeLaAgenda cita)
    {
        ArgumentNullException.ThrowIfNull(cita);
        return ClaseDeEtiqueta(cita.Estado, cita.Cancelacion);
    }

    /// <summary>La clase del chip de un estado mostrado, con su cancelación; la misma en la Agenda y en el Detalle de Cita.</summary>
    internal static string ClaseDeEtiqueta(EstadoEnLaAgenda estado, CancelacionEnLaAgenda? cancelacion) => (estado, cancelacion) switch
    {
        (EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.Tardia) => "dsm-chip-de-estado dsm-chip-de-estado--tardia",
        (EstadoEnLaAgenda.Cancelada or EstadoEnLaAgenda.NoSePresento, _) => "dsm-chip-de-estado dsm-chip-de-estado--neutro",
        _ => "dsm-chip-de-estado",
    };

    /// <summary>"444 123 4567" de un celular de México en E.164; cualquier otra forma, tal cual.</summary>
    public static string Telefono(string e164) =>
        e164 is { Length: 13 } && e164.StartsWith("+52", StringComparison.Ordinal)
            ? $"{e164[3..6]} {e164[6..9]} {e164[9..]}"
            : e164;
}
