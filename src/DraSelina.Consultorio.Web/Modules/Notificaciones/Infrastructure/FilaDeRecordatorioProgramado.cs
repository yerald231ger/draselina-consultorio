namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>
/// Una fila de <c>notificaciones.recordatorio_programado</c>: el Recordatorio pendiente de una Cita (AD-20, T-25). La
/// escribe <see cref="Recordatorios"/>. El Paciente y el inicio vienen del evento, para enviar sin preguntar a Agenda
/// (Gerardo, 2026-11-08, entrada 4.2). Sin llaves foráneas (AD-2).
/// </summary>
public sealed class FilaDeRecordatorioProgramado
{
    public const string Tabla = "recordatorio_programado";

    /// <summary>El índice de <c>programado_para</c>: la vuelta busca los que ya tocan.</summary>
    public const string IndiceDeProgramadoPara = "ix_recordatorio_programado_programado_para";

    /// <summary>El id de <c>agenda.cita</c>: una fila por Cita.</summary>
    public Guid CitaId { get; set; }

    /// <summary>El id de <c>pacientes.paciente</c>.</summary>
    public Guid PacienteId { get; set; }

    /// <summary>El inicio de la Cita, en UTC.</summary>
    public DateTimeOffset Inicio { get; set; }

    /// <summary>Cuándo se envía, en UTC.</summary>
    public DateTimeOffset ProgramadoPara { get; set; }
}
