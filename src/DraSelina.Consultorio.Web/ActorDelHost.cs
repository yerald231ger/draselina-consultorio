using System.Security.Claims;
using DraSelina.Consultorio.Kernel;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// El único servicio que dice quién actúa (AD-22). Uno por ámbito: el de la petición en SSR estático o el del
/// circuito en Interactive Server. Lee el usuario del <see cref="AuthenticationStateProvider"/> de ese mismo
/// ámbito: autenticado y en el área de administración es <see cref="Actor.Administrador"/> (no hay registro público);
/// si no, <see cref="Actor.Paciente"/>. El área de administración es una petición HTTP bajo <c>/admin</c> o el circuito de
/// Interactive Server (solo la administración abre circuito, AD-11). Una petición HTTP fuera de <c>/admin</c> (la reserva,
/// <c>/c/...</c>, el archivo de calendario) es <see cref="Actor.Paciente"/> aunque traiga la cookie de sesión: la reserva
/// del mismo navegador que tiene la administración abierta es del Paciente (entrada 5.10). Autenticado quiere decir con la cookie de sesión de Identity, que solo se emite tras el
/// segundo factor: la cookie parcial del segundo factor no es la del esquema por omisión y no cuenta (Identidad/).
/// <see cref="Actor.Sistema"/> no se deduce: lo declara quien crea el ámbito, desde el
/// host, con <see cref="DeclararSistema"/>.
/// </summary>
public sealed class ActorDelHost(
    AuthenticationStateProvider autenticacion,
    IHttpContextAccessor peticiones,
    IServiceProvider ambito) : IActorActual
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
        if (_esSistema) return Actor.Sistema;

        return await EsAdministradorAsync() is not null ? Actor.Administrador : Actor.Paciente;
    }

    /// <summary>
    /// El id de la cuenta (claim <see cref="ClaimTypes.NameIdentifier"/>) si el actor es <see cref="Actor.Administrador"/>;
    /// nulo si no. En un ámbito sin actor lanza, igual que <see cref="ObtenerAsync"/>.
    /// </summary>
    public async Task<string?> IdDelAdministradorAsync()
    {
        if (_esSistema) return null;

        return (await EsAdministradorAsync())?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    // El usuario si actúa como Administrador: autenticado y en el área de administración; nulo si no.
    private async Task<ClaimsPrincipal?> EsAdministradorAsync()
    {
        var usuario = await UsuarioAsync();
        return usuario.Identity?.IsAuthenticated == true && EnElAreaDeAdministracion() ? usuario : null;
    }

    // Este ámbito es el de una petición HTTP si la petición en curso lo tiene como sus servicios: así se distingue del
    // circuito, que es otro ámbito aunque el acceso a la petición devuelva la de su conexión (/_blazor). En una petición,
    // solo bajo /admin; fuera de una petición (el circuito, o un ámbito que crea el host), como siempre.
    private bool EnElAreaDeAdministracion() =>
        peticiones.HttpContext is not { } peticion
        || !ReferenceEquals(peticion.RequestServices, ambito)
        || peticion.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase);

    private async Task<ClaimsPrincipal> UsuarioAsync()
    {
        try
        {
            // BL0013 avisa de un estado viejo cuando se guarda; aquí no se guarda: se pide de nuevo en cada llamada,
            // así que siempre es el vigente del ámbito y no hace falta escuchar AuthenticationStateChanged.
#pragma warning disable BL0013
            return (await autenticacion.GetAuthenticationStateAsync()).User;
#pragma warning restore BL0013
        }
        catch (InvalidOperationException error)
        {
            throw new InvalidOperationException(
                "Este ámbito no tiene actor: no viene de una petición ni de un circuito, y no se declaró Sistema "
                + $"({nameof(ActorDelHost)}.{nameof(DeclararSistema)}). Quien crea el ámbito lo declara, desde el host.",
                error);
        }
    }
}
