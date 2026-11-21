using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Los tipos de visita que el Paciente puede elegir. Una consulta: no abre transacción ni cambia estado (T-14).
/// </summary>
public sealed class TiposDeVisitaReservablesHandler(ITiposDeVisitaReservables reservables)
    : IQueryHandler<TiposDeVisitaReservables, IReadOnlyList<TipoDeVisitaReservable>>
{
    public async Task<Resultado<IReadOnlyList<TipoDeVisitaReservable>>> HandleAsync(
        TiposDeVisitaReservables consulta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Resultado.Exito(await reservables.ListarAsync(cancellationToken));
    }
}
