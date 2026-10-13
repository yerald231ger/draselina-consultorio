namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// Una fila de <c>agenda.outbox</c>: un evento de integración que Agenda publicó (AD-3). Es de Agenda: cada módulo
/// que publica declara la suya. La escribe <see cref="OutboxDeAgenda"/> y después solo la toca el servicio de
/// entrega del host.
/// </summary>
public sealed class FilaDeOutbox
{
    public const string Tabla = "outbox";

    /// <summary>El estado con el que nace una fila. Los otros dos, <c>Entregado</c> y <c>Fallido</c>, los pone la entrega.</summary>
    public const string Pendiente = "Pendiente";

    /// <summary>El <c>EventoId</c> del evento.</summary>
    public Guid Id { get; set; }

    /// <summary>El nombre del tipo del evento.</summary>
    public string Tipo { get; set; } = "";

    /// <summary>El evento serializado. Sin datos personales (AD-23).</summary>
    public string Contenido { get; set; } = "";

    public DateTimeOffset OcurridoEn { get; set; }

    public Guid ClaveDeOrden { get; set; }

    public string Estado { get; set; } = Pendiente;

    /// <summary>Entregas fallidas hasta ahora.</summary>
    public int Intentos { get; set; }

    /// <summary>Desde cuándo puede intentarse. Al guardar el evento es <see cref="OcurridoEn"/>.</summary>
    public DateTimeOffset ProximoIntentoEn { get; set; }

    public DateTimeOffset? EntregadoEn { get; set; }
}
