using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El horario de atención del sitio: la misma semana tipo que <see cref="SemanaTipoHandler"/>, sin pedir sesión. Una
/// consulta: no abre transacción ni cambia estado (T-14).
/// </summary>
public sealed class HorarioDeAtencionHandler(IAgendaDelRango agenda)
    : IQueryHandler<HorarioDeAtencion, IReadOnlyList<DiaDeLaBase>>
{
    public async Task<Resultado<IReadOnlyList<DiaDeLaBase>>> HandleAsync(
        HorarioDeAtencion consulta,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Resultado.Exito(await SemanaTipoHandler.LeerAsync(agenda, cancellationToken));
    }
}
