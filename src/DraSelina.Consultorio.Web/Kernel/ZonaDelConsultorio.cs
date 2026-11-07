namespace DraSelina.Consultorio.Kernel;

/// <summary>
/// La zona horaria del consultorio, <c>America/Mexico_City</c> (AD-17), en un solo lugar: la leen el dominio de
/// Agenda (<c>HoraLocal</c>) y la interfaz, que no usa ese dominio.
/// </summary>
public static class ZonaDelConsultorio
{
    /// <summary>El id IANA de la zona.</summary>
    public const string Id = "America/Mexico_City";

    /// <summary>La zona, resuelta una vez.</summary>
    public static TimeZoneInfo Zona { get; } = TimeZoneInfo.FindSystemTimeZoneById(Id);
}
