using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// La semana de atención del sitio: la misma semana tipo que <see cref="SemanaTipoHandler"/>, con <see cref="SemanaTipoGuardada"/>, sin pedir sesión. Una
/// consulta: no abre transacción ni cambia estado (T-14).
/// </summary>
public sealed class SemanaDeAtencionHandler(IAgendaDelRango agenda)
    : IQueryHandler<SemanaDeAtencion, IReadOnlyList<DiaDeLaBase>>
{
    public async Task<Resultado<IReadOnlyList<DiaDeLaBase>>> HandleAsync(
        SemanaDeAtencion consulta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Resultado.Exito(await SemanaTipoGuardada.LeerAsync(agenda, cancellationToken));
    }
}
