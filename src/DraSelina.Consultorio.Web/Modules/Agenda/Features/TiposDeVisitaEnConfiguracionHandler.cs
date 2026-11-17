using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>Los Tipos de visita activos para Configuración. Una consulta: solo lee.</summary>
public sealed class TiposDeVisitaEnConfiguracionHandler(ITiposDeVisitaEditables tipos)
    : IQueryHandler<TiposDeVisitaEnConfiguracion, IReadOnlyList<TipoDeVisitaEditable>>
{
    public async Task<Resultado<IReadOnlyList<TipoDeVisitaEditable>>> HandleAsync(
        TiposDeVisitaEnConfiguracion consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Resultado.Exito(await tipos.ListarAsync(cancellationToken));
    }
}
