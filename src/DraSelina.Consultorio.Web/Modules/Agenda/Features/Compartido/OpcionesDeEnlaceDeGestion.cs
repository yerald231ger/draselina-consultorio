using System.Text;
using DraSelina.Consultorio.Agenda.Domain;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// La configuración del host con la que Agenda arma el Enlace de gestión (AD-19), en la sección
/// <c>EnlaceDeGestion</c>. Las dos se validan al arrancar el host: sin ellas no arranca, en lugar de fallar al reservar.
/// </summary>
public sealed class OpcionesDeEnlaceDeGestion
{
    public const string Seccion = "EnlaceDeGestion";

    /// <summary>
    /// La clave del host, al menos 32 bytes en UTF-8. Vive fuera de la base y del repositorio: en local la da el
    /// parámetro secreto del AppHost. Cambiarla cambia todos los enlaces.
    /// </summary>
    public string? Clave { get; set; }

    /// <summary>La dirección absoluta del sitio, sin <c>/c/</c>: <c>https://citas.ejemplo.mx</c>. En local, la de la web del AppHost.</summary>
    public string? UrlBase { get; set; }

    /// <summary>La clave como la usa el dominio. Solo después de validar.</summary>
    public ClaveDeGestion ClaveDeGestion() => Domain.ClaveDeGestion.Crear(Encoding.UTF8.GetBytes(Clave!));
}

/// <summary>Valida <see cref="OpcionesDeEnlaceDeGestion"/>; cada mensaje nombra la configuración que falta o no vale.</summary>
public sealed class ValidacionDeOpcionesDeEnlaceDeGestion : IValidateOptions<OpcionesDeEnlaceDeGestion>
{
    public ValidateOptionsResult Validate(string? name, OpcionesDeEnlaceDeGestion opciones)
    {
        var fallas = new List<string>();
        var clave = $"{OpcionesDeEnlaceDeGestion.Seccion}:{nameof(OpcionesDeEnlaceDeGestion.Clave)}";
        var urlBase = $"{OpcionesDeEnlaceDeGestion.Seccion}:{nameof(OpcionesDeEnlaceDeGestion.UrlBase)}";

        if (string.IsNullOrEmpty(opciones.Clave))
        {
            fallas.Add($"Falta la configuración {clave}: la clave del Enlace de gestión (AD-19). En local la da el parámetro secreto del AppHost.");
        }
        else if (Encoding.UTF8.GetByteCount(opciones.Clave) < ClaveDeGestion.BytesMinimos)
        {
            fallas.Add($"La configuración {clave} lleva al menos {ClaveDeGestion.BytesMinimos} bytes.");
        }

        if (string.IsNullOrWhiteSpace(opciones.UrlBase))
        {
            fallas.Add($"Falta la configuración {urlBase}: la dirección del sitio con la que se arma el Enlace de gestión.");
        }
        else if (!Uri.TryCreate(opciones.UrlBase, UriKind.Absolute, out var url) || url.Scheme is not ("https" or "http"))
        {
            fallas.Add($"La configuración {urlBase} es una dirección absoluta http o https.");
        }

        return fallas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(fallas);
    }
}
