using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace DraSelina.Consultorio.Web.Identidad;

/// <summary>
/// La cookie parcial del segundo factor para quien todavía no lo activó: la misma que deja
/// <see cref="SignInManager{TUser}.PasswordSignInAsync(TUser, string, bool, bool)"/> cuando pide el código (esquema
/// <see cref="IdentityConstants.TwoFactorUserIdScheme"/>, con el id de la cuenta en <see cref="ClaimTypes.Name"/> y su
/// sello de seguridad), así <see cref="SignInManager{TUser}.GetTwoFactorAuthenticationUserAsync"/> la lee igual. Desde
/// Identity 11 esa lectura compara el sello con el de la cuenta: si cambió (por ejemplo, al crear la clave de la app
/// autenticadora), la cookie ya no sirve y hay que guardarla de nuevo. No es la del esquema por omisión: no cuenta como
/// sesión. Cada página de <c>/admin/entrar/...</c> revisa si la cuenta tiene o no el segundo factor activo antes de
/// aceptarla, así esta cookie solo abre el enrolamiento.
/// </summary>
public static class CookieParcial
{
    public static async Task GuardarAsync(HttpContext contexto, SignInManager<IdentityUser> entrada, IdentityUser cuenta)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(cuenta);

        var usuarios = entrada.UserManager;
        var identidad = new ClaimsIdentity(IdentityConstants.TwoFactorUserIdScheme);
        identidad.AddClaim(new Claim(ClaimTypes.Name, await usuarios.GetUserIdAsync(cuenta)));
        if (usuarios.SupportsUserSecurityStamp)
        {
            identidad.AddClaim(new Claim(
                entrada.Options.ClaimsIdentity.SecurityStampClaimType,
                await usuarios.GetSecurityStampAsync(cuenta)));
        }

        await contexto.SignInAsync(IdentityConstants.TwoFactorUserIdScheme, new ClaimsPrincipal(identidad));
    }
}
