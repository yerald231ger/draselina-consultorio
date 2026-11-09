using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Los envíos programados de Notificaciones (AD-20). Una vuelta envía los Recordatorios que ya tocan (FR-22) y, desde
/// la hora de la Agenda del día, "Tus citas de hoy" a la doctora (FR-25); el servicio en segundo plano de
/// Notificaciones la repite cada minuto. Un paso que falla no impide el otro.
/// <para>
/// Cada Recordatorio va en su propia transacción y su propio ámbito: lo aparta con <c>FOR UPDATE SKIP LOCKED</c>,
/// pide el contacto a Pacientes y, con correo, registra el <c>mensaje</c> <see cref="TipoDeMensaje.Recordatorio"/>
/// <see cref="EstadoDeMensaje.Enviado"/>, borra la fila, envía y confirma. Sin correo (o sin Paciente), borra la fila
/// sin enviar; si la Cita ya empezó, también. Si algo falla (el canal caído), la transacción se deshace y la fila
/// queda para la siguiente vuelta.
/// No pregunta a Agenda si la Cita sigue vigente: los eventos de cada Cita llegan en orden y la cancelación borra la
/// fila. No registra la dirección, el nombre ni el contenido (AD-23).
/// </para>
/// <para>
/// La Agenda del día sale una sola vez por fecha local: con la hora local en o después de <c>hora_agenda_del_dia</c> y
/// sin <c>mensaje</c> <see cref="TipoDeMensaje.AgendaDelDia"/> para hoy, le pide a Agenda la de hoy (Agenda decide si
/// se atiende, T-27). Sin Tramos ni Citas registra <see cref="EstadoDeMensaje.Omitido"/> y no envía nada; si no, pide
/// el nombre de cada Paciente, registra <see cref="EstadoDeMensaje.Enviado"/> con la fecha, envía y confirma, en una
/// transacción. Si el canal falla, se deshace y se intenta en la siguiente vuelta. Solo hoy: los días pasados no se
/// recuperan.
/// </para>
/// </summary>
public sealed class EnviosProgramados(IServiceScopeFactory ambitos, IClock reloj, ILogger<EnviosProgramados> registro)
{
    /// <summary>
    /// Da una vuelta: intenta cada Recordatorio con hora en o antes de ahora, una vez, y después la Agenda del día si ya
    /// toca. Devuelve cuántos Recordatorios intentó. Nunca lanza por un envío que falla, ni porque no se pueda leer la
    /// tabla: lo registra y sigue (o termina ese paso). Solo lanza si se cancela.
    /// </summary>
    public async Task<int> DarUnaVueltaAsync(CancellationToken cancellationToken)
    {
        var intentados = await EnviarLosRecordatoriosAsync(cancellationToken);

        try
        {
            await EnviarLaAgendaDelDiaAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            // Al soltar la transacción sin confirmar se deshace: no queda mensaje y la siguiente vuelta lo intenta.
            registro.LogWarning(error, "La Agenda del día no se pudo enviar; se intenta en la siguiente vuelta");
        }

        return intentados;
    }

    private async Task<int> EnviarLosRecordatoriosAsync(CancellationToken cancellationToken)
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

    // La Agenda del día de hoy, si ya es hora y no salió (ni se omitió) hoy. Lanza si algo falla: lo registra la vuelta.
    private async Task EnviarLaAgendaDelDiaAsync(CancellationToken cancellationToken)
    {
        await using var ambito = ambitos.CreateAsyncScope();
        var servicios = ambito.ServiceProvider;
        var configuracion = servicios.GetRequiredService<IConfiguracionDeEnvio>();

        var ahora = reloj.UtcNow;
        var local = TimeZoneInfo.ConvertTime(ahora, ZonaDelConsultorio.Zona).DateTime;
        var hoy = DateOnly.FromDateTime(local);
        if (TimeOnly.FromDateTime(local) < await configuracion.HoraDeLaAgendaDelDiaAsync(cancellationToken))
        {
            return;
        }

        var mensajes = servicios.GetRequiredService<IMensajes>();
        await using var tx = await servicios.GetRequiredService<ITransaccionDeNotificaciones>().AbrirAsync(cancellationToken);
        if (await mensajes.ExisteDeLaFechaAsync(TipoDeMensaje.AgendaDelDia, hoy, cancellationToken))
        {
            return;
        }

        var agenda = await servicios.GetRequiredService<IAgendaApi>().AgendaDelDiaAsync(hoy, cancellationToken);

        // Ese día no se atiende y no hay Citas: no sale nada, y queda anotado para no volver a preguntar hoy.
        if (!agenda.TieneTramos && agenda.Citas.Count == 0)
        {
            await mensajes.RegistrarAsync(MensajeDeLaFecha(hoy, EstadoDeMensaje.Omitido, ahora), cancellationToken);
            await tx.ConfirmarAsync(CancellationToken.None);
            return;
        }

        var pacientes = servicios.GetRequiredService<IPacientesApi>();
        var citas = new List<PlantillaDeAgendaDelDia.Cita>(agenda.Citas.Count);
        foreach (var cita in agenda.Citas)
        {
            // Toda Cita tiene su Paciente: si Pacientes no lo conoce, es un error de programación.
            var contactos = await pacientes.ObtenerContactosAsync(cita.PatientId, cancellationToken)
                            ?? throw new InvalidOperationException("Una Cita del día apunta a un Paciente que Pacientes no conoce.");
            citas.Add(new PlantillaDeAgendaDelDia.Cita(cita.Hora, contactos.Nombre, cita.MotivoDeConsulta));
        }

        var mensaje = PlantillaDeAgendaDelDia.Armar(new PlantillaDeAgendaDelDia.Datos(
            await configuracion.CorreoDeLaDoctoraAsync(cancellationToken), hoy, citas));

        await mensajes.RegistrarAsync(MensajeDeLaFecha(hoy, EstadoDeMensaje.Enviado, ahora), cancellationToken);
        await servicios.GetRequiredService<INotificationChannel>().EnviarAsync(mensaje, cancellationToken);

        // El correo ya salió: lo que falta es anotarlo, aunque se cancele la vuelta.
        await tx.ConfirmarAsync(CancellationToken.None);
    }

    private static MensajeRegistrado MensajeDeLaFecha(DateOnly fecha, EstadoDeMensaje estado, DateTimeOffset ahora) => new(
        Guid.CreateVersion7(),
        EventoId: null,
        CitaId: null,
        TipoDeMensaje.AgendaDelDia,
        CanalDeMensaje.Correo,
        estado,
        ahora,
        fecha);
}
