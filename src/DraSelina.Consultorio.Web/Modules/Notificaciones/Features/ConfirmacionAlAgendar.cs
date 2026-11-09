using System.Text;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Con <see cref="CitaAgendada"/>, le envía al Paciente con correo su confirmación (CAP-18, FR-21), una sola vez por
/// <see cref="CitaAgendada.EventoId"/> (AD-3). Igual para las Citas en línea que para las del Consultorio (FR-21,
/// <c>[ASSUMPTION]</c> del PRD). Sale después de la reserva, por el outbox: nunca la retrasa (FR-26, AD-20).
/// <list type="number">
/// <item>Si ya hay un <c>mensaje</c> de <see cref="TipoDeMensaje.Confirmacion"/> con ese evento, no hace nada.</item>
/// <item>Sin correo (o sin Paciente), no envía ni registra nada <c>[ASSUMPTION]</c>.</item>
/// <item>Con correo, registra <see cref="EstadoDeMensaje.Enviado"/>, envía y confirma, todo en la transacción de
/// Notificaciones. El registro va antes del envío: si otra entrega del mismo evento lo registró antes, el índice
/// único lo rechaza y este no envía.</item>
/// <item>Si el canal lanza, la transacción se deshace y la excepción sale: el outbox reintenta.</item>
/// </list>
/// No registra nada: ni la dirección, ni el nombre, ni el contenido van a registros o trazas (AD-23).
/// </summary>
public sealed class ConfirmacionAlAgendar(
    ITransaccionDeNotificaciones transaccion,
    IMensajes mensajes,
    IPacientesApi pacientes,
    IAgendaApi agenda,
    IContenidoApi contenido,
    INotificationChannel canal,
    IClock reloj) : IEventHandler<CitaAgendada>
{
    public async Task HandleAsync(CitaAgendada evento, CancellationToken cancellationToken)
    {
        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        if (await mensajes.ExisteAsync(evento.EventoId, TipoDeMensaje.Confirmacion, cancellationToken))
        {
            return;
        }

        if (await pacientes.ObtenerContactosAsync(evento.PatientId, cancellationToken) is not { Correo: { } correo } contactos)
        {
            return;
        }

        var inicio = TimeZoneInfo.ConvertTime(evento.Inicio, ZonaDelConsultorio.Zona);
        var archivo = await agenda.ObtenerArchivoDeCalendarioAsync(evento.CitaId, cancellationToken);
        var mensaje = PlantillaDeConfirmacion.Armar(new PlantillaDeConfirmacion.Datos(
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

        await mensajes.RegistrarAsync(
            new MensajeRegistrado(
                Guid.CreateVersion7(),
                evento.EventoId,
                evento.CitaId,
                TipoDeMensaje.Confirmacion,
                CanalDeMensaje.Correo,
                EstadoDeMensaje.Enviado,
                reloj.UtcNow),
            cancellationToken);

        await canal.EnviarAsync(mensaje, cancellationToken);

        // El correo ya salió: lo que falta es anotarlo, aunque se cancele la entrega. Si se cancelara aquí, el
        // siguiente intento lo enviaría otra vez.
        await tx.ConfirmarAsync(CancellationToken.None);
    }
}
