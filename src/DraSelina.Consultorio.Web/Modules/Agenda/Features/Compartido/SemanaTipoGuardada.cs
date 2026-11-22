using DraSelina.Consultorio.Agenda.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// La semana tipo guardada, de lunes a domingo. La leen <see cref="SemanaTipoHandler"/> (administración) y
/// <see cref="SemanaDeAtencionHandler"/> (la lectura pública del sitio).
/// </summary>
internal static class SemanaTipoGuardada
{
    /// <summary>De lunes a domingo, como se muestra la semana.</summary>
    private static readonly IReadOnlyList<DayOfWeek> DeLunesADomingo =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    /// <summary>
    /// La semana tipo de lunes a domingo con <see cref="Domain.AgendaAtencion.TramosBase"/>, sin custom: el rango es un
    /// solo día cualquiera.
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
