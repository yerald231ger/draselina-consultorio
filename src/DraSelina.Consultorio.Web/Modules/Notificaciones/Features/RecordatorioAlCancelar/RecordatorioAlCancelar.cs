using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Con <see cref="CitaCancelada"/>, borra el Recordatorio de la Cita, si tiene (FR-22; T-25, AD-20). Idempotente: si
/// el evento llega dos veces, la segunda no encuentra nada que borrar.
/// </summary>
public sealed class RecordatorioAlCancelar(
    ITransaccionDeNotificaciones transaccion,
    IRecordatorios recordatorios) : IEventHandler<CitaCancelada>
{
    public async Task HandleAsync(CitaCancelada evento, CancellationToken cancellationToken)
    {
        await using var tx = await transaccion.AbrirAsync(cancellationToken);
        await recordatorios.BorrarAsync(evento.CitaId, cancellationToken);
        await tx.ConfirmarAsync(cancellationToken);
    }
}
