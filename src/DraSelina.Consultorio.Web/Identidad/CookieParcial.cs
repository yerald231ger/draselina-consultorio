using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace DraSelina.Consultorio.Web.Identidad;

/// <summary>
/// La cookie parcial del segundo factor para quien todavía no lo activó: la misma que deja
/// <see cref="SignInManager{TUser}.PasswordSignInAsync(TUser, string, bool, bool)"/> cuando pide el código (esquema
/// <see cref="IdentityConstants.TwoFactorUserIdScheme"/>, con el id de la cuenta en <see cref="ClaimTypes.Name"/>), así
/// <see cref="SignInManager{TUser}.GetTwoFactorAuthenticationUserAsync"/> la lee igual. No es la del esquema por
/// omisión: no cuenta como sesión. Cada página de <c>/admin/entrar/...</c> revisa si la cuenta tiene o no el segundo
/// factor activo antes de aceptarla, así esta cookie solo abre el enrolamiento.
/// </summary>
public static class CookieParcial
{
    public static Task GuardarAsync(HttpContext contexto, IdentityUser cuenta)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(cuenta);

        var identidad = new ClaimsIdentity(IdentityConstants.TwoFactorUserIdScheme);
        identidad.AddClaim(new Claim(ClaimTypes.Name, cuenta.Id));
        return contexto.SignInAsync(IdentityConstants.TwoFactorUserIdScheme, new ClaimsPrincipal(identidad));
    }
}
