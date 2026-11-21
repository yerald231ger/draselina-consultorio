using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de Tu cita (FR-8): lo que la página necesita de una Cita, sin rastrear. Busca solo por el hash del
/// token del Enlace de gestión (AD-19). Nunca lee motivo de consulta ni datos de contacto (AD-23).
/// </summary>
public interface ICitasDelEnlace
{
    /// <summary>La Cita cuyo <c>token_hash</c> es <paramref name="tokenHash"/>; nula si no hay.</summary>
    Task<CitaLeidaPorEnlace?> PorTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken);
}

/// <summary>Lo que Tu cita necesita de una Cita.</summary>
/// <param name="Id">Para excluirla de sus propios horarios al reprogramar; nunca va a la interfaz como dirección.</param>
/// <param name="Inicio">En UTC.</param>
/// <param name="Fin">En UTC.</param>
/// <param name="Estado">El estado guardado.</param>
/// <param name="MotivoDeCancelacion">Solo si está Cancelada.</param>
/// <param name="PacienteId">Para pedir el nombre a Pacientes.</param>
/// <param name="TipoDeVisitaId">El tipo de la Cita.</param>
/// <param name="TipoDeVisita">El nombre de hoy del tipo de visita.</param>
public sealed record CitaLeidaPorEnlace(
    CitaId Id,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    EstadoCita Estado,
    MotivoDeCancelacion? MotivoDeCancelacion,
    PatientId PacienteId,
    TipoDeVisitaId TipoDeVisitaId,
    string TipoDeVisita);
