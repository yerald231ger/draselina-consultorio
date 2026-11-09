using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DraSelina.Consultorio.Web.Identidad;

/// <summary>
/// La cuenta del Administrador, dato inicial del host (AD-25). La corre el MigrationService después de migrar; la web
/// nunca la crea. Es idempotente: si ya hay una cuenta, con ese correo o con otro, no la toca (ni su contraseña ni su
/// segundo factor) ni crea otra: hay un solo Administrador (AD-22). No hay registro público (AD-22): esta es la única forma de que exista una cuenta.
/// </summary>
public static class SiembraDelAdministrador
{
    /// <summary>La sección de configuración: <c>Administrador:Correo</c> y <c>Administrador:Contrasena</c>.</summary>
    public const string Seccion = "Administrador";

    /// <summary>El correo cuando la configuración no da otro. <c>[ASSUMPTION]</c> hasta que exista el real.</summary>
    public const string CorreoPorOmision = "administrador@consultorio.local";

    /// <summary>Cómo terminó la siembra.</summary>
    public enum Siembra
    {
        Creada = 1,
        YaExistia = 2,
    }

    /// <summary>
    /// Crea la cuenta del Administrador con el correo y la contraseña de <paramref name="configuracion"/>, sin segundo
    /// factor: el primer inicio de sesión lleva al enrolamiento. Si ya existe alguna cuenta, no hace nada y no pide la
    /// contraseña.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Si falta la contraseña o no cumple la política de Identity. El mensaje lleva solo los códigos de error de Identity:
    /// sus descripciones pueden llevar el correo, y el MigrationService lo registra (AD-23).
    /// </exception>
    public static async Task<Siembra> SembrarAsync(UserManager<IdentityUser> usuarios, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(usuarios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var seccion = configuracion.GetSection(Seccion);
        var correo = seccion["Correo"] is { Length: > 0 } dado ? dado.Trim() : CorreoPorOmision;

        if (await usuarios.Users.AnyAsync())
        {
            return Siembra.YaExistia;
        }

        var contrasena = seccion["Contrasena"];
        if (string.IsNullOrEmpty(contrasena))
        {
            throw new InvalidOperationException(
                $"Falta la contraseña inicial del Administrador ({Seccion}:Contrasena). La da el parámetro secreto "
                + "'clave-administrador' del AppHost: dotnet user-secrets set \"Parameters:clave-administrador\" \"...\" "
                + "--project src/DraSelina.Consultorio.AppHost");
        }

        var cuenta = new IdentityUser
        {
            UserName = correo,
            Email = correo,
            EmailConfirmed = true,
            LockoutEnabled = true,
        };

        var creada = await usuarios.CreateAsync(cuenta, contrasena);
        if (!creada.Succeeded)
        {
            throw new InvalidOperationException(
                "La cuenta del Administrador no se creó: la contraseña inicial (parámetro 'clave-administrador' del "
                + "AppHost) o el correo no cumplen la política de Identity. Pide al menos 6 caracteres, con una "
                + "mayúscula, una minúscula, un dígito y un símbolo. Códigos de Identity: "
                + string.Join(", ", creada.Errors.Select(e => e.Code)));
        }

        return Siembra.Creada;
    }
}
