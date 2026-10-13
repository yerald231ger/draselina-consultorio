using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// El servicio en segundo plano que entrega los eventos de los outbox: da una vuelta al arrancar y después una
/// cada <see cref="Intervalo"/>. Nunca tumba la web: una vuelta que falla se registra y se espera la siguiente.
/// </summary>
public sealed class ServicioDeEntregaDeEventos(EntregaDeEventos entrega, ILogger<ServicioDeEntregaDeEventos> registro) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // La web no espera a la primera vuelta para arrancar.
        await Task.Yield();

        using var temporizador = new PeriodicTimer(Intervalo);
        try
        {
            do
            {
                try
                {
                    await entrega.DarUnaVueltaAsync(stoppingToken);
                }
                catch (Exception error) when (error is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    registro.LogError(error, "La vuelta de entrega de eventos falló; se intenta en la siguiente");
                }
            }
            while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // La web se está apagando.
        }
    }
}
