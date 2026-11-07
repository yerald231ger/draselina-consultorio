using System.Text.Json.Serialization;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Evento de integración (AD-3): se reprogramó una Cita; es la misma Cita en otro horario (RN-15). Lo publica Agenda por
/// su outbox, en la misma transacción que el cambio (T-30), con el <see cref="CitaId"/> como clave de orden; lo consume
/// Notificaciones para avisar y mover el recordatorio (T-25). Sin nombre, teléfono, correo, motivo de consulta ni
/// enlace de gestión (AD-23).
/// </summary>
/// <param name="EventoId">Id del evento; también el de su fila en el outbox.</param>
/// <param name="OcurridoEn">Cuándo se reprogramó, del reloj inyectado.</param>
/// <param name="CitaId">La Cita reprogramada.</param>
/// <param name="PatientId">El Paciente de la Cita.</param>
/// <param name="Actor">Quién la reprogramó (AD-22). Se serializa por su nombre (<c>"Paciente"</c>), como lo guarda la
/// bitácora.</param>
/// <param name="InicioAnterior">El inicio que tenía, en UTC (AD-17).</param>
/// <param name="InicioNuevo">El inicio nuevo, en UTC (AD-17).</param>
/// <param name="FinNuevo">El fin nuevo, en UTC (AD-17).</param>
public sealed record CitaReprogramada(
    Guid EventoId,
    DateTimeOffset OcurridoEn,
    CitaId CitaId,
    PatientId PatientId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Actor>))] Actor Actor,
    DateTimeOffset InicioAnterior,
    DateTimeOffset InicioNuevo,
    DateTimeOffset FinNuevo);
