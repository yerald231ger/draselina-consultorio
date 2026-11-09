using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Con un <see cref="CitaCancelada"/> de motivo <see cref="CitaCancelada.MotivoTarde"/>, le envía al Administrador la
/// alerta de cancelación tardía (CAP-19, FR-24) en cuanto se entrega el evento, sin esperar a un proceso periódico. El
/// motivo lo decidió Agenda: aquí no se recalcula (E-16, T-27); los demás motivos no alertan. Una sola vez por
/// <see cref="CitaCancelada.EventoId"/> (<see cref="TipoDeMensaje.AlertaTardia"/>), con el patrón de
/// <see cref="ConfirmacionAlAgendar"/>. Sale aunque el Paciente no tenga correo: va al Administrador. No registra nada
/// (AD-23).
/// </summary>
public sealed class AlertaDeCancelacionTardia(
    ITransaccionDeNotificaciones transaccion,
    IMensajes mensajes,
    IPacientesApi pacientes,
    IConfiguracionDeEnvio configuracion,
    INotificationChannel canal,
    IClock reloj) : IEventHandler<CitaCancelada>
{
    public async Task HandleAsync(CitaCancelada evento, CancellationToken cancellationToken)
    {
        if (evento.Motivo != CitaCancelada.MotivoTarde)
        {
            return;
        }

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        if (await mensajes.ExisteAsync(evento.EventoId, TipoDeMensaje.AlertaTardia, cancellationToken))
        {
            return;
        }

        // Toda Cita tiene su Paciente: si Pacientes no lo conoce, es un error de programación.
        var contactos = await pacientes.ObtenerContactosAsync(evento.PatientId, cancellationToken)
                        ?? throw new InvalidOperationException("La Cita cancelada apunta a un Paciente que Pacientes no conoce.");

        var inicio = TimeZoneInfo.ConvertTime(evento.Inicio, ZonaDelConsultorio.Zona);
        var mensaje = PlantillaDeAlertaTardia.Armar(new PlantillaDeAlertaTardia.Datos(
            Destinatario: await configuracion.CorreoDelAdministradorAsync(cancellationToken),
            Paciente: contactos.Nombre,
            Fecha: DateOnly.FromDateTime(inicio.DateTime),
            Hora: TimeOnly.FromDateTime(inicio.DateTime)));

        await mensajes.RegistrarAsync(
            new MensajeRegistrado(
                Guid.CreateVersion7(),
                evento.EventoId,
                evento.CitaId,
                TipoDeMensaje.AlertaTardia,
                CanalDeMensaje.Correo,
                EstadoDeMensaje.Enviado,
                reloj.UtcNow),
            cancellationToken);

        await canal.EnviarAsync(mensaje, cancellationToken);

        // El correo ya salió: lo que falta es anotarlo, aunque se cancele la entrega (como en ConfirmacionAlAgendar).
        await tx.ConfirmarAsync(CancellationToken.None);
    }
}
