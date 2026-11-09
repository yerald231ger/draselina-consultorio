using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>
/// El servicio en segundo plano de los envíos programados de Notificaciones (AD-20): da una vuelta de
/// <see cref="EnviosProgramados"/> al arrancar y después una cada <see cref="Intervalo"/>. Nunca tumba la web: una
/// vuelta que falla se registra y se espera la siguiente.
/// </summary>
public sealed class ServicioDeEnviosProgramados(EnviosProgramados envios, ILogger<ServicioDeEnviosProgramados> registro) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

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
                    await envios.DarUnaVueltaAsync(stoppingToken);
                }
                catch (Exception error) when (error is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    registro.LogError(error, "La vuelta de envíos programados falló; se intenta en la siguiente");
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
