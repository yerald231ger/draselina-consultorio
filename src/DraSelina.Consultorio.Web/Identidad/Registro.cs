using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Web.Identidad;

/// <summary>
/// Lo que el host registra de Identity (AD-22): la cuenta del Administrador, su segundo factor por app autenticadora
/// (TOTP, Decision 2026-11-09) y la cookie de sesión. Sin registro público, sin páginas de Identity de plantilla, sin
/// roles y sin correo: las únicas páginas son las de <c>Identidad/Paginas</c>, bajo <see cref="RutaDeEntrar"/>.
/// </summary>
public static class Registro
{
    /// <summary>Inicio de sesión: adonde lleva la cookie a quien pide una página de administración sin sesión.</summary>
    public const string RutaDeEntrar = "/admin/entrar";

    /// <summary>El código de la app autenticadora, con la contraseña ya comprobada y el segundo factor activo.</summary>
    public const string RutaDelSegundoFactor = "/admin/entrar/segundo-factor";

    /// <summary>Enrolamiento: el QR y la clave, con la contraseña ya comprobada y el segundo factor sin activar.</summary>
    public const string RutaDeActivar = "/admin/entrar/activar";

    /// <summary>Un código de recuperación en lugar del de la app.</summary>
    public const string RutaDeRecuperacion = "/admin/entrar/recuperacion";

    /// <summary>Cerrar la sesión: un POST con antiforgery desde la navegación de administración.</summary>
    public const string RutaDeSalir = "/admin/salir";

    /// <summary>La Agenda: adonde se llega al entrar, si no se pidió otra página.</summary>
    public const string RutaDeAdministracion = "/admin";

    /// <summary>
    /// Lo que comparten la web y el MigrationService: el contexto de <c>identidad</c>, el <see cref="UserManager{TUser}"/>
    /// con sus almacenes y las reglas de la cuenta (contraseña y bloqueo por intentos, los de Identity por omisión
    /// <c>[ASSUMPTION]</c>). Así la siembra del Administrador aplica la misma política de contraseña que el inicio de sesión.
    /// </summary>
    public static IdentityBuilder AddCuentasDeIdentidad(this IServiceCollection servicios, string cadenaDeConexion)
    {
        servicios.AddDbContext<IdentidadDbContext>(opciones => IdentidadDbContext.Configurar(opciones, cadenaDeConexion));

        return servicios
            .AddIdentityCore<IdentityUser>(opciones =>
            {
                opciones.Stores.SchemaVersion = IdentidadDbContext.VersionDelEsquema;
                opciones.User.RequireUniqueEmail = true;
                // Sin correo de confirmación: la cuenta la crea la siembra, ya confirmada.
                opciones.SignIn.RequireConfirmedAccount = false;
                opciones.SignIn.RequireConfirmedEmail = false;
                // Bloqueo por intentos: los valores por omisión de Identity (5 intentos, 5 minutos) [ASSUMPTION].
                opciones.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<IdentidadDbContext>();
    }

    /// <summary>
    /// Identity en la web: las cuentas, el <see cref="SignInManager{TUser}"/>, el proveedor de códigos de la app
    /// autenticadora y las cookies de Identity. La de sesión es la del esquema por omisión; la parcial del segundo
    /// factor (<see cref="IdentityConstants.TwoFactorUserIdScheme"/>) no lo es, así que no cuenta como sesión.
    /// </summary>
    public static IServiceCollection AddIdentidad(this IServiceCollection servicios, string cadenaDeConexion)
    {
        servicios
            .AddAuthentication(opciones =>
            {
                opciones.DefaultScheme = IdentityConstants.ApplicationScheme;
                opciones.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        servicios.ConfigureApplicationCookie(cookie =>
        {
            cookie.LoginPath = RutaDeEntrar;
            cookie.AccessDeniedPath = RutaDeEntrar;
            cookie.LogoutPath = RutaDeSalir;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        servicios
            .AddCuentasDeIdentidad(cadenaDeConexion)
            .AddSignInManager()
            .AddTokenProvider<AuthenticatorTokenProvider<IdentityUser>>(TokenOptions.DefaultAuthenticatorProvider);

        return servicios.AddAuthorization();
    }

    /// <summary>
    /// El único endpoint de Identity fuera de las páginas: <see cref="RutaDeSalir"/>, un POST con antiforgery que cierra
    /// la sesión (y la parcial del segundo factor, si hubiera) y lleva a <see cref="RutaDeEntrar"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapIdentidad(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost(RutaDeSalir, async (SignInManager<IdentityUser> entrada) =>
        {
            await entrada.SignOutAsync();
            return Results.LocalRedirect(RutaDeEntrar);
        }).WithMetadata(new RequireAntiforgeryTokenAttribute());

        return rutas;
    }

    /// <summary>
    /// Una dirección de regreso solo si es de administración y del propio sitio: <c>/admin</c>, <c>/admin?...</c> o algo
    /// bajo <c>/admin/</c>, sin esquema ni anfitrión. Cualquier otra cosa lleva a <see cref="RutaDeAdministracion"/>.
    /// </summary>
    public static string RegresoSeguro(string? regreso) =>
        regreso is { Length: > 0 }
        && (regreso == RutaDeAdministracion
            || regreso.StartsWith(RutaDeAdministracion + "/", StringComparison.Ordinal)
            || regreso.StartsWith(RutaDeAdministracion + "?", StringComparison.Ordinal))
        && !regreso.StartsWith(RutaDeEntrar, StringComparison.Ordinal)
        && !regreso.Contains("//", StringComparison.Ordinal)
        && !regreso.Contains('\\', StringComparison.Ordinal)
            ? regreso
            : RutaDeAdministracion;
}
