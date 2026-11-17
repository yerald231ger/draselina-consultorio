using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>Las reglas de reserva guardadas, para la pantalla de Configuración (CAP-16, AD-14). Solo el Administrador. Solo lee.</summary>
public sealed record ReglasDeReservaVigentes : IQuery<ReglasDeReservaValores>, IRequiereAdministrador;

/// <summary>Los cuatro valores de <c>reglas_de_reserva</c>.</summary>
/// <param name="AnticipacionMinimaDias">Hoy 1: desde mañana.</param>
/// <param name="VentanaMeses">Hoy 2.</param>
/// <param name="UmbralCancelacionTardiaMin">Hoy 120.</param>
/// <param name="TopeSemanalPorTelefono">Hoy 3.</param>
public sealed record ReglasDeReservaValores(int AnticipacionMinimaDias, int VentanaMeses, int UmbralCancelacionTardiaMin, int TopeSemanalPorTelefono);
