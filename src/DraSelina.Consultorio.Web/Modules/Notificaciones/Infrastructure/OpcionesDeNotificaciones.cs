namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>La sección <c>Notificaciones</c> de la configuración.</summary>
public sealed class OpcionesDeNotificaciones
{
    public const string Seccion = "Notificaciones";

    /// <summary>
    /// La dirección de la que salen los correos. En desarrollo la da el AppHost: <c>citas@consultorio.local</c>
    /// <c>[ASSUMPTION]</c>.
    /// </summary>
    public string? Remitente { get; set; }

    /// <summary>
    /// La dirección absoluta del sitio, a la que llevan los botones de los correos que no usan el Enlace de gestión
    /// ("Agendar otra cita", "Elegir otro horario" a <c>/reservar</c>). En local la da el AppHost: la de la web.
    /// </summary>
    public string? UrlDelSitio { get; set; }
}
