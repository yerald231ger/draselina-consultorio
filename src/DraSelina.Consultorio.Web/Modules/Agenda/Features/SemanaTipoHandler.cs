using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// La semana tipo guardada. Una consulta: lee la base completa con <see cref="IAgendaDelRango"/> (sin custom: el rango es
/// un solo día cualquiera) y la devuelve de lunes a domingo con <see cref="Domain.AgendaAtencion.TramosBase"/>.
/// </summary>
public sealed class SemanaTipoHandler(IAgendaDelRango agenda) : IQueryHandler<SemanaTipo, IReadOnlyList<DiaDeLaBase>>
{
    /// <summary>De lunes a domingo, como se muestra la semana.</summary>
    public static readonly IReadOnlyList<DayOfWeek> DeLunesADomingo =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    public async Task<Resultado<IReadOnlyList<DiaDeLaBase>>> HandleAsync(SemanaTipo consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Resultado.Exito(await LeerAsync(agenda, cancellationToken));
    }

    /// <summary>
    /// La semana tipo de lunes a domingo, sin custom: el rango es un solo día cualquiera. La comparte
    /// <see cref="HorarioDeAtencionHandler"/>, la lectura pública del sitio.
    /// </summary>
    internal static async Task<IReadOnlyList<DiaDeLaBase>> LeerAsync(IAgendaDelRango agenda, CancellationToken cancellationToken)
    {
        var atencion = await agenda.LeerAsync(DateOnly.MinValue, DateOnly.MinValue, cancellationToken);
        return
        [
            .. DeLunesADomingo.Select(dia => new DiaDeLaBase(
                dia, [.. atencion.TramosBase(dia).Select(t => new TramoDeLaAgenda(t.Inicio, t.Fin))])),
        ];
    }
}
