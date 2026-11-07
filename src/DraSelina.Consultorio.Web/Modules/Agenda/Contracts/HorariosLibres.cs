using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Los horarios que el Paciente puede reservar de un tipo de visita, por fecha, dentro de la ventana de reserva. Se
/// calculan en cada consulta; nunca se guardan (RN-3). Un tipo inexistente o que no se agenda en línea vuelve como
/// <c>Fallo(</c><see cref="CodigosDeAgenda.TipoNoReservable"/><c>)</c>.
/// </summary>
/// <param name="TipoDeVisitaId">El tipo de visita que eligió el Paciente.</param>
/// <param name="Aparte">
/// Al reprogramar, la Cita que se mueve: no ocupa sus propios horarios, así su hora y las que traslapa vuelven como
/// libres (Gerardo, 2026-11-06). Nulo al reservar.
/// </param>
public sealed record HorariosLibres(TipoDeVisitaId TipoDeVisitaId, CitaId? Aparte = null) : IQuery<IReadOnlyList<DiaConHorarios>>;

/// <summary>
/// Una fecha de la ventana con algún horario posible del tipo, con sus horas libres. Lista vacía: las Citas ocupan
/// todos sus horarios y la fecha se ve "Lleno". No vienen las fechas en las que no se atiende ni las que no tienen
/// ningún horario posible del tipo (su duración no cabe en los tramos, o hoy ya pasó).
/// </summary>
/// <param name="Fecha">Fecha local del consultorio (AD-17).</param>
/// <param name="Horas">Horas locales de inicio, en orden.</param>
public sealed record DiaConHorarios(DateOnly Fecha, IReadOnlyList<TimeOnly> Horas);
