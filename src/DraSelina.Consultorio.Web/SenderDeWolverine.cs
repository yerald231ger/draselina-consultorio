using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Wolverine;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// <see cref="ISender"/> sobre Wolverine (AD-10). Ejecuta el handler del comando o de la consulta en línea y
/// devuelve su <see cref="Resultado{T}"/>. Una excepción del handler sale tal cual: no se convierte en resultado.
/// </summary>
internal sealed class SenderDeWolverine(IMessageBus bus) : ISender
{
    public Task<Resultado<T>> SendAsync<T>(ICommand<T> comando, CancellationToken cancellationToken = default) =>
        DespacharAsync<T>(comando, cancellationToken);

    public Task<Resultado<T>> SendAsync<T>(IQuery<T> consulta, CancellationToken cancellationToken = default) =>
        DespacharAsync<T>(consulta, cancellationToken);

    private async Task<Resultado<T>> DespacharAsync<T>(object mensaje, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        // Sin handler, Wolverine lanza aquí una excepción que nombra el tipo del mensaje.
        var resultado = await bus.InvokeAsync<Resultado<T>>(mensaje, cancelacion);

        return resultado ?? throw new InvalidOperationException(
            $"El handler de {mensaje.GetType().FullName} no devolvió un {nameof(Resultado)}<{typeof(T).Name}>.");
    }
}
