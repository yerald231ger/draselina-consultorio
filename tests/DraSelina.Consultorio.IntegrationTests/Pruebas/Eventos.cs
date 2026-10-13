using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>El evento que publica el comando de prueba <see cref="Anotar"/>, junto con su anotación.</summary>
public sealed record AnotacionHecha(Guid EventoId, DateTimeOffset OcurridoEn, Guid AnotacionId);

/// <summary>
/// Evento de prueba cuyo handler obedece lo que el evento trae: falla sus primeras
/// <paramref name="FallasAntesDeEntregar"/> entregas y tarda <paramref name="DemoraMs"/> en cada una.
/// </summary>
public sealed record EventoDePrueba(Guid EventoId, int FallasAntesDeEntregar = 0, int DemoraMs = 0)
{
    /// <summary>Un handler que nunca lo acepta.</summary>
    public const int FallaSiempre = int.MaxValue;
}

/// <summary>Evento de prueba con dos handlers registrados.</summary>
public sealed record EventoConDosHandlers(Guid EventoId);

/// <summary>Evento de prueba que nadie escucha.</summary>
public sealed record EventoQueNadieEscucha(Guid EventoId);

/// <summary>
/// El rastro que deja un handler de evento de prueba cada vez que recibe un evento, también cuando falla. Vive en
/// la base para que una prueba afirme sobre lo que pasó aunque el handler haya corrido en otra web.
/// </summary>
public sealed class EntregaRecibida
{
    /// <summary>Consecutivo de la base: el orden en que llegaron.</summary>
    public long Id { get; set; }

    public Guid EventoId { get; set; }

    public string Handler { get; set; } = "";

    /// <summary>La hora del reloj inyectado al recibirlo.</summary>
    public DateTimeOffset RecibidoEn { get; set; }

    /// <summary>Si el handler lanzó después de dejar este rastro.</summary>
    public bool Fallo { get; set; }
}

/// <summary>El puerto por el que los handlers de evento de prueba dejan y cuentan su rastro.</summary>
public interface IEntregasRecibidas
{
    Task<int> ContarAsync(Guid eventoId, string handler, CancellationToken cancellationToken);

    /// <summary>Guarda el rastro de inmediato, sin transacción: queda aunque el handler lance después.</summary>
    Task DejarAsync(Guid eventoId, string handler, bool fallo, CancellationToken cancellationToken);
}

public sealed class EntregasRecibidas(PruebasDbContext db, IClock reloj) : IEntregasRecibidas
{
    public Task<int> ContarAsync(Guid eventoId, string handler, CancellationToken cancellationToken) =>
        db.Entregas.AsNoTracking().CountAsync(e => e.EventoId == eventoId && e.Handler == handler, cancellationToken);

    public async Task DejarAsync(Guid eventoId, string handler, bool fallo, CancellationToken cancellationToken)
    {
        db.Entregas.Add(new EntregaRecibida { EventoId = eventoId, Handler = handler, RecibidoEn = reloj.UtcNow, Fallo = fallo });
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>La excepción con la que un handler de evento de prueba rechaza una entrega.</summary>
public sealed class EntregaRechazadaDePrueba(Guid eventoId) : Exception($"El handler de prueba rechazó el evento {eventoId}.");

/// <summary>
/// El handler de <see cref="AnotacionHecha"/>. Solo acepta el evento si ya ve la anotación: como lee por otra
/// conexión, eso solo pasa cuando la transacción del comando ya se confirmó.
/// </summary>
public sealed class AlHacerseUnaAnotacion(IAnotaciones anotaciones, IEntregasRecibidas entregas) : IEventHandler<AnotacionHecha>
{
    public async Task HandleAsync(AnotacionHecha evento, CancellationToken cancellationToken)
    {
        if (await anotaciones.LeerTextoAsync(evento.AnotacionId, cancellationToken) is null)
        {
            throw new InvalidOperationException($"El evento {evento.EventoId} llegó antes que su anotación {evento.AnotacionId}.");
        }

        await entregas.DejarAsync(evento.EventoId, nameof(AlHacerseUnaAnotacion), fallo: false, cancellationToken);
    }
}

/// <summary>El handler de <see cref="EventoDePrueba"/>: deja su rastro, tarda y falla según lo que el evento pide.</summary>
public sealed class EscuchaDePrueba(IEntregasRecibidas entregas) : IEventHandler<EventoDePrueba>
{
    public async Task HandleAsync(EventoDePrueba evento, CancellationToken cancellationToken)
    {
        var anteriores = await entregas.ContarAsync(evento.EventoId, nameof(EscuchaDePrueba), cancellationToken);
        var falla = anteriores < evento.FallasAntesDeEntregar;

        await entregas.DejarAsync(evento.EventoId, nameof(EscuchaDePrueba), falla, cancellationToken);

        if (evento.DemoraMs > 0)
        {
            await Task.Delay(evento.DemoraMs, cancellationToken);
        }

        if (falla)
        {
            throw new EntregaRechazadaDePrueba(evento.EventoId);
        }
    }
}

/// <summary>El primer handler de <see cref="EventoConDosHandlers"/>.</summary>
public sealed class PrimeraEscucha(IEntregasRecibidas entregas) : IEventHandler<EventoConDosHandlers>
{
    public Task HandleAsync(EventoConDosHandlers evento, CancellationToken cancellationToken) =>
        entregas.DejarAsync(evento.EventoId, nameof(PrimeraEscucha), fallo: false, cancellationToken);
}

/// <summary>El segundo handler de <see cref="EventoConDosHandlers"/>.</summary>
public sealed class SegundaEscucha(IEntregasRecibidas entregas) : IEventHandler<EventoConDosHandlers>
{
    public Task HandleAsync(EventoConDosHandlers evento, CancellationToken cancellationToken) =>
        entregas.DejarAsync(evento.EventoId, nameof(SegundaEscucha), fallo: false, cancellationToken);
}
