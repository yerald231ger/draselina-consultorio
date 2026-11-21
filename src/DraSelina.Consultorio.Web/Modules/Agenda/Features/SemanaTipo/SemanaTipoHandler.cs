using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// La semana tipo guardada. Una consulta: lee la base completa con <see cref="IAgendaDelRango"/> (sin custom: el rango es
/// un solo día cualquiera) y la devuelve de lunes a domingo con <see cref="SemanaTipoGuardada"/>.
/// </summary>
public sealed class SemanaTipoHandler(IAgendaDelRango agenda) : IQueryHandler<SemanaTipo, IReadOnlyList<DiaDeLaBase>>
{
    public async Task<Resultado<IReadOnlyList<DiaDeLaBase>>> HandleAsync(SemanaTipo consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Resultado.Exito(await SemanaTipoGuardada.LeerAsync(agenda, cancellationToken));
    }
}
