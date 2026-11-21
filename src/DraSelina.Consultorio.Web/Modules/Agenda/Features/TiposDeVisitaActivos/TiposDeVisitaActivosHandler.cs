using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Los tipos de visita que el Administrador puede agendar. Una consulta: no abre transacción ni cambia estado (T-14).
/// </summary>
public sealed class TiposDeVisitaActivosHandler(ITiposDeVisitaActivos activos)
    : IQueryHandler<TiposDeVisitaActivos, IReadOnlyList<TipoDeVisitaActivo>>
{
    public async Task<Resultado<IReadOnlyList<TipoDeVisitaActivo>>> HandleAsync(
        TiposDeVisitaActivos consulta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Resultado.Exito(await activos.ListarAsync(cancellationToken));
    }
}
