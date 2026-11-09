using System.Data.Common;
using DraSelina.Consultorio.Notificaciones.Features;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>
/// <see cref="INotificationChannel"/> por SMTP, con MailKit. El servidor sale de la cadena de conexión
/// <see cref="CadenaDeConexion"/> que da Aspire (<c>Endpoint=smtp://host:puerto</c>, Mailpit en local); el remitente,
/// de <c>Notificaciones:Remitente</c>. Sin una de las dos, la web arranca igual y lo que falla es el envío: el outbox
/// reintenta. No se autentica: Azure Communication Services también acepta SMTP, pero al desplegar habría que agregar
/// la autenticación (fuera de esta entrada).
/// <para>
/// Si el envío falla, lanza una <see cref="InvalidOperationException"/> que solo dice el tipo de la falla y, si el
/// servidor contestó, su código: la respuesta del servidor puede repetir la dirección del destinatario, y lo que lanza
/// un handler va a los registros (AD-23).
/// </para>
/// </summary>
public sealed class CanalDeCorreoSmtp(IConfiguration configuracion, IOptions<OpcionesDeNotificaciones> opciones) : INotificationChannel
{
    /// <summary>El nombre de la cadena de conexión: el del recurso Mailpit en el AppHost.</summary>
    public const string CadenaDeConexion = "mailpit";

    public async Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        var servidor = Servidor();
        var remitente = Remitente();
        using var cliente = new SmtpClient();

        try
        {
            // Dentro del try: una dirección que MimeKit no acepta tampoco sale en el mensaje de la falla.
            var correo = Armar(mensaje, remitente);
            await cliente.ConnectAsync(
                servidor.Host,
                servidor.Port,
                servidor.Scheme == "smtps" ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable,
                cancellationToken);
            await cliente.SendAsync(correo, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            // Sin la excepción original ni su mensaje (AD-23).
            var codigo = error is SmtpCommandException comando ? $", código {(int)comando.StatusCode}" : "";
            throw new InvalidOperationException($"No se pudo enviar el correo por SMTP ({error.GetType().Name}{codigo}).");
        }

        // El servidor ya aceptó el correo: una falla al despedirse (o el límite del handler) no debe llegar al handler,
        // que desharía su transacción y el outbox lo enviaría otra vez.
        try
        {
            await cliente.DisconnectAsync(quit: true, CancellationToken.None);
        }
        catch (Exception)
        {
            // Nada que hacer: el correo salió.
        }
    }

    private static MimeMessage Armar(MensajeDeCorreo mensaje, MailboxAddress remitente)
    {
        var cuerpo = new BodyBuilder { HtmlBody = mensaje.Html, TextBody = mensaje.Texto };
        foreach (var adjunto in mensaje.Adjuntos)
        {
            cuerpo.Attachments.Add(adjunto.NombreDeArchivo, adjunto.Contenido, ContentType.Parse(adjunto.TipoDeContenido));
        }

        var correo = new MimeMessage { Subject = mensaje.Asunto, Body = cuerpo.ToMessageBody() };
        correo.From.Add(remitente);
        correo.To.Add(MailboxAddress.Parse(mensaje.Destinatario));
        return correo;
    }

    private Uri Servidor()
    {
        var cadena = configuracion.GetConnectionString(CadenaDeConexion);
        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión '{CadenaDeConexion}' del servidor de correo: la da el AppHost.");
        }

        var partes = new DbConnectionStringBuilder { ConnectionString = cadena };
        if (!partes.TryGetValue("Endpoint", out var endpoint)
            || !Uri.TryCreate(endpoint?.ToString(), UriKind.Absolute, out var servidor)
            || servidor.Scheme is not ("smtp" or "smtps"))
        {
            throw new InvalidOperationException(
                $"La cadena de conexión '{CadenaDeConexion}' no tiene la forma Endpoint=smtp://host:puerto.");
        }

        return servidor;
    }

    private MailboxAddress Remitente() =>
        MailboxAddress.TryParse(opciones.Value.Remitente, out var remitente)
            ? remitente
            : throw new InvalidOperationException(
                $"Falta {OpcionesDeNotificaciones.Seccion}:{nameof(OpcionesDeNotificaciones.Remitente)}, o no es una dirección de correo.");
}
