namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El archivo de calendario de una Cita (FR-5): un evento iCalendar (RFC 5545) con su inicio y fin en UTC, el lugar
/// del consultorio y el Enlace de gestión en la descripción. Sin motivo de consulta, nombre, teléfono ni correo
/// (AD-23). Lo arma solo Agenda, por <see cref="IAgendaApi"/>: la confirmación lo descarga y Notificaciones lo adjunta.
/// Lleva el Enlace de gestión: nunca va a registros (AD-23); <see cref="ToString"/> no lo muestra.
/// </summary>
/// <param name="Contenido">El texto iCalendar, con fin de línea CRLF.</param>
public sealed record ArchivoDeCalendario(string Contenido)
{
    /// <summary>El tipo de contenido con que se sirve o se adjunta.</summary>
    public const string TipoDeContenido = "text/calendar; charset=utf-8";

    /// <summary>El nombre con que se descarga o se adjunta.</summary>
    public const string NombreDeArchivo = "cita.ics";

    public override string ToString() => "ArchivoDeCalendario(oculto)";
}
