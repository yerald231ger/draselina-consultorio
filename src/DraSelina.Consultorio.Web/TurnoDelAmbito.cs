using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// Un handler a la vez por ámbito. En Interactive Server el ámbito es el circuito y sus <c>DbContext</c> duran lo que
/// él: dos componentes de una misma página que consultan a la vez (las secciones de Configuración, cada una en su
/// <c>OnInitializedAsync</c>) usarían el mismo contexto en paralelo y EF lanza "A second operation was started…".
/// Con un turno por ámbito, el segundo espera a que termine el primero. Ningún handler llama a otro handler, así que
/// el turno no se pide dos veces en una misma llamada (recorrido 6.8).
/// </summary>
public sealed class TurnoDelAmbito : IDisposable
{
    private readonly SemaphoreSlim _turno = new(1, 1);

    /// <summary>Corre <paramref name="siguiente"/> cuando sea su turno en este ámbito.</summary>
    public async Task<Resultado<T>> EnSuTurnoAsync<T>(Func<Task<Resultado<T>>> siguiente, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(siguiente);
        await _turno.WaitAsync(cancellationToken);
        try
        {
            return await siguiente();
        }
        finally
        {
            _turno.Release();
        }
    }

    public void Dispose() => _turno.Dispose();
}

/// <summary>Todo comando espera su turno en el ámbito (<see cref="TurnoDelAmbito"/>); es el decorador de más afuera.</summary>
public sealed class TurnoDeComandos<TComando, T>(ICommandHandler<TComando, T> siguiente, TurnoDelAmbito turno)
    : ICommandHandler<TComando, T>
    where TComando : ICommand<T>
{
    /// <summary>El handler que envuelve.</summary>
    public ICommandHandler<TComando, T> Siguiente => siguiente;

    public Task<Resultado<T>> HandleAsync(TComando comando, CancellationToken cancellationToken) =>
        turno.EnSuTurnoAsync(() => siguiente.HandleAsync(comando, cancellationToken), cancellationToken);
}

/// <summary>Toda consulta espera su turno en el ámbito (<see cref="TurnoDelAmbito"/>); es el decorador de más afuera.</summary>
public sealed class TurnoDeConsultas<TConsulta, T>(IQueryHandler<TConsulta, T> siguiente, TurnoDelAmbito turno)
    : IQueryHandler<TConsulta, T>
    where TConsulta : IQuery<T>
{
    /// <summary>El handler que envuelve.</summary>
    public IQueryHandler<TConsulta, T> Siguiente => siguiente;

    public Task<Resultado<T>> HandleAsync(TConsulta consulta, CancellationToken cancellationToken) =>
        turno.EnSuTurnoAsync(() => siguiente.HandleAsync(consulta, cancellationToken), cancellationToken);
}
