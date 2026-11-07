using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de la disponibilidad y del tope semanal (T-17): los rangos de las Citas no canceladas, de
/// cualquier origen, que ocupan algo de un periodo, y el conteo de las Agendadas de unos Pacientes. No pasa por el repositorio de la Cita ni rastrea lo que lee.
/// </summary>
public interface ICitasActivas
{
    /// <summary>
    /// Los rangos <c>[inicio, fin)</c> de las Citas no canceladas que traslapan <c>[desde, hasta)</c>, en UTC, sin la
    /// Cita <paramref name="aparte"/> si se da (la que se reprograma no ocupa sus propios horarios).
    /// </summary>
    Task<IReadOnlyList<RangoDeTiempo>> EntreAsync(
        DateTimeOffset desde,
        DateTimeOffset hasta,
        CitaId? aparte,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cuántas Citas <see cref="EstadoCita.Agendada"/> de esos Pacientes, de cualquier origen, empiezan en
    /// <c>[desde, hasta)</c>, en UTC. Para el tope semanal (RN-18): las canceladas no cuentan.
    /// </summary>
    Task<int> AgendadasDeAsync(
        IReadOnlyCollection<PatientId> pacientes,
        DateTimeOffset desde,
        DateTimeOffset hasta,
        CancellationToken cancellationToken);
}
