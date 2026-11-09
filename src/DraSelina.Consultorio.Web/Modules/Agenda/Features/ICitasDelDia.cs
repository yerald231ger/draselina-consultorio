using DraSelina.Consultorio.Agenda.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de la Agenda del día (FR-25): las Citas Agendadas de un día, de cualquier origen, con su motivo
/// de consulta. Sin rastrear y sin transacción. No registra lo que lee (AD-23).
/// </summary>
public interface ICitasDelDia
{
    /// <summary>
    /// Las Citas <see cref="Domain.EstadoCita.Agendada"/> que empiezan en <c>[desde, hasta)</c>, en UTC, en orden de
    /// inicio, con la hora local de inicio.
    /// </summary>
    Task<IReadOnlyList<CitaDelDia>> AgendadasEntreAsync(DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken);
}
