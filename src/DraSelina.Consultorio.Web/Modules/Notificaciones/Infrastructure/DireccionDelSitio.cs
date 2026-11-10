using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary><see cref="IDireccionDelSitio"/> con <see cref="OpcionesDeNotificaciones.UrlDelSitio"/>.</summary>
public sealed class DireccionDelSitio(IOptions<OpcionesDeNotificaciones> opciones) : IDireccionDelSitio
{
    public string Reservar() => $"{Base()}/reservar";

    public string Agenda(DateOnly fecha) =>
        $"{Base()}/admin?fecha={fecha.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}";

    private string Base() =>
        Uri.TryCreate(opciones.Value.UrlDelSitio, UriKind.Absolute, out var url) && url.Scheme is "https" or "http"
            ? url.ToString().TrimEnd('/')
            : throw new InvalidOperationException(
                $"Falta {OpcionesDeNotificaciones.Seccion}:{nameof(OpcionesDeNotificaciones.UrlDelSitio)}, o no es una dirección absoluta http o https.");
}
