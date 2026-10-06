using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.EntityFrameworkCore.Internals;
using Wolverine.Runtime;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// Un renglón de la tabla que fija quién es dueño de la transacción: los handlers de ese namespace corren en la
/// transacción de ese <see cref="DbContext"/>.
/// </summary>
public sealed record ModuloConTransaccion(string EspacioDeHandlers, Type Contexto);

public static class RegistroDeTransacciones
{
    /// <summary>
    /// Da de alta un módulo en la política de transacción: todo handler de comando o de evento del namespace
    /// <paramref name="espacioDeHandlers"/> corre en la transacción de <typeparamref name="TContexto"/>.
    /// </summary>
    public static IServiceCollection AddTransaccionDeModulo<TContexto>(this IServiceCollection servicios, string espacioDeHandlers)
        where TContexto : DbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(espacioDeHandlers);
        return servicios.AddSingleton(new ModuloConTransaccion(espacioDeHandlers, typeof(TContexto)));
    }
}

/// <summary>
/// La transacción de un handler, abierta y cerrada por el host (<see cref="UnaTransaccionPorModulo"/>). Es la del
/// <see cref="DbContext"/> del módulo del handler, y los mensajes que el handler publica se escriben en ella.
/// Cuando el handler atiende un mensaje que llegó por una cola, en ella queda también ese mensaje marcado como
/// atendido.
/// El host nunca guarda: lo que el handler no guardó no queda. La usa el código que Wolverine genera para cada
/// handler; ningún handler la pide.
/// </summary>
public sealed class TransaccionDeModulo : IAsyncDisposable
{
    private readonly IDbContextTransaction _transaccion;
    private readonly EfCoreEnvelopeTransaction _sobres;
    private readonly MessageContext _mensajes;

    private TransaccionDeModulo(IDbContextTransaction transaccion, EfCoreEnvelopeTransaction sobres, MessageContext mensajes)
    {
        _transaccion = transaccion;
        _sobres = sobres;
        _mensajes = mensajes;
    }

    /// <summary>Abre la transacción del contexto y hace que los mensajes que se publiquen se escriban en ella.</summary>
    public static async Task<TransaccionDeModulo> AbrirAsync(DbContext contexto, MessageContext mensajes, CancellationToken cancelacion)
    {
        var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion).ConfigureAwait(false);
        try
        {
            var sobres = new EfCoreEnvelopeTransaction(contexto, mensajes);
            await mensajes.EnlistInOutboxAsync(sobres).ConfigureAwait(false);
            return new TransaccionDeModulo(transaccion, sobres, mensajes);
        }
        catch
        {
            await transaccion.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// El cierre de un comando: con un resultado exitoso confirma el cambio y los mensajes, juntos; con cualquier
    /// otro deshace todo.
    /// </summary>
    public Task CerrarAsync(Resultado? resultado, CancellationToken cancelacion) =>
        resultado is { EsExito: true } ? ConfirmarAsync(cancelacion) : DeshacerAsync(cancelacion);

    /// <summary>
    /// Confirma la transacción y después suelta los mensajes a sus colas. Si el handler atiende un mensaje que
    /// llegó por una cola, antes de confirmar lo marca como atendido en el almacenamiento de Wolverine, dentro de
    /// esta misma transacción: cambio, mensajes publicados y mensaje atendido se confirman juntos. No llama a
    /// <c>SaveChanges</c>: los contextos de módulo no llevan el modelo de Wolverine, así que todo eso se escribe
    /// con SQL en la transacción.
    /// </summary>
    public async Task ConfirmarAsync(CancellationToken cancelacion) =>
        await _sobres.CommitAsync(cancelacion).ConfigureAwait(false);

    private async Task DeshacerAsync(CancellationToken cancelacion)
    {
        await _transaccion.RollbackAsync(cancelacion).ConfigureAwait(false);

        // Los mensajes publicados se quedaron en memoria esperando la confirmación: se descartan.
        await _mensajes.ClearAllAsync().ConfigureAwait(false);
    }

    /// <summary>Si el handler lanzó, la transacción sigue abierta y aquí se deshace.</summary>
    public ValueTask DisposeAsync() => _transaccion.DisposeAsync();
}
