using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura del archivo de calendario (FR-5): lo que lleva el evento de una Cita, sin rastrear. Busca por el
/// hash del token del Enlace de gestión (AD-19) o por id. Nunca lee motivo de consulta ni datos del Paciente (AD-23).
/// </summary>
public interface ICitasDelCalendario
{
    /// <summary>La Cita cuyo <c>token_hash</c> es <paramref name="tokenHash"/>; nula si no hay.</summary>
    Task<CitaDelCalendario?> PorTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken);

    /// <summary>La Cita con ese id; nula si no hay.</summary>
    Task<CitaDelCalendario?> PorIdAsync(CitaId citaId, CancellationToken cancellationToken);
}

/// <summary>Lo que el archivo de calendario necesita de una Cita.</summary>
/// <param name="Id">La Cita: da el <c>UID</c> del evento y su Enlace de gestión.</param>
/// <param name="Inicio">En UTC.</param>
/// <param name="Fin">En UTC.</param>
/// <param name="Estado">Solo una Cita Agendada entrega el archivo.</param>
/// <param name="TipoDeVisita">El nombre de hoy del tipo de visita.</param>
public sealed record CitaDelCalendario(CitaId Id, DateTimeOffset Inicio, DateTimeOffset Fin, EstadoCita Estado, string TipoDeVisita);
