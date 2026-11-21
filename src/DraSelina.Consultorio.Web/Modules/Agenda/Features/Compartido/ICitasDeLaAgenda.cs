using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de la Agenda del Administrador (FR-14): las Citas de un rango en todos sus estados, a diferencia de
/// <see cref="ICitasDelDia"/>, que solo da las Agendadas. Sin rastrear y sin transacción. No registra lo que lee (AD-23).
/// </summary>
public interface ICitasDeLaAgenda
{
    /// <summary>Las Citas que empiezan en <c>[desde, hasta)</c>, en UTC, en cualquier estado, en orden de inicio.</summary>
    Task<IReadOnlyList<CitaLeidaDeLaAgenda>> EntreAsync(DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken);
}

/// <summary>Una Cita como la lee <see cref="ICitasDeLaAgenda"/>. El motivo y la nota nunca van a registros (AD-23).</summary>
/// <param name="Inicio">En UTC.</param>
/// <param name="Fin">En UTC.</param>
public sealed record CitaLeidaDeLaAgenda(
    CitaId Id,
    PatientId PacienteId,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    EstadoCita Estado,
    MotivoDeCancelacion? MotivoDeCancelacion,
    OrigenDeCita Origen,
    string? Nota,
    string? MotivoDeConsulta)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append(
            $"Id = {Id}, PacienteId = {PacienteId}, Inicio = {Inicio:O}, Fin = {Fin:O}, Estado = {Estado}, "
            + $"MotivoDeCancelacion = {MotivoDeCancelacion}, Origen = {Origen}, Nota = oculta, MotivoDeConsulta = oculto");
        return true;
    }
}
