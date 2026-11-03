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

    /// <summary>
    /// La semana de calendario local de <paramref name="fecha"/>, de lunes a domingo (RN-18), como instantes UTC
    /// <c>[desde, hasta)</c>: de las 00:00 locales del lunes a las 00:00 locales del lunes siguiente.
    /// </summary>
    public static (DateTimeOffset Desde, DateTimeOffset Hasta) SemanaDe(DateOnly fecha)
    {
        // DayOfWeek empieza en domingo (0); el lunes queda 0 días atrás y el domingo 6.
        var lunes = fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7));
        return (AUtc(lunes, TimeOnly.MinValue), AUtc(lunes.AddDays(7), TimeOnly.MinValue));
    }
}
