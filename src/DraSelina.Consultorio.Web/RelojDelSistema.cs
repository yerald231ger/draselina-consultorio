using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// El reloj de la aplicación: la hora del sistema. Es el único lugar que la lee (AD-17). Solo en Development suma
/// <see cref="OpcionesDeReloj.Desplazamiento"/>, que se lee en cada llamada: al guardar
/// <c>appsettings.Development.local.json</c> el reloj cambia sin reiniciar la web ni perder la base (Gerardo, 2026-11-07).
/// </summary>
/// <param name="desplazamiento">Nulo fuera de Development: la hora real, siempre.</param>
public sealed class RelojDelSistema(IOptionsMonitor<OpcionesDeReloj>? desplazamiento = null) : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow + Desplazamiento();

    private TimeSpan Desplazamiento()
    {
        if (desplazamiento is null)
        {
            return TimeSpan.Zero;
        }

        try
        {
            return desplazamiento.CurrentValue.Desplazamiento ?? TimeSpan.Zero;
        }
        catch (InvalidOperationException error)
        {
            // Un valor que no es una duración ("23h") rompe cada lectura del reloj: que el mensaje diga dónde está.
            throw new InvalidOperationException(
                $"{OpcionesDeReloj.Seccion}:{nameof(OpcionesDeReloj.Desplazamiento)} en {OpcionesDeReloj.ArchivoLocal} no es una duración válida; se escribe como \"23:00:00\".",
                error);
        }
    }
}

/// <summary>
/// La sección <c>Reloj</c> de la configuración. Sirve para recorrer en local lo que depende de la hora (la cancelación
/// tardía, el Recordatorio, No se presentó) sin esperar a que pase (épica 3, entrada 5).
/// </summary>
public sealed class OpcionesDeReloj
{
    public const string Seccion = "Reloj";

    /// <summary>El archivo de la web donde se pone en Development; no va al repositorio.</summary>
    public const string ArchivoLocal = "appsettings.Development.local.json";

    /// <summary>Lo que se suma a la hora real; por ejemplo <c>"23:00:00"</c>. Solo vale en Development.</summary>
    public TimeSpan? Desplazamiento { get; set; }

    /// <summary>
    /// Fuera de Development el host no arranca con un desplazamiento distinto de cero: un reloj movido en otro
    /// entorno cambiaría qué Citas ya pasaron, cuáles son tardías y qué horarios se ofrecen.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si el entorno no es Development y el desplazamiento no es cero.</exception>
    public static void ExigirSoloEnDevelopment(IConfiguration configuracion, IHostEnvironment entorno)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        ArgumentNullException.ThrowIfNull(entorno);

        var desplazamiento = configuracion.GetSection(Seccion).GetValue<TimeSpan?>(nameof(Desplazamiento));
        if (!entorno.IsDevelopment() && desplazamiento is { } valor && valor != TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"{Seccion}:{nameof(Desplazamiento)} solo vale en Development; en {entorno.EnvironmentName} se quita de la configuración.");
        }
    }
}
