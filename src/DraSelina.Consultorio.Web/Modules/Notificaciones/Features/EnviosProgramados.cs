using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Los envíos programados de Notificaciones (AD-20). Una vuelta envía los Recordatorios que ya tocan (FR-22); el
/// servicio en segundo plano de Notificaciones la repite cada minuto. La Agenda del día (entrada 6) se sumará aquí.
/// <para>
/// Cada Recordatorio va en su propia transacción y su propio ámbito: lo aparta con <c>FOR UPDATE SKIP LOCKED</c>,
/// pide el contacto a Pacientes y, con correo, registra el <c>mensaje</c> <see cref="TipoDeMensaje.Recordatorio"/>
/// <see cref="EstadoDeMensaje.Enviado"/>, borra la fila, envía y confirma. Sin correo (o sin Paciente), borra la fila
/// sin enviar; si la Cita ya empezó, también. Si algo falla (el canal caído), la transacción se deshace y la fila
/// queda para la siguiente vuelta.
/// No pregunta a Agenda si la Cita sigue vigente: los eventos de cada Cita llegan en orden y la cancelación borra la
/// fila. No registra la dirección, el nombre ni el contenido (AD-23).
/// </para>
/// </summary>
public sealed class EnviosProgramados(IServiceScopeFactory ambitos, IClock reloj, ILogger<EnviosProgramados> registro)
{
    /// <summary>
    /// Da una vuelta: intenta cada Recordatorio con hora en o antes de ahora, una vez. Devuelve cuántos intentó. Nunca
    /// lanza por un Recordatorio que falla, ni porque no se pueda leer la tabla: lo registra y sigue (o termina la
    /// vuelta). Solo lanza si se cancela.
    /// </summary>
    public async Task<int> DarUnaVueltaAsync(CancellationToken cancellationToken)
    {
        // Los que fallaron en esta vuelta no se vuelven a tomar hasta la siguiente.
        var fallidos = new List<CitaId>();
        var intentados = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!await EnviarElSiguienteAsync(fallidos, cancellationToken))
                {
                    return intentados;
                }

                intentados++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                registro.LogError(error, "No se pudieron leer los Recordatorios programados; se intenta en la siguiente vuelta");
                return intentados;
            }
        }
    }

    // Aparta un Recordatorio vencido y lo envía, en una transacción. Falso si no queda ninguno. Si el envío falla, lo
    // registra, lo agrega a fallidos y devuelve verdadero; solo lanza si no pudo apartar.
    private async Task<bool> EnviarElSiguienteAsync(List<CitaId> fallidos, CancellationToken cancellationToken)
    {
        await using var ambito = ambitos.CreateAsyncScope();
        var servicios = ambito.ServiceProvider;
        var recordatorios = servicios.GetRequiredService<IRecordatorios>();

        await using var tx = await servicios.GetRequiredService<ITransaccionDeNotificaciones>().AbrirAsync(cancellationToken);
        if (await recordatorios.ApartarElSiguienteVencidoAsync(reloj.UtcNow, fallidos, cancellationToken) is not { } recordatorio)
        {
            return false;
        }

        try
        {
            await EnviarAsync(servicios, recordatorios, recordatorio, cancellationToken);
            await tx.ConfirmarAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            // Al soltar la transacción sin confirmar se deshace: la fila sigue.
            fallidos.Add(recordatorio.CitaId);
            registro.LogWarning(error,
                "El Recordatorio de la Cita {CitaId} no se pudo enviar; queda para la siguiente vuelta", recordatorio.CitaId.Valor);
        }

        return true;
    }

    private async Task EnviarAsync(
        IServiceProvider servicios, IRecordatorios recordatorios, RecordatorioProgramado recordatorio, CancellationToken cancellationToken)
    {
        await recordatorios.BorrarAsync(recordatorio.CitaId, cancellationToken);

        // La Cita ya empezó (la web estuvo caída o el evento llegó tarde): "Mañana tienes cita" ya no aplica.
        if (recordatorio.Inicio <= reloj.UtcNow)
        {
            return;
        }

        var contactos = await servicios.GetRequiredService<IPacientesApi>().ObtenerContactosAsync(recordatorio.PacienteId, cancellationToken);
        if (contactos is not { Correo: { } correo })
        {
            return;
        }

        var agenda = servicios.GetRequiredService<IAgendaApi>();
        var inicio = TimeZoneInfo.ConvertTime(recordatorio.Inicio, ZonaDelConsultorio.Zona);
        var mensaje = PlantillaDeRecordatorio.Armar(new PlantillaDeRecordatorio.Datos(
            Destinatario: correo,
            Nombre: contactos.Nombre,
            Fecha: DateOnly.FromDateTime(inicio.DateTime),
            Hora: TimeOnly.FromDateTime(inicio.DateTime),
            TipoDeVisita: await agenda.ObtenerTipoDeVisitaAsync(recordatorio.CitaId, cancellationToken),
            Consultorio: servicios.GetRequiredService<IContenidoApi>().DatosDelConsultorio,
            EnlaceDeGestion: agenda.ObtenerEnlaceDeGestion(recordatorio.CitaId)));

        await servicios.GetRequiredService<IMensajes>().RegistrarAsync(
            new MensajeRegistrado(
                Guid.CreateVersion7(),
                EventoId: null,
                recordatorio.CitaId,
                TipoDeMensaje.Recordatorio,
                CanalDeMensaje.Correo,
                EstadoDeMensaje.Enviado,
                reloj.UtcNow),
            cancellationToken);

        await servicios.GetRequiredService<INotificationChannel>().EnviarAsync(mensaje, cancellationToken);
    }
}
