using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de las Citas de un Paciente: para "Agendar cita" (entrada 5.4), su próxima Cita Agendada y la última
/// que ya empezó; para su historial (entrada 5.7), todas. Sin rastrear y sin transacción. No registra lo que lee (AD-23).
/// </summary>
public interface ICitasDelPaciente
{
    /// <summary>
    /// La Cita Agendada del Paciente con el inicio más cercano después de <paramref name="ahora"/>, y la de inicio más
    /// reciente en o antes de <paramref name="ahora"/>, en cualquier estado. Cada una nula si no hay.
    /// </summary>
    Task<(CitaLeidaDelPaciente? Proxima, CitaLeidaDelPaciente? Anterior)> ProximaYAnteriorAsync(
        PatientId paciente, DateTimeOffset ahora, CancellationToken cancellationToken);

    /// <summary>Todas las Citas del Paciente, en cualquier estado, de la de inicio más reciente a la más antigua.</summary>
    Task<IReadOnlyList<CitaLeidaDelPaciente>> TodasAsync(PatientId paciente, CancellationToken cancellationToken);
}

/// <summary>Una Cita como la lee <see cref="ICitasDelPaciente"/>.</summary>
/// <param name="Id">La Cita.</param>
/// <param name="Inicio">En UTC.</param>
/// <param name="TipoDeVisita">El nombre de su tipo; nulo si el tipo ya no existe.</param>
/// <param name="Origen">Quién la creó.</param>
public sealed record CitaLeidaDelPaciente(
    CitaId Id,
    DateTimeOffset Inicio,
    string? TipoDeVisita,
    OrigenDeCita Origen,
    EstadoCita Estado,
    MotivoDeCancelacion? MotivoDeCancelacion);
