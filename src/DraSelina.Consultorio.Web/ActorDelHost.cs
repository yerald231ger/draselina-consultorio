using DraSelina.Consultorio.Kernel;
using Microsoft.AspNetCore.Components.Authorization;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// El único servicio que dice quién actúa (AD-22). Uno por ámbito: el de la petición en SSR estático o el del
/// circuito en Interactive Server. Lee el usuario del <see cref="AuthenticationStateProvider"/> de ese mismo
/// ámbito: autenticado es <see cref="Actor.Administrador"/> (no hay registro público); sin autenticar,
/// <see cref="Actor.Paciente"/>. <see cref="Actor.Sistema"/> no se deduce: lo declara quien crea el ámbito, desde el
/// host, con <see cref="DeclararSistema"/>.
/// </summary>
public sealed class ActorDelHost(AuthenticationStateProvider autenticacion) : IActorActual
{
    private bool _esSistema;

    /// <summary>
    /// Declara que este ámbito actúa como <see cref="Actor.Sistema"/>. Lo llama quien crea el ámbito, desde el host,
    /// pidiendo esta clase al contenedor; no está en <see cref="IActorActual"/>, así un handler no puede declararse
    /// <c>Sistema</c>. Un módulo no ve esta clase: cómo obtiene un ámbito <c>Sistema</c> el trabajo programado de un
    /// módulo se decide con el primer trabajo programado. La entrega de eventos no lo llama: una política usa el
    /// actor que viene en el evento.
    /// </summary>
    public void DeclararSistema() => _esSistema = true;

    /// <summary>
    /// El actor del ámbito. En un ámbito que no viene de una petición ni de un circuito y que no se declaró
    /// <c>Sistema</c> no hay actor: lanza <see cref="InvalidOperationException"/>. Es un error de programación, no un
    /// <c>Resultado</c>.
    /// </summary>
    public async Task<Actor> ObtenerAsync()
    {
        if (_esSistema)
        {
            return Actor.Sistema;
        }

        AuthenticationState estado;
        try
        {
            // BL0013 avisa de un estado viejo cuando se guarda; aquí no se guarda: se pide de nuevo en cada llamada,
            // así que siempre es el vigente del ámbito y no hace falta escuchar AuthenticationStateChanged.
#pragma warning disable BL0013
            estado = await autenticacion.GetAuthenticationStateAsync();
#pragma warning restore BL0013
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidOperationException(
                "Este ámbito no tiene actor: no viene de una petición ni de un circuito, y no se declaró Sistema "
                + $"({nameof(ActorDelHost)}.{nameof(DeclararSistema)}). Quien crea el ámbito lo declara, desde el host.",
                error);
        }

        return estado.User.Identity?.IsAuthenticated == true ? Actor.Administrador : Actor.Paciente;
    }
}
