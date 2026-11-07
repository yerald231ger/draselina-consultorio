using System.Text.Json.Serialization;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Evento de integración (AD-3): se canceló una Cita. Lo publica Agenda por su outbox, en la misma transacción que el
/// cambio (T-30), con el <see cref="CitaId"/> como clave de orden; lo consumen Notificaciones (épica 4) y la operación
/// del Consultorio (épica 5). El motivo ya viene decidido por la Cita: quien lo consume no lo recalcula (E-16). Sin
/// nombre, teléfono, correo, motivo de consulta ni enlace de gestión (AD-23).
/// </summary>
/// <param name="EventoId">Id del evento; también el de su fila en el outbox.</param>
/// <param name="OcurridoEn">Cuándo se canceló, del reloj inyectado.</param>
/// <param name="CitaId">La Cita cancelada.</param>
/// <param name="PatientId">El Paciente de la Cita.</param>
/// <param name="Actor">Quién la canceló (AD-22). Se serializa por su nombre (<c>"Paciente"</c>), como lo guarda la
/// bitácora.</param>
/// <param name="Inicio">El inicio que tenía la Cita, en UTC (AD-17).</param>
/// <param name="Motivo">Por qué: <see cref="MotivoPaciente"/>, <see cref="MotivoTarde"/> o
/// <see cref="MotivoConsultorio"/>.</param>
public sealed record CitaCancelada(
    Guid EventoId,
    DateTimeOffset OcurridoEn,
    CitaId CitaId,
    PatientId PatientId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Actor>))] Actor Actor,
    DateTimeOffset Inicio,
    string Motivo)
{
    /// <summary>La canceló el Paciente a tiempo. El mismo texto que guarda <c>agenda.cita.motivo_de_cancelacion</c>.</summary>
    public const string MotivoPaciente = "paciente";

    /// <summary>
    /// La canceló el Paciente dentro del umbral de cancelación tardía (RN-16). El mismo texto que guarda
    /// <c>agenda.cita.motivo_de_cancelacion</c>.
    /// </summary>
    public const string MotivoTarde = "tarde";

    /// <summary>La canceló el Consultorio. El mismo texto que guarda <c>agenda.cita.motivo_de_cancelacion</c>.</summary>
    public const string MotivoConsultorio = "consultorio";
}
