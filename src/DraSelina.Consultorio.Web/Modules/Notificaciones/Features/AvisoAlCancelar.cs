using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Con <see cref="CitaCancelada"/>, le envía al Paciente con correo el aviso de cancelación (CAP-18, FR-23), una sola
/// vez por <see cref="CitaCancelada.EventoId"/> (AD-3), con el patrón de <see cref="ConfirmacionAlAgendar"/>. Qué correo
/// sale lo dice el <see cref="CitaCancelada.Motivo"/> que decidió Agenda; aquí no se recalcula (E-16):
/// <list type="bullet">
/// <item><see cref="CitaCancelada.MotivoPaciente"/>: "Tu cita está cancelada", <see cref="TipoDeMensaje.Cancelada"/>.</item>
/// <item><see cref="CitaCancelada.MotivoTarde"/>: el mismo correo y el mismo tipo, con la frase de cancelación
/// tardía.</item>
/// <item><see cref="CitaCancelada.MotivoConsultorio"/>: la disculpa del Consultorio,
/// <see cref="TipoDeMensaje.CanceladaPorConsultorio"/>.</item>
/// </list>
/// Un motivo que no es ninguno de los tres lanza, sin datos del Paciente: es un evento que este handler no sabe leer.
/// Es un handler aparte del que borra el Recordatorio (<see cref="RecordatorioAlCancelar"/>). No registra nada (AD-23).
/// </summary>
public sealed class AvisoAlCancelar(
    ITransaccionDeNotificaciones transaccion,
    IMensajes mensajes,
    IPacientesApi pacientes,
    IAgendaApi agenda,
    IDireccionDelSitio sitio,
    INotificationChannel canal,
    IClock reloj) : IEventHandler<CitaCancelada>
{
    public async Task HandleAsync(CitaCancelada evento, CancellationToken cancellationToken)
    {
        var (clase, tipoDeMensaje) = evento.Motivo switch
        {
            CitaCancelada.MotivoPaciente => (PlantillaDeCancelacion.Clase.PorElPaciente, TipoDeMensaje.Cancelada),
            CitaCancelada.MotivoTarde => (PlantillaDeCancelacion.Clase.Tardia, TipoDeMensaje.Cancelada),
            CitaCancelada.MotivoConsultorio => (PlantillaDeCancelacion.Clase.PorElConsultorio, TipoDeMensaje.CanceladaPorConsultorio),
            _ => throw new InvalidOperationException($"{nameof(CitaCancelada)} con un motivo que Notificaciones no conoce."),
        };

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        if (await mensajes.ExisteAsync(evento.EventoId, tipoDeMensaje, cancellationToken))
        {
            return;
        }

        if (await pacientes.ObtenerContactosAsync(evento.PatientId, cancellationToken) is not { Correo: { } correo } contactos)
        {
            return;
        }

        var inicio = TimeZoneInfo.ConvertTime(evento.Inicio, ZonaDelConsultorio.Zona);
        var mensaje = PlantillaDeCancelacion.Armar(new PlantillaDeCancelacion.Datos(
            Destinatario: correo,
            Nombre: contactos.Nombre,
            Fecha: DateOnly.FromDateTime(inicio.DateTime),
            Hora: TimeOnly.FromDateTime(inicio.DateTime),
            TipoDeVisita: await agenda.ObtenerTipoDeVisitaAsync(evento.CitaId, cancellationToken),
            UrlDeReservar: sitio.Reservar(),
            Clase: clase));

        await mensajes.RegistrarAsync(
            new MensajeRegistrado(
                Guid.CreateVersion7(),
                evento.EventoId,
                evento.CitaId,
                tipoDeMensaje,
                CanalDeMensaje.Correo,
                EstadoDeMensaje.Enviado,
                reloj.UtcNow),
            cancellationToken);

        await canal.EnviarAsync(mensaje, cancellationToken);

        // El correo ya salió: lo que falta es anotarlo, aunque se cancele la entrega (como en ConfirmacionAlAgendar).
        await tx.ConfirmarAsync(CancellationToken.None);
    }
}
