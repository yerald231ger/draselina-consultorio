using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Los eventos internos de Agenda que deja guardar, cambiar o borrar una Agenda custom cuando el Administrador eligió
/// cancelar alguna Cita (AD-6, RN-10). Los arma el handler del comando (Gerardo, 2026-11-17) y van al outbox en la misma
/// transacción que la custom (T-30); no salen de Agenda (T-23): solo los escucha la política P-1
/// (<see cref="CancelarCitasElegidas"/>). Sin datos del Paciente (AD-23).
/// </summary>
public interface IEventoDeAgendaCustom
{
    Guid EventoId { get; }

    DateTimeOffset OcurridoEn { get; }

    /// <summary>La fecha local de la custom.</summary>
    DateOnly Fecha { get; }

    /// <summary>Las Citas que el Administrador eligió cancelar.</summary>
    IReadOnlyList<CitaId> CitasACancelar { get; }

    /// <summary>Quién decidió: el actor de cada cancelación (T-31).</summary>
    Actor Actor { get; }

    /// <summary>El id de la cuenta del Administrador, para la bitácora de cada cancelación (RN-24).</summary>
    string? ActorId { get; }
}

/// <summary>Se registró la Agenda custom de una fecha con Citas elegidas para cancelar.</summary>
public sealed record AgendaCustomFueRegistrada(
    Guid EventoId, DateTimeOffset OcurridoEn, DateOnly Fecha, IReadOnlyList<CitaId> CitasACancelar, Actor Actor, string? ActorId)
    : IEventoDeAgendaCustom;

/// <summary>Se cambió la Agenda custom de una fecha con Citas elegidas para cancelar.</summary>
public sealed record AgendaCustomFueCambiada(
    Guid EventoId, DateTimeOffset OcurridoEn, DateOnly Fecha, IReadOnlyList<CitaId> CitasACancelar, Actor Actor, string? ActorId)
    : IEventoDeAgendaCustom;

/// <summary>Se borró la Agenda custom de una fecha con Citas elegidas para cancelar.</summary>
public sealed record AgendaCustomFueBorrada(
    Guid EventoId, DateTimeOffset OcurridoEn, DateOnly Fecha, IReadOnlyList<CitaId> CitasACancelar, Actor Actor, string? ActorId)
    : IEventoDeAgendaCustom;
