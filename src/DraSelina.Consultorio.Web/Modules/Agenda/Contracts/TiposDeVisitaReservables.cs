using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Los tipos de visita que el Paciente puede elegir en el sitio: activos y reservables en línea, en su orden (FR-1).
/// Sin precios. Lista vacía si hoy no hay ninguno.
/// </summary>
public sealed record TiposDeVisitaReservables : IQuery<IReadOnlyList<TipoDeVisitaReservable>>;

/// <summary>Un tipo de visita como lo ve el Paciente al elegir, en pantalla "Tipo de consulta".</summary>
/// <param name="Id">El id que viaja en la cadena de consulta (<c>tipo</c>).</param>
/// <param name="Nombre">"Primera consulta".</param>
/// <param name="Descripcion">El texto bajo el nombre; puede no tener.</param>
/// <param name="DuracionMin">En minutos.</param>
public sealed record TipoDeVisitaReservable(TipoDeVisitaId Id, string Nombre, string? Descripcion, int DuracionMin);
