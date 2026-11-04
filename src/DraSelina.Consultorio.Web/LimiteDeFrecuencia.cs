using System.Globalization;
using System.Threading.RateLimiting;
using DraSelina.Consultorio.UI;
using DraSelina.Consultorio.UI.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.Web;

/// <summary>Cuántas peticiones se permiten por dirección IP en una ventana de tiempo.</summary>
public sealed class Ventana
{
    public int Permitidas { get; set; }

    public TimeSpan Duracion { get; set; }
}

/// <summary>
/// Los límites de frecuencia de las páginas de reserva (FR-7, CAP-5), de la sección <c>LimiteDeFrecuencia</c> de la
/// configuración. Por IP: envíos (POST) de Tus datos y visitas (GET) a <c>/reservar</c>, <c>/reservar/horario</c> y
/// <c>/reservar/datos</c> (Gerardo, 2026-11-04).
/// </summary>
public sealed class OpcionesDeLimiteDeFrecuencia
{
    public const string Seccion = "LimiteDeFrecuencia";

    public Ventana Envios { get; set; } = new();

    public Ventana Visitas { get; set; } = new();
}

/// <summary>
/// El límite de frecuencia de la reserva: una política por IP que las páginas de reserva piden con
/// <c>[EnableRateLimiting(LimiteDeFrecuencia.Politica)]</c>. El resto del sitio, <c>/health</c>, los recursos
/// estáticos y <c>/c/…</c> no la llevan. Al pasarlo, 429 con la página amable de <see cref="DemasiadosIntentos"/>,
/// nunca un error técnico.
/// </summary>
/// <remarks>
/// La IP sale de la conexión. Detrás de un proxy habrá que leer los encabezados reenviados; se decide al preparar
/// producción.
/// </remarks>
public static class LimiteDeFrecuencia
{
    public const string Politica = DemasiadosIntentos.Politica;

    public static IServiceCollection AddLimiteDeFrecuencia(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios
            .AddOptions<OpcionesDeLimiteDeFrecuencia>()
            .Bind(configuracion.GetSection(OpcionesDeLimiteDeFrecuencia.Seccion))
            .Validate(o => Valida(o.Envios) && Valida(o.Visitas),
                $"La sección {OpcionesDeLimiteDeFrecuencia.Seccion} necesita Envios y Visitas con Permitidas y Duracion mayores que cero.")
            .ValidateOnStart();

        return servicios.AddRateLimiter(limitador =>
        {
            limitador.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limitador.OnRejected = RechazarAsync;
            limitador.AddPolicy(Politica, Particion);
        });
    }

    private static bool Valida(Ventana ventana) => ventana.Permitidas > 0 && ventana.Duracion > TimeSpan.Zero;

    // Envíos y visitas cuentan por separado: un POST de Tus datos no gasta visitas.
    private static RateLimitPartition<string> Particion(HttpContext contexto)
    {
        var opciones = contexto.RequestServices.GetRequiredService<IOptions<OpcionesDeLimiteDeFrecuencia>>().Value;
        var envio = HttpMethods.IsPost(contexto.Request.Method);
        var ventana = envio ? opciones.Envios : opciones.Visitas;
        var ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-ip";

        return RateLimitPartition.GetFixedWindowLimiter(
            $"{(envio ? "envios" : "visitas")}:{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = ventana.Permitidas,
                Window = ventana.Duracion,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }

    private static async ValueTask RechazarAsync(OnRejectedContext rechazo, CancellationToken cancelacion)
    {
        var http = rechazo.HttpContext;
        if (rechazo.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
        {
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        // La página completa, con el layout del sitio: App la pinta en lugar de la ruta pedida.
        var pagina = new RazorComponentResult<App>(new Dictionary<string, object?> { [nameof(App.Pagina)] = typeof(DemasiadosIntentos) })
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
        };
        await pagina.ExecuteAsync(http);
    }
}
