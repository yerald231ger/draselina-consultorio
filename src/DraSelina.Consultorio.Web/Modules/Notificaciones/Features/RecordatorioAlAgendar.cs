using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Con <see cref="CitaAgendada"/>, programa el Recordatorio de la Cita (CAP-18, FR-22; T-25, AD-20), igual para las
/// Citas en línea que para las del Consultorio. Idempotente: el evento puede llegar dos veces (AD-3).
/// <list type="bullet">
/// <item>Si la hora del Recordatorio (<see cref="HoraDelRecordatorio"/>) es posterior a
/// <see cref="CitaAgendada.OcurridoEn"/>, guarda la fila; si la Cita ya tiene una, no hace nada.</item>
/// <item>Si no, no hay fila: registra un <c>mensaje</c> <see cref="TipoDeMensaje.Recordatorio"/>
/// <see cref="EstadoDeMensaje.Omitido"/> con el <see cref="CitaAgendada.EventoId"/>, una sola vez.</item>
/// </list>
/// Si ya salió un Recordatorio de la Cita después de <see cref="CitaAgendada.OcurridoEn"/> (un reintento tardío del
/// evento), no hace nada. No pide contacto: el correo se busca al enviar.
/// </summary>
public sealed class RecordatorioAlAgendar(
    ITransaccionDeNotificaciones transaccion,
    IRecordatorios recordatorios,
    IMensajes mensajes,
    IClock reloj) : IEventHandler<CitaAgendada>
{
    public async Task HandleAsync(CitaAgendada evento, CancellationToken cancellationToken)
    {
        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // Un reintento del evento que llega cuando su Recordatorio ya salió: no hay nada que programar.
        if (await mensajes.EnviadoDesdeAsync(evento.CitaId, TipoDeMensaje.Recordatorio, evento.OcurridoEn, cancellationToken))
        {
            return;
        }

        var hora = await recordatorios.HoraDelRecordatorioAsync(cancellationToken);
        var programadoPara = HoraDelRecordatorio.ProgramadoPara(evento.Inicio, hora);

        if (programadoPara > evento.OcurridoEn)
        {
            await recordatorios.ProgramarAsync(
                new RecordatorioProgramado(evento.CitaId, evento.PatientId, evento.Inicio, programadoPara), cancellationToken);
        }
        else
        {
            await OmitidoDelRecordatorio.RegistrarAsync(mensajes, reloj, evento.EventoId, evento.CitaId, cancellationToken);
        }

        await tx.ConfirmarAsync(cancellationToken);
    }
}

/// <summary>
/// El <c>mensaje</c> <see cref="TipoDeMensaje.Recordatorio"/> <see cref="EstadoDeMensaje.Omitido"/> de un evento cuyo
/// Recordatorio ya no alcanzaba a salir (FR-22). Una sola vez por evento: si ya está, no hace nada.
/// </summary>
internal static class OmitidoDelRecordatorio
{
    public static async Task RegistrarAsync(
        IMensajes mensajes, IClock reloj, Guid eventoId, CitaId citaId, CancellationToken cancellationToken)
    {
        if (await mensajes.ExisteAsync(eventoId, TipoDeMensaje.Recordatorio, cancellationToken))
        {
            return;
        }

        await mensajes.RegistrarAsync(
            new MensajeRegistrado(
                Guid.CreateVersion7(),
                eventoId,
                citaId,
                TipoDeMensaje.Recordatorio,
                CanalDeMensaje.Correo,
                EstadoDeMensaje.Omitido,
                reloj.UtcNow),
            cancellationToken);
    }
}
