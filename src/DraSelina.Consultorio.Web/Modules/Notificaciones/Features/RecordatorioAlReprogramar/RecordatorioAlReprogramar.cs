using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Con <see cref="CitaReprogramada"/>, mueve el Recordatorio de la Cita a su nuevo inicio (FR-22; T-25, AD-20).
/// Idempotente: el evento puede llegar dos veces (AD-3).
/// <list type="bullet">
/// <item>Recalcula la hora con <see cref="CitaReprogramada.InicioNuevo"/>. Si es posterior a
/// <see cref="CitaReprogramada.OcurridoEn"/>, deja la fila con el nuevo inicio y la nueva hora, la cree o la
/// actualice.</item>
/// <item>Si no, borra la fila y registra un <c>mensaje</c> <see cref="TipoDeMensaje.Recordatorio"/>
/// <see cref="EstadoDeMensaje.Omitido"/> con el <see cref="CitaReprogramada.EventoId"/>, una sola vez.</item>
/// </list>
/// Si ya salió un Recordatorio de la Cita, o se omitió al cambiar la hora del Recordatorio, después de
/// <see cref="CitaReprogramada.OcurridoEn"/> (un reintento tardío del evento), no hace nada.
/// </summary>
public sealed class RecordatorioAlReprogramar(
    ITransaccionDeNotificaciones transaccion,
    IRecordatorios recordatorios,
    IMensajes mensajes,
    IClock reloj) : IEventHandler<CitaReprogramada>
{
    public async Task HandleAsync(CitaReprogramada evento, CancellationToken cancellationToken)
    {
        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // Un reintento del evento que llega cuando su Recordatorio ya salió, o ya se omitió al cambiar la hora del
        // Recordatorio: no hay nada que programar.
        if (await mensajes.CerradoDesdeAsync(evento.CitaId, TipoDeMensaje.Recordatorio, evento.OcurridoEn, cancellationToken))
        {
            return;
        }

        var hora = await recordatorios.HoraDelRecordatorioAsync(cancellationToken);
        var programadoPara = HoraDelRecordatorio.ProgramadoPara(evento.InicioNuevo, hora);

        if (programadoPara > evento.OcurridoEn)
        {
            await recordatorios.MoverAsync(
                new RecordatorioProgramado(evento.CitaId, evento.PatientId, evento.InicioNuevo, programadoPara), cancellationToken);
        }
        else
        {
            await recordatorios.BorrarAsync(evento.CitaId, cancellationToken);
            await OmitidoDelRecordatorio.RegistrarAsync(mensajes, reloj, evento.EventoId, evento.CitaId, cancellationToken);
        }

        await tx.ConfirmarAsync(cancellationToken);
    }
}
