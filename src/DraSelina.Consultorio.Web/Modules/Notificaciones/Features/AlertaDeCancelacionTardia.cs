using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Con un <see cref="CitaCancelada"/> de motivo <see cref="CitaCancelada.MotivoTarde"/>, le envía al Administrador la
/// alerta de cancelación tardía (CAP-19, FR-24) en cuanto se entrega el evento, sin esperar a un proceso periódico. El
/// motivo lo decidió Agenda: aquí no se recalcula (E-16, T-27); los demás motivos no alertan. Una sola vez por
/// <see cref="CitaCancelada.EventoId"/> (<see cref="TipoDeMensaje.AlertaTardia"/>), con
/// <see cref="EnvioUnico"/>. Sale aunque el Paciente no tenga correo: va al Administrador. No registra nada
/// (AD-23).
/// </summary>
public sealed class AlertaDeCancelacionTardia(
    EnvioUnico envio,
    IPacientesApi pacientes,
    IConfiguracionDeEnvio configuracion,
    IDireccionDelSitio sitio) : IEventHandler<CitaCancelada>
{
    public Task HandleAsync(CitaCancelada evento, CancellationToken cancellationToken) =>
        evento.Motivo != CitaCancelada.MotivoTarde
            ? Task.CompletedTask
            : envio.EnviarAsync(evento.EventoId, evento.CitaId, TipoDeMensaje.AlertaTardia, ct => ArmarAsync(evento, ct), cancellationToken);

    private async Task<MensajeDeCorreo?> ArmarAsync(CitaCancelada evento, CancellationToken cancellationToken)
    {
        // Toda Cita tiene su Paciente: si Pacientes no lo conoce, es un error de programación.
        var contactos = await pacientes.ObtenerContactosAsync(evento.PatientId, cancellationToken)
                        ?? throw new InvalidOperationException("La Cita cancelada apunta a un Paciente que Pacientes no conoce.");

        var inicio = TimeZoneInfo.ConvertTime(evento.Inicio, ZonaDelConsultorio.Zona);
        var fecha = DateOnly.FromDateTime(inicio.DateTime);
        return PlantillaDeAlertaTardia.Armar(new PlantillaDeAlertaTardia.Datos(
            Destinatario: await configuracion.CorreoDelAdministradorAsync(cancellationToken),
            Paciente: contactos.Nombre,
            Fecha: fecha,
            Hora: TimeOnly.FromDateTime(inicio.DateTime),
            UrlDeLaAgenda: sitio.Agenda(fecha)));
    }
}
