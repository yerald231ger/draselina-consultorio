using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// El puerto de <c>notificaciones.recordatorio_programado</c>: el Recordatorio pendiente de cada Cita (AD-20, T-25),
/// y la hora del Recordatorio de <c>notificaciones.configuracion_de_envio</c>. Escribe dentro de la transacción abierta
/// del llamador.
/// </summary>
public interface IRecordatorios
{
    /// <summary>La hora local del Recordatorio (<c>hora_recordatorio</c>).</summary>
    Task<TimeOnly> HoraDelRecordatorioAsync(CancellationToken cancellationToken);

    /// <summary>Guarda el Recordatorio de su Cita. Si la Cita ya tiene uno, no hace nada.</summary>
    Task ProgramarAsync(RecordatorioProgramado recordatorio, CancellationToken cancellationToken);

    /// <summary>Deja el Recordatorio de su Cita con estos datos: lo crea o lo reemplaza.</summary>
    Task MoverAsync(RecordatorioProgramado recordatorio, CancellationToken cancellationToken);

    /// <summary>Borra el Recordatorio de la Cita, si tiene.</summary>
    Task BorrarAsync(CitaId citaId, CancellationToken cancellationToken);

    /// <summary>
    /// El Recordatorio más antiguo con <see cref="RecordatorioProgramado.ProgramadoPara"/> en o antes de
    /// <paramref name="ahora"/>, sin los de <paramref name="excepto"/>, apartado con <c>FOR UPDATE SKIP LOCKED</c> en
    /// la transacción abierta: otra vuelta no lo toma hasta que esta termine. Nulo si no queda ninguno.
    /// </summary>
    Task<RecordatorioProgramado?> ApartarElSiguienteVencidoAsync(
        DateTimeOffset ahora, IReadOnlyCollection<CitaId> excepto, CancellationToken cancellationToken);
}

/// <summary>Una fila de <c>notificaciones.recordatorio_programado</c>.</summary>
/// <param name="CitaId">La Cita; una fila por Cita.</param>
/// <param name="PacienteId">El Paciente de la Cita, del evento.</param>
/// <param name="Inicio">El inicio de la Cita, en UTC, del evento.</param>
/// <param name="ProgramadoPara">Cuándo se envía, en UTC.</param>
public sealed record RecordatorioProgramado(CitaId CitaId, PatientId PacienteId, DateTimeOffset Inicio, DateTimeOffset ProgramadoPara);

/// <summary>
/// Cuándo sale el Recordatorio (FR-22): el día local anterior al de la Cita, a la hora del Recordatorio, en
/// <see cref="ZonaDelConsultorio"/>. Una Cita del jueves a las 4:00 pm, con la hora a las 9:00, da el miércoles a las
/// 9:00 am local.
/// </summary>
public static class HoraDelRecordatorio
{
    /// <summary>El instante, en UTC, del Recordatorio de una Cita que empieza en <paramref name="inicio"/>.</summary>
    public static DateTimeOffset ProgramadoPara(DateTimeOffset inicio, TimeOnly hora)
    {
        var local = TimeZoneInfo.ConvertTime(inicio, ZonaDelConsultorio.Zona);
        var dia = DateOnly.FromDateTime(local.DateTime).AddDays(-1);
        var enLaZona = dia.ToDateTime(hora, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(enLaZona, ZonaDelConsultorio.Zona), TimeSpan.Zero);
    }
}
