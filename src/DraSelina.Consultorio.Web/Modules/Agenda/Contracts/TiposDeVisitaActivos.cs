using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Los tipos de visita que el Administrador puede elegir al agendar una Cita del consultorio (FR-15, entrada 5.4): todos
/// los activos, reservables en línea o no, en su orden. A diferencia de <see cref="TiposDeVisitaReservables"/>, que da
/// solo los que el Paciente agenda en el sitio. Solo el Administrador (AD-22). Lista vacía si no hay ninguno.
/// </summary>
public sealed record TiposDeVisitaActivos : IQuery<IReadOnlyList<TipoDeVisitaActivo>>, IRequiereAdministrador;

/// <summary>Un tipo de visita activo, como lo elige el Administrador.</summary>
/// <param name="Id">El tipo.</param>
/// <param name="Nombre">"Primera consulta".</param>
/// <param name="DuracionMin">En minutos.</param>
/// <param name="ReservableEnLinea">Si el Paciente también lo agenda en el sitio.</param>
public sealed record TipoDeVisitaActivo(TipoDeVisitaId Id, string Nombre, int DuracionMin, bool ReservableEnLinea);
