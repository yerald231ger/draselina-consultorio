using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.BuildingBlocks.Contracts;

/// <summary>El único camino de la interfaz hacia el backend (AD-10). Exclusivo de la interfaz (AD-3).</summary>
public interface ISender
{
    Task<Resultado<T>> SendAsync<T>(ICommand<T> comando, CancellationToken cancellationToken = default);

    Task<Resultado<T>> SendAsync<T>(IQuery<T> consulta, CancellationToken cancellationToken = default);
}
