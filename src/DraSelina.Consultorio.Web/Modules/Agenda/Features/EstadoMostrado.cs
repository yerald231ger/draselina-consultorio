using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Cómo se ve el estado de una Cita en la administración: la Agenda (<see cref="DiasDeLaAgendaHandler"/>), el Paciente
/// reconocido de "Agendar cita" (<see cref="PacienteReconocidoHandler"/>), el Detalle de Cita
/// (<see cref="DetalleDeCitaHandler"/>) y el historial del Paciente (<see cref="HistorialDelPacienteHandler"/>) lo
/// muestran igual, con su origen. Atendida no se guarda: es
/// una Cita Agendada cuyo inicio ya llegó (RN-19, RN-31).
/// </summary>
internal static class EstadoMostrado
{
    // RN-31: "ya pasó" se mide desde el inicio; en el inicio mismo ya cuenta como Atendida, el mismo corte de
    // Cita.Cancelar.
    public static EstadoEnLaAgenda Estado(EstadoCita estado, DateTimeOffset inicio, DateTimeOffset ahora) => estado switch
    {
        EstadoCita.Agendada when inicio <= ahora => EstadoEnLaAgenda.Atendida,
        EstadoCita.Agendada => EstadoEnLaAgenda.Agendada,
        EstadoCita.NoSePresento => EstadoEnLaAgenda.NoSePresento,
        EstadoCita.Cancelada => EstadoEnLaAgenda.Cancelada,
        _ => throw new InvalidOperationException($"Estado de Cita desconocido: {estado}."),
    };

    public static CancelacionEnLaAgenda? Cancelacion(MotivoDeCancelacion? motivo) => motivo switch
    {
        null => null,
        MotivoDeCancelacion.Paciente => CancelacionEnLaAgenda.PorElPaciente,
        MotivoDeCancelacion.Tarde => CancelacionEnLaAgenda.Tardia,
        MotivoDeCancelacion.Consultorio => CancelacionEnLaAgenda.PorElConsultorio,
        _ => throw new InvalidOperationException($"Motivo de cancelación desconocido: {motivo}."),
    };

    public static OrigenEnLaAgenda Origen(OrigenDeCita origen) => origen switch
    {
        OrigenDeCita.EnLinea => OrigenEnLaAgenda.EnLinea,
        OrigenDeCita.Consultorio => OrigenEnLaAgenda.DelConsultorio,
        _ => throw new InvalidOperationException($"Origen de Cita desconocido: {origen}."),
    };
}
