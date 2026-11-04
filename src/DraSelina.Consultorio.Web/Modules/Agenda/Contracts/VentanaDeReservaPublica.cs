using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Desde cuándo y hasta cuándo se puede reservar en línea, de las reglas de reserva: la nota del calendario de Día y
/// hora en escritorio ("desde mañana y hasta 2 meses"). Solo lee; los horarios siguen saliendo de
/// <see cref="HorariosLibres"/>.
/// </summary>
public sealed record VentanaDeReservaPublica : IQuery<VentanaDeReservaVigente>;

/// <summary>La ventana de reserva vigente.</summary>
/// <param name="AnticipacionMinimaDias">Días calendario desde hoy antes de los cuales no se reserva: 1 es desde mañana.</param>
/// <param name="VentanaMeses">Meses hacia adelante hasta los que se reserva.</param>
public sealed record VentanaDeReservaVigente(int AnticipacionMinimaDias, int VentanaMeses);
