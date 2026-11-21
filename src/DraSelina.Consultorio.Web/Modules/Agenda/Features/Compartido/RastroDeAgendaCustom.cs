using System.Globalization;
using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Lo que dejan en la bitácora y en el outbox <see cref="GuardarAgendaCustomHandler"/> y
/// <see cref="BorrarAgendaCustomHandler"/>: el detalle de una custom y el evento interno para la política P-1.
/// </summary>
internal static class RastroDeAgendaCustom
{
    /// <summary>El motivo y los Tramos de <paramref name="custom"/>, para el antes y el después de la bitácora.</summary>
    internal static object Detalle(AgendaCustom custom) => new
    {
        motivo = custom.Motivo,
        tramos = custom.Tramos.Select(t => new
        {
            inicio = t.Inicio.ToString("HH:mm", CultureInfo.InvariantCulture),
            fin = t.Fin.ToString("HH:mm", CultureInfo.InvariantCulture),
        }),
    };

    /// <summary>Guarda el evento interno en el outbox con su tipo concreto: el nombre del tipo es su columna <c>tipo</c>.</summary>
    internal static Task PublicarAsync(IOutboxDeAgenda outbox, IEventoDeAgendaCustom evento, CancellationToken cancellationToken) =>
        evento switch
        {
            AgendaCustomFueRegistrada e => outbox.PublicarAsync(e, e.EventoId, e.EventoId, e.OcurridoEn, cancellationToken),
            AgendaCustomFueCambiada e => outbox.PublicarAsync(e, e.EventoId, e.EventoId, e.OcurridoEn, cancellationToken),
            AgendaCustomFueBorrada e => outbox.PublicarAsync(e, e.EventoId, e.EventoId, e.OcurridoEn, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(evento), evento.GetType().Name, "Evento de Agenda custom desconocido."),
        };
}
