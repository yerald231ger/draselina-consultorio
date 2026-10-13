namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El puerto por el que el handler de un comando de Agenda publica un evento de integración (AD-3). El evento se
/// guarda en <c>agenda.outbox</c> dentro de la transacción abierta del handler: queda si la transacción se
/// confirma y desaparece si se deshace. El servicio de entrega del host lo entrega después a sus handlers.
/// </summary>
public interface IOutboxDeAgenda
{
    /// <summary>
    /// Guarda el evento para entregarlo después. Lanza si no hay una transacción abierta.
    /// </summary>
    /// <param name="evento">El evento. Sin datos personales (AD-23).</param>
    /// <param name="eventoId">El <c>EventoId</c> del evento: es el id de su fila.</param>
    /// <param name="claveDeOrden">Los eventos con la misma clave se entregan en el orden en que ocurrieron.</param>
    /// <param name="ocurridoEn">Cuándo ocurrió, leído del reloj inyectado.</param>
    Task PublicarAsync<TEvento>(
        TEvento evento,
        Guid eventoId,
        Guid claveDeOrden,
        DateTimeOffset ocurridoEn,
        CancellationToken cancellationToken)
        where TEvento : notnull;
}
