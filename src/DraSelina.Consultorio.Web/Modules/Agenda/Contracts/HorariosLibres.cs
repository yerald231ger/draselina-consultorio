using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Los horarios que el Paciente puede reservar de un tipo de visita, por fecha, dentro de la ventana de reserva. Se
/// calculan en cada consulta; nunca se guardan (RN-3). Un tipo inexistente o que no se agenda en línea vuelve como
/// <c>Fallo(</c><see cref="CodigosDeAgenda.TipoNoReservable"/><c>)</c>.
/// </summary>
/// <param name="TipoDeVisitaId">El tipo de visita que eligió el Paciente.</param>
public sealed record HorariosLibres(TipoDeVisitaId TipoDeVisitaId) : IQuery<IReadOnlyList<DiaConHorarios>>;

/// <summary>
/// Una fecha de la ventana en la que Selina atiende, con sus horas libres. Lista vacía: la fecha está llena. Las fechas
/// en las que no se atiende no vienen.
/// </summary>
/// <param name="Fecha">Fecha local del consultorio (AD-17).</param>
/// <param name="Horas">Horas locales de inicio, en orden.</param>
public sealed record DiaConHorarios(DateOnly Fecha, IReadOnlyList<TimeOnly> Horas);
