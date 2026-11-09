using System.Collections.Concurrent;
using DraSelina.Consultorio.Notificaciones.Features;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// El <see cref="INotificationChannel"/> de las webs de las pruebas: guarda lo que se le pide enviar, en orden, y
/// lanza mientras <see cref="Caido"/> sea verdadero, como un servidor de correo caído. Uno por web.
/// </summary>
public sealed class CanalDePrueba : INotificationChannel
{
    private readonly ConcurrentQueue<MensajeDeCorreo> _enviados = new();

    /// <summary>Lo enviado, en el orden en que se envió.</summary>
    public IReadOnlyList<MensajeDeCorreo> Enviados => [.. _enviados];

    /// <summary>Mientras sea verdadero, cada envío lanza y no se guarda.</summary>
    public bool Caido { get; set; }

    public Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken cancellationToken)
    {
        if (Caido)
        {
            throw new InvalidOperationException("El canal de prueba está caído.");
        }

        _enviados.Enqueue(mensaje);
        return Task.CompletedTask;
    }
}
