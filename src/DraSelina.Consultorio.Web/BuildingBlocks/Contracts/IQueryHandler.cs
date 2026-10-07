using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.BuildingBlocks.Contracts;

/// <summary>
/// El handler de una consulta: exactamente uno por consulta (AD-10). Quien lo necesita lo pide al contenedor por
/// esta interfaz y nunca conoce la clase que la implementa.
/// </summary>
public interface IQueryHandler<in TConsulta, T>
    where TConsulta : IQuery<T>
{
    Task<Resultado<T>> HandleAsync(TConsulta consulta, CancellationToken cancellationToken);
}
