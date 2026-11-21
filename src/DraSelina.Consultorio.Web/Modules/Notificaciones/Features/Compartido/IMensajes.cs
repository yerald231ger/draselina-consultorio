using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// El puerto de <c>notificaciones.mensaje</c>: lo enviado, para no enviarlo dos veces (AD-3). Sin dirección ni
/// contenido (AD-23). Guarda dentro de la transacción abierta del llamador.
/// </summary>
public interface IMensajes
{
    /// <summary>Si ya hay un mensaje de ese tipo para ese evento, en cualquier estado.</summary>
    Task<bool> ExisteAsync(Guid eventoId, TipoDeMensaje tipo, CancellationToken cancellationToken);

    /// <summary>
    /// Si el mensaje de ese tipo para esa Cita ya quedó cerrado en o después de <paramref name="desde"/>: salió
    /// (<see cref="EstadoDeMensaje.Enviado"/>) o se omitió sin evento (<see cref="EstadoDeMensaje.Omitido"/> con
    /// <see cref="MensajeRegistrado.EventoId"/> nulo: el de un cambio de la hora del Recordatorio). El Omitido de un
    /// evento no cuenta: lo cubre <see cref="ExisteAsync"/> por evento, y contarlo aquí taparía a un evento posterior
    /// de la misma Cita entregado después.
    /// </summary>
    Task<bool> CerradoDesdeAsync(CitaId citaId, TipoDeMensaje tipo, DateTimeOffset desde, CancellationToken cancellationToken);

    /// <summary>Si ya hay un mensaje de ese tipo para esa fecha local, en cualquier estado. Por el índice único de (tipo, fecha).</summary>
    Task<bool> ExisteDeLaFechaAsync(TipoDeMensaje tipo, DateOnly fecha, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda <paramref name="mensaje"/> en la transacción abierta. Si ya hay uno con su evento y su tipo (o con su tipo
    /// y su fecha), el índice único lo rechaza y lanza: otra entrega del mismo evento (u otra vuelta) llegó antes.
    /// </summary>
    Task RegistrarAsync(MensajeRegistrado mensaje, CancellationToken cancellationToken);
}

/// <summary>Una fila de <c>notificaciones.mensaje</c> (modelo de datos, esquema <c>notificaciones</c>).</summary>
/// <param name="Id">Guid v7.</param>
/// <param name="EventoId">El evento que lo originó; nulo en los envíos programados.</param>
/// <param name="CitaId">La Cita de la que habla; nula en la Agenda del día. Sin llave foránea.</param>
/// <param name="Tipo">Qué mensaje es.</param>
/// <param name="Canal">Por dónde salió: <see cref="CanalDeMensaje.Correo"/>.</param>
/// <param name="Estado">Cómo terminó.</param>
/// <param name="RegistradoEn">Del reloj inyectado.</param>
/// <param name="Fecha">La fecha local de un envío por fecha (la Agenda del día); nula en los demás.</param>
public sealed record MensajeRegistrado(
    Guid Id,
    Guid? EventoId,
    CitaId? CitaId,
    TipoDeMensaje Tipo,
    string Canal,
    EstadoDeMensaje Estado,
    DateTimeOffset RegistradoEn,
    DateOnly? Fecha = null);

/// <summary>Los mensajes de Notificaciones. Se guardan por su nombre, con <c>CHECK</c>.</summary>
public enum TipoDeMensaje
{
    Confirmacion = 1,
    Recordatorio = 2,
    Reprogramada = 3,
    Cancelada = 4,
    CanceladaPorConsultorio = 5,
    AlertaTardia = 6,
    AgendaDelDia = 7,
}

/// <summary>Cómo terminó un mensaje. Se guarda por su nombre, con <c>CHECK</c>.</summary>
public enum EstadoDeMensaje
{
    Enviado = 1,
    Fallido = 2,
    Omitido = 3,
}

/// <summary>Los canales de <c>notificaciones.mensaje.canal</c>.</summary>
public static class CanalDeMensaje
{
    public const string Correo = "correo";
}
