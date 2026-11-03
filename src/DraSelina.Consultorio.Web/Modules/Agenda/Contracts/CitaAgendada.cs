using System.Text.Json.Serialization;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Evento de integración (AD-3): se agendó una Cita. Lo publica Agenda por su outbox, en la misma transacción que la
/// Cita (T-30), con el <see cref="CitaId"/> como clave de orden; lo consume Notificaciones. Sin nombre, teléfono,
/// correo, motivo de consulta ni enlace de gestión (AD-23): quien necesite el contacto lo pide a Pacientes por
/// <see cref="PatientId"/>.
/// </summary>
/// <param name="EventoId">Id del evento; también el de su fila en el outbox.</param>
/// <param name="OcurridoEn">Cuándo se agendó, del reloj inyectado.</param>
/// <param name="CitaId">La Cita agendada.</param>
/// <param name="PatientId">El Paciente de la Cita.</param>
/// <param name="Actor">Quién la agendó (AD-22). Se serializa por su nombre (<c>"Paciente"</c>), como lo guarda la
/// bitácora, para que el contrato no dependa de la numeración de <see cref="Kernel.Actor"/>.</param>
/// <param name="Inicio">Inicio de la Cita, en UTC (AD-17).</param>
/// <param name="Fin">Fin de la Cita, en UTC (AD-17).</param>
/// <param name="Origen">Quién creó la Cita: <see cref="OrigenEnLinea"/> o <see cref="OrigenConsultorio"/>.</param>
public sealed record CitaAgendada(
    Guid EventoId,
    DateTimeOffset OcurridoEn,
    CitaId CitaId,
    PatientId PatientId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Actor>))] Actor Actor,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string Origen)
{
    /// <summary>La creó el Paciente desde el sitio. El mismo texto que guarda <c>agenda.cita.origen</c>.</summary>
    public const string OrigenEnLinea = "en_linea";

    /// <summary>La registró el Administrador a mano. El mismo texto que guarda <c>agenda.cita.origen</c>.</summary>
    public const string OrigenConsultorio = "consultorio";
}
