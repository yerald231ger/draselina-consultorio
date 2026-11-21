namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// El canal por el que Notificaciones envía sus mensajes (AD-20). Hoy, correo por SMTP
/// (<c>Infrastructure/CanalDeCorreoSmtp</c>), hacia Mailpit en local. Azure Communication Services también acepta SMTP,
/// pero al desplegar el canal necesitaría autenticarse, y eso queda fuera por ahora. Si no pudo
/// enviar, lanza: el handler deshace su transacción y el outbox reintenta (AD-3).
/// </summary>
public interface INotificationChannel
{
    /// <summary>Envía <paramref name="mensaje"/>. Lanza si el servidor no lo recibió.</summary>
    Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken cancellationToken);
}

/// <summary>
/// Un correo listo para enviar. Lleva la dirección y el contenido del Paciente: nunca va a registros, trazas ni a
/// <c>notificaciones.mensaje</c> (AD-23); <see cref="ToString"/> no los muestra.
/// </summary>
/// <param name="Destinatario">La dirección de correo.</param>
/// <param name="Asunto">El asunto.</param>
/// <param name="Html">La versión HTML.</param>
/// <param name="Texto">La versión de texto plano, legible por sí sola.</param>
/// <param name="Adjuntos">Los archivos adjuntos; vacía si no hay.</param>
public sealed record MensajeDeCorreo(
    string Destinatario,
    string Asunto,
    string Html,
    string Texto,
    IReadOnlyList<AdjuntoDeCorreo> Adjuntos)
{
    public override string ToString() => $"MensajeDeCorreo(oculto, {Adjuntos.Count} adjuntos)";
}

/// <summary>Un archivo adjunto a un correo. <see cref="ToString"/> no muestra su contenido (AD-23).</summary>
/// <param name="NombreDeArchivo">Con su extensión: <c>cita.ics</c>.</param>
/// <param name="TipoDeContenido">El tipo MIME, con su juego de caracteres si es texto.</param>
/// <param name="Contenido">Los bytes del archivo.</param>
public sealed record AdjuntoDeCorreo(string NombreDeArchivo, string TipoDeContenido, byte[] Contenido)
{
    public override string ToString() => $"AdjuntoDeCorreo({NombreDeArchivo}, {TipoDeContenido}, oculto)";
}
