namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>
/// Una fila de <c>notificaciones.mensaje</c>: lo enviado, para no enviarlo dos veces (AD-3). Sin dirección de correo
/// ni contenido (AD-23). La escribe <see cref="Mensajes"/>.
/// </summary>
public sealed class FilaDeMensaje
{
    public const string Tabla = "mensaje";

    /// <summary>El índice único de <c>(evento_id, tipo)</c>: un mensaje de cada tipo por evento.</summary>
    public const string IndiceDeEventoYTipo = "ux_mensaje_evento_id_tipo";

    /// <summary>
    /// El índice único parcial de <c>(tipo, fecha)</c> donde <c>fecha</c> no es nula: un envío por fecha (la Agenda del
    /// día).
    /// </summary>
    public const string IndiceDeTipoYFecha = "ux_mensaje_tipo_fecha";

    public Guid Id { get; set; }

    /// <summary>Nulo en los envíos programados.</summary>
    public Guid? EventoId { get; set; }

    /// <summary>El id de <c>agenda.cita</c>, sin llave foránea (AD-2).</summary>
    public Guid? CitaId { get; set; }

    /// <summary>El nombre de <c>TipoDeMensaje</c>.</summary>
    public string Tipo { get; set; } = "";

    public string Canal { get; set; } = "";

    /// <summary>El nombre de <c>EstadoDeMensaje</c>.</summary>
    public string Estado { get; set; } = "";

    public DateTimeOffset RegistradoEn { get; set; }

    /// <summary>La fecha local de un envío programado por fecha (la Agenda del día); nula en los demás.</summary>
    public DateOnly? Fecha { get; set; }
}
