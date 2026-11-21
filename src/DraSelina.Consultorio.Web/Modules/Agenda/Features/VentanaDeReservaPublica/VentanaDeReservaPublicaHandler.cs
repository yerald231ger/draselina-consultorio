using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>La ventana de reserva de las reglas vigentes. Una consulta: no abre transacción ni cambia estado (T-14).</summary>
public sealed class VentanaDeReservaPublicaHandler(IReglasDeReserva reglasDeReserva)
    : IQueryHandler<VentanaDeReservaPublica, VentanaDeReservaVigente>
{
    public async Task<Resultado<VentanaDeReservaVigente>> HandleAsync(
        VentanaDeReservaPublica consulta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var reglas = await reglasDeReserva.ObtenerAsync(cancellationToken);
        return Resultado.Exito(new VentanaDeReservaVigente(reglas.AnticipacionMinimaDias, reglas.VentanaMeses));
    }
}
