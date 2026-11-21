using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>Las reglas de reserva guardadas. Una consulta: lee la única fila y no cambia nada.</summary>
public sealed class ReglasDeReservaVigentesHandler(IReglasDeReserva reglas) : IQueryHandler<ReglasDeReservaVigentes, ReglasDeReservaValores>
{
    public async Task<Resultado<ReglasDeReservaValores>> HandleAsync(ReglasDeReservaVigentes consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Resultado.Exito(ValoresDeReglasDeReserva.De(await reglas.ObtenerAsync(cancellationToken)));
    }
}
