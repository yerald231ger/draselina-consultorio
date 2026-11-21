using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Los valores de unas reglas de reserva, como los devuelven <see cref="ReglasDeReservaVigentesHandler"/> y
/// <see cref="CambiarReglasDeReservaHandler"/>.
/// </summary>
internal static class ValoresDeReglasDeReserva
{
    /// <summary>Los cuatro valores de unas reglas.</summary>
    internal static ReglasDeReservaValores De(ReglasDeReserva reglas) =>
        new(reglas.AnticipacionMinimaDias, reglas.VentanaMeses, reglas.UmbralCancelacionTardiaMin, reglas.TopeSemanalPorTelefono);
}
