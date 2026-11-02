namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// La hora local del consultorio, <c>America/Mexico_City</c> (AD-17). Las fechas y horas que ve el Paciente son
/// locales; los instantes que se guardan, UTC.
/// </summary>
public static class HoraLocal
{
    public const string IdDeZona = "America/Mexico_City";

    /// <summary>La zona del consultorio.</summary>
    public static TimeZoneInfo Zona { get; } = TimeZoneInfo.FindSystemTimeZoneById(IdDeZona);

    /// <summary>El instante UTC de una fecha y hora locales del consultorio.</summary>
    public static DateTimeOffset AUtc(DateOnly fecha, TimeOnly hora)
    {
        var local = fecha.ToDateTime(hora, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zona), TimeSpan.Zero);
    }

    /// <summary>La fecha y hora locales del consultorio de un instante.</summary>
    public static (DateOnly Fecha, TimeOnly Hora) DesdeUtc(DateTimeOffset instante)
    {
        var local = TimeZoneInfo.ConvertTime(instante, Zona);
        return (DateOnly.FromDateTime(local.DateTime), TimeOnly.FromDateTime(local.DateTime));
    }
}
