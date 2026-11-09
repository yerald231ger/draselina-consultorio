using System.Text;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Con <see cref="CitaReprogramada"/>, le envía al Paciente con correo el aviso "Cita reprogramada" (CAP-18, FR-23), una
/// sola vez por <see cref="CitaReprogramada.EventoId"/> (AD-3), con <see cref="EnvioUnico"/>:
/// <list type="number">
/// <item>Si ya hay un <c>mensaje</c> de <see cref="TipoDeMensaje.Reprogramada"/> con ese evento, no hace nada.</item>
/// <item>Sin correo (o sin Paciente), no envía ni registra nada.</item>
/// <item>Con correo, registra <see cref="EstadoDeMensaje.Enviado"/>, envía y confirma, todo en la transacción de
/// Notificaciones.</item>
/// <item>Si el canal lanza, la transacción se deshace y la excepción sale: el outbox reintenta.</item>
/// </list>
/// La fecha y la hora salen de <see cref="CitaReprogramada.InicioNuevo"/>. Es un handler aparte del que mueve el
/// Recordatorio (<see cref="RecordatorioAlReprogramar"/>). No registra nada (AD-23).
/// </summary>
public sealed class AvisoAlReprogramar(
    EnvioUnico envio,
    IPacientesApi pacientes,
    IAgendaApi agenda,
    IContenidoApi contenido) : IEventHandler<CitaReprogramada>
{
    public Task HandleAsync(CitaReprogramada evento, CancellationToken cancellationToken) =>
        envio.EnviarAsync(evento.EventoId, evento.CitaId, TipoDeMensaje.Reprogramada, ct => ArmarAsync(evento, ct), cancellationToken);

    private async Task<MensajeDeCorreo?> ArmarAsync(CitaReprogramada evento, CancellationToken cancellationToken)
    {
        if (await pacientes.ObtenerContactosAsync(evento.PatientId, cancellationToken) is not { Correo: { } correo } contactos)
        {
            return null;
        }

        var inicio = TimeZoneInfo.ConvertTime(evento.InicioNuevo, ZonaDelConsultorio.Zona);
        var archivo = await agenda.ObtenerArchivoDeCalendarioAsync(evento.CitaId, cancellationToken);
        return PlantillaDeReprogramacion.Armar(new PlantillaDeReprogramacion.Datos(
            Destinatario: correo,
            Nombre: contactos.Nombre,
            Fecha: DateOnly.FromDateTime(inicio.DateTime),
            Hora: TimeOnly.FromDateTime(inicio.DateTime),
            TipoDeVisita: await agenda.ObtenerTipoDeVisitaAsync(evento.CitaId, cancellationToken),
            Consultorio: contenido.DatosDelConsultorio,
            EnlaceDeGestion: agenda.ObtenerEnlaceDeGestion(evento.CitaId),
            Calendario: archivo is null
                ? null
                : new AdjuntoDeCorreo(
                    ArchivoDeCalendario.NombreDeArchivo,
                    ArchivoDeCalendario.TipoDeContenido,
                    Encoding.UTF8.GetBytes(archivo.Contenido))));
    }
}
