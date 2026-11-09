using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Envía un mensaje una sola vez por evento (AD-3), con el orden que fijó <see cref="ConfirmacionAlAgendar"/>:
/// <list type="number">
/// <item>Abre la transacción de Notificaciones.</item>
/// <item>Si ya hay un <c>mensaje</c> de ese tipo con ese evento, no hace nada.</item>
/// <item>Pide el mensaje al handler; si no hay (por ejemplo, el Paciente no tiene correo), no envía ni registra nada.</item>
/// <item>Registra <see cref="EstadoDeMensaje.Enviado"/>, envía y confirma. El registro va antes del envío: si otra
/// entrega del mismo evento lo registró antes, el índice único lo rechaza y este no envía.</item>
/// <item>Si el canal lanza, la transacción se deshace y la excepción sale: el outbox reintenta.</item>
/// </list>
/// Cada handler decide si envía y qué mensaje arma. No registra nada: ni la dirección, ni el nombre, ni el contenido
/// van a registros o trazas (AD-23).
/// </summary>
public sealed class EnvioUnico(
    ITransaccionDeNotificaciones transaccion,
    IMensajes mensajes,
    INotificationChannel canal,
    IClock reloj)
{
    /// <summary>Envía el mensaje que da <paramref name="armar"/>, si hay, una sola vez por evento y tipo.</summary>
    /// <param name="eventoId">El evento que lo origina.</param>
    /// <param name="citaId">La Cita de la que habla.</param>
    /// <param name="tipo">Qué mensaje es.</param>
    /// <param name="armar">El mensaje, o nulo si no hay que enviar nada. Corre dentro de la transacción, después de
    /// revisar que no se haya enviado.</param>
    /// <param name="cancellationToken">Cancela hasta antes de enviar; la confirmación no se cancela.</param>
    public async Task EnviarAsync(
        Guid eventoId,
        CitaId citaId,
        TipoDeMensaje tipo,
        Func<CancellationToken, Task<MensajeDeCorreo?>> armar,
        CancellationToken cancellationToken)
    {
        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        if (await mensajes.ExisteAsync(eventoId, tipo, cancellationToken))
        {
            return;
        }

        if (await armar(cancellationToken) is not { } mensaje)
        {
            return;
        }

        await mensajes.RegistrarAsync(
            new MensajeRegistrado(
                Guid.CreateVersion7(),
                eventoId,
                citaId,
                tipo,
                CanalDeMensaje.Correo,
                EstadoDeMensaje.Enviado,
                reloj.UtcNow),
            cancellationToken);

        await canal.EnviarAsync(mensaje, cancellationToken);

        // El correo ya salió: lo que falta es anotarlo, aunque se cancele la entrega. Si se cancelara aquí, el
        // siguiente intento lo enviaría otra vez.
        await tx.ConfirmarAsync(CancellationToken.None);
    }
}
