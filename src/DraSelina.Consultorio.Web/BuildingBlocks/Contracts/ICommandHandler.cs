using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.BuildingBlocks.Contracts;

/// <summary>
/// El handler de un comando: exactamente uno por comando (AD-10). Quien lo necesita lo pide al contenedor por
/// esta interfaz y nunca conoce la clase que la implementa.
/// </summary>
public interface ICommandHandler<in TComando, T>
    where TComando : ICommand<T>
{
    Task<Resultado<T>> HandleAsync(TComando comando, CancellationToken cancellationToken);
}
