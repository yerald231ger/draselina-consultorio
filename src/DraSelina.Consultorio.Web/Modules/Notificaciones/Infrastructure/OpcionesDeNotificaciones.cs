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
}
