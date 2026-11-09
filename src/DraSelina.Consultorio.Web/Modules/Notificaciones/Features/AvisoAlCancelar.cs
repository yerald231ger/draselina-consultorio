using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Con <see cref="CitaCancelada"/>, le envía al Paciente con correo el aviso de cancelación (CAP-18, FR-23), una sola
/// vez por <see cref="CitaCancelada.EventoId"/> (AD-3), con <see cref="EnvioUnico"/>. Qué correo
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
    EnvioUnico envio,
    IPacientesApi pacientes,
    IAgendaApi agenda,
    IDireccionDelSitio sitio) : IEventHandler<CitaCancelada>
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

        await envio.EnviarAsync(evento.EventoId, evento.CitaId, tipoDeMensaje, ct => ArmarAsync(evento, clase, ct), cancellationToken);
    }

    private async Task<MensajeDeCorreo?> ArmarAsync(CitaCancelada evento, PlantillaDeCancelacion.Clase clase, CancellationToken cancellationToken)
    {
        if (await pacientes.ObtenerContactosAsync(evento.PatientId, cancellationToken) is not { Correo: { } correo } contactos)
        {
            return null;
        }

        var inicio = TimeZoneInfo.ConvertTime(evento.Inicio, ZonaDelConsultorio.Zona);
        return PlantillaDeCancelacion.Armar(new PlantillaDeCancelacion.Datos(
            Destinatario: correo,
            Nombre: contactos.Nombre,
            Fecha: DateOnly.FromDateTime(inicio.DateTime),
            Hora: TimeOnly.FromDateTime(inicio.DateTime),
            TipoDeVisita: await agenda.ObtenerTipoDeVisitaAsync(evento.CitaId, cancellationToken),
            UrlDeReservar: sitio.Reservar(),
            Clase: clase));
    }
}
