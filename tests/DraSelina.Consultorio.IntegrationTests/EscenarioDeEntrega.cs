using DraSelina.Consultorio.IntegrationTests.Pruebas;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Lo que necesita una prueba de entrega de eventos para que nadie más toque sus eventos: una base propia, migrada,
/// una web sin entrega automática sobre ella y un reloj que solo mueve la prueba. Todo lo que devuelve sale de la
/// base.
/// </summary>
internal sealed class EscenarioDeEntrega : IAsyncDisposable
{
    private readonly Arnes _arnes;

    private EscenarioDeEntrega(Arnes arnes, string cadena)
    {
        _arnes = arnes;
        Cadena = cadena;
        Web = arnes.ArrancarWebSinEntregaAutomatica(cadena, Reloj);
    }

    /// <summary>La base propia del escenario.</summary>
    public string Cadena { get; }

    public RelojDePrueba Reloj { get; } = new();

    /// <summary>La web del escenario: no entrega nada hasta que la prueba llama a <see cref="VueltaAsync"/>.</summary>
    public WebApplicationFactory<Program> Web { get; }

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    public static async Task<EscenarioDeEntrega> CrearAsync(Arnes arnes) =>
        new(arnes, await arnes.CrearBaseMigradaAsync(Cancelacion));

    /// <summary>Otra web sin entrega automática sobre la misma base y con el mismo reloj: una segunda instancia.</summary>
    public WebApplicationFactory<Program> OtraWeb() => _arnes.ArrancarWebSinEntregaAutomatica(Cadena, Reloj);

    /// <summary>
    /// Publica un evento por el puerto de outbox de los tipos de prueba, dentro de una transacción que confirma.
    /// Ocurre a la hora del reloj, salvo que se indique otra. Devuelve su <c>EventoId</c>.
    /// </summary>
    public async Task<Guid> PublicarAsync<TEvento>(Func<Guid, TEvento> evento, Guid claveDeOrden, DateTimeOffset? ocurridoEn = null)
        where TEvento : notnull
    {
        var eventoId = Guid.CreateVersion7();

        await using var ambito = Web.Services.CreateAsyncScope();
        var transaccion = ambito.ServiceProvider.GetRequiredService<ITransaccionDePruebas>();
        var outbox = ambito.ServiceProvider.GetRequiredService<IOutboxDePruebas>();

        await using var tx = await transaccion.AbrirAsync(Cancelacion);
        await outbox.PublicarAsync(evento(eventoId), eventoId, claveDeOrden, ocurridoEn ?? Reloj.UtcNow, Cancelacion);
        await tx.ConfirmarAsync(Cancelacion);

        return eventoId;
    }

    /// <summary>Da una vuelta de entrega en la web del escenario. Devuelve cuántos eventos intentó.</summary>
    public Task<int> VueltaAsync() => Arnes.DarUnaVueltaDeEntregaAsync(Web, Cancelacion);

    /// <summary>La fila del evento en <c>pruebas.outbox</c>.</summary>
    public async Task<FilaLeidaDeOutbox> EventoAsync(Guid eventoId) =>
        await Consultas.EventoAsync(Cadena, PruebasDbContext.Esquema, eventoId, Cancelacion)
        ?? throw new InvalidOperationException($"El evento {eventoId} no está en el outbox.");

    /// <summary>El rastro que los handlers dejaron de ese evento, en el orden en que lo recibieron.</summary>
    public async Task<List<EntregaRecibida>> EntregasAsync(Guid eventoId)
    {
        await using var contexto = _arnes.CrearContextoDePruebas(Cadena);
        return await contexto.Entregas.AsNoTracking().Where(e => e.EventoId == eventoId).OrderBy(e => e.Id).ToListAsync(Cancelacion);
    }

    public ValueTask DisposeAsync() => Web.DisposeAsync();
}

/// <summary>Esperar a que algo pase en la base, preguntando seguido. Solo para lo que hace el servicio en segundo plano.</summary>
internal static class Espera
{
    public static async Task<bool> HastaAsync(Func<Task<bool>> condicion, TimeSpan limite, CancellationToken cancelacion)
    {
        var fin = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fin)
        {
            if (await condicion())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancelacion);
        }

        return await condicion();
    }
}
