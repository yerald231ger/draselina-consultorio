using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// La política P-1 (AD-6, RN-10): cuando guardar, cambiar o borrar una Agenda custom trae Citas elegidas para cancelar,
/// cancela cada una en su propia transacción (T-5) con <see cref="CancelacionPorConsultorio"/>, el mismo camino que el
/// comando del Administrador, con motivo consultorio y a nombre del actor que viene en el evento, nunca como Sistema
/// (T-31). Es idempotente (T-29): la que ya no está Agendada, ya empezó (RN-31) o no existe se omite sin bitácora ni
/// evento, y la ya cancelada por el Consultorio en una entrega anterior también. Si una cancelación lanza, las ya
/// confirmadas quedan y el evento se reintenta: esas se omiten la siguiente vez.
/// </summary>
public sealed class CancelarCitasElegidas(
    ITransaccionDeAgenda transaccion,
    CancelacionPorConsultorio cancelacion,
    IClock reloj)
    : IEventHandler<AgendaCustomFueRegistrada>, IEventHandler<AgendaCustomFueCambiada>, IEventHandler<AgendaCustomFueBorrada>
{
    public Task HandleAsync(AgendaCustomFueRegistrada evento, CancellationToken cancellationToken) =>
        CancelarAsync(evento, cancellationToken);

    public Task HandleAsync(AgendaCustomFueCambiada evento, CancellationToken cancellationToken) =>
        CancelarAsync(evento, cancellationToken);

    public Task HandleAsync(AgendaCustomFueBorrada evento, CancellationToken cancellationToken) =>
        CancelarAsync(evento, cancellationToken);

    private async Task CancelarAsync(IEventoDeAgendaCustom evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        foreach (var id in evento.CitasACancelar)
        {
            await using var tx = await transaccion.AbrirAsync(cancellationToken);
            var hecha = await cancelacion.CancelarAsync(id, evento.Actor, evento.ActorId, reloj.UtcNow, cancellationToken);
            if (hecha.Resultado == ResultadoDeCancelacion.Cancelada)
            {
                await tx.ConfirmarAsync(cancellationToken);
            }
            else
            {
                await tx.DeshacerAsync(cancellationToken);
            }
        }
    }
}
