using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// El <c>mensaje</c> <see cref="TipoDeMensaje.Recordatorio"/> <see cref="EstadoDeMensaje.Omitido"/> de un evento cuyo
/// Recordatorio ya no alcanzaba a salir (FR-22). Una sola vez por evento: si ya está, no hace nada.
/// </summary>
internal static class OmitidoDelRecordatorio
{
    internal static async Task RegistrarAsync(
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
