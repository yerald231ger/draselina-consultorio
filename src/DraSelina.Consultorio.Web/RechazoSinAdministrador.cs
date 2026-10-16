using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// Rechaza todo comando marcado con <see cref="IRequiereAdministrador"/> que no llegue de un
/// <see cref="Actor.Administrador"/> (AD-22): devuelve <see cref="Resultado"/> no autorizado y no valida ni llama al
/// handler. No se confía en que la página esté protegida. Un comando no marcado pasa sin pedir el actor.
/// </summary>
public sealed class RechazoSinAdministradorDeComandos<TComando, T>(ICommandHandler<TComando, T> siguiente, IActorActual actor)
    : ICommandHandler<TComando, T>
    where TComando : ICommand<T>
{
    /// <summary>El handler que envuelve.</summary>
    public ICommandHandler<TComando, T> Siguiente => siguiente;

    public Task<Resultado<T>> HandleAsync(TComando comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        return comando is IRequiereAdministrador
            ? RechazoSinAdministrador.SoloAdministradorAsync(actor, () => siguiente.HandleAsync(comando, cancellationToken))
            : siguiente.HandleAsync(comando, cancellationToken);
    }
}

/// <summary>
/// Rechaza toda consulta marcada con <see cref="IRequiereAdministrador"/> que no llegue de un
/// <see cref="Actor.Administrador"/> (AD-22), igual que <see cref="RechazoSinAdministradorDeComandos{TComando, T}"/>.
/// Una consulta no marcada pasa sin pedir el actor.
/// </summary>
public sealed class RechazoSinAdministradorDeConsultas<TConsulta, T>(IQueryHandler<TConsulta, T> siguiente, IActorActual actor)
    : IQueryHandler<TConsulta, T>
    where TConsulta : IQuery<T>
{
    /// <summary>El handler que envuelve.</summary>
    public IQueryHandler<TConsulta, T> Siguiente => siguiente;

    public Task<Resultado<T>> HandleAsync(TConsulta consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        return consulta is IRequiereAdministrador
            ? RechazoSinAdministrador.SoloAdministradorAsync(actor, () => siguiente.HandleAsync(consulta, cancellationToken))
            : siguiente.HandleAsync(consulta, cancellationToken);
    }
}

/// <summary>La regla, una sola vez para comandos y consultas: solo el <see cref="Actor.Administrador"/> pasa.</summary>
internal static class RechazoSinAdministrador
{
    public static async Task<Resultado<T>> SoloAdministradorAsync<T>(IActorActual actor, Func<Task<Resultado<T>>> siguiente) =>
        await actor.ObtenerAsync() == Actor.Administrador
            ? await siguiente()
            : Resultado.NoAutorizado().Como<T>();
}
