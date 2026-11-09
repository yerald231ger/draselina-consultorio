namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Las direcciones del sitio a las que llevan los botones de los correos. Las da la configuración
/// (<c>Notificaciones:UrlDelSitio</c>); en local, el AppHost pasa la de la web.
/// </summary>
public interface IDireccionDelSitio
{
    /// <summary>
    /// La dirección absoluta de <c>/reservar</c>. Lanza si falta la configuración: el handler no registra nada y el
    /// outbox reintenta.
    /// </summary>
    string Reservar();
}
