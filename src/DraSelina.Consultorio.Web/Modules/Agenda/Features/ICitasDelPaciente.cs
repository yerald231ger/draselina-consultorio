using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de las Citas de un Paciente para "Agendar cita" (entrada 5.4): su próxima Cita Agendada y la última
/// que ya empezó. Sin rastrear y sin transacción. No registra lo que lee (AD-23).
/// </summary>
public interface ICitasDelPaciente
{
    /// <summary>
    /// La Cita Agendada del Paciente con el inicio más cercano después de <paramref name="ahora"/>, y la de inicio más
    /// reciente en o antes de <paramref name="ahora"/>, en cualquier estado. Cada una nula si no hay.
    /// </summary>
    Task<(CitaLeidaDelPaciente? Proxima, CitaLeidaDelPaciente? Anterior)> ProximaYAnteriorAsync(
        PatientId paciente, DateTimeOffset ahora, CancellationToken cancellationToken);
}

/// <summary>Una Cita como la lee <see cref="ICitasDelPaciente"/>.</summary>
/// <param name="Inicio">En UTC.</param>
/// <param name="TipoDeVisita">El nombre de su tipo; nulo si el tipo ya no existe.</param>
public sealed record CitaLeidaDelPaciente(
    DateTimeOffset Inicio, string? TipoDeVisita, EstadoCita Estado, MotivoDeCancelacion? MotivoDeCancelacion);
