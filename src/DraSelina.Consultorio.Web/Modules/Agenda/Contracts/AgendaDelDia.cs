using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// La Agenda de una fecha local del consultorio, para "Tus citas de hoy" (CAP-20, FR-25): si se atiende y qué Citas
/// tiene. Lo decide Agenda; Notificaciones no lo recalcula (T-27).
/// </summary>
/// <param name="TieneTramos">Si la fecha tiene Tramos vigentes (INV-V1): de su agenda custom o de la base.</param>
/// <param name="Citas">Las Citas Agendadas de esa fecha, de cualquier origen, en orden de inicio.</param>
public sealed record AgendaDelDia(bool TieneTramos, IReadOnlyList<CitaDelDia> Citas)
{
    // Las Citas llevan el motivo de consulta (AD-23).
    public override string ToString() => $"AgendaDelDia(TieneTramos = {TieneTramos}, Citas = {Citas.Count})";
}

/// <summary>Una Cita Agendada de la Agenda del día.</summary>
/// <param name="Hora">Hora local de inicio.</param>
/// <param name="PatientId">El Paciente: su nombre lo da Pacientes.</param>
/// <param name="MotivoDeConsulta">Nulo si la Cita (del Consultorio) no lo tiene.</param>
public sealed record CitaDelDia(TimeOnly Hora, PatientId PatientId, string? MotivoDeConsulta)
{
    // Nunca se registra el motivo (AD-23).
    public override string ToString() => $"CitaDelDia(Hora = {Hora:HH:mm}, motivo oculto)";
}
