using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El Enlace de gestión y el archivo de calendario de una Cita. El enlace se deriva cada vez; no se guarda ni se registra
/// (AD-19, AD-23). El archivo sale de <see cref="GeneradorDeCalendario"/>, con el lugar que da Contenido (AD-15). Lo usan
/// <see cref="AgendaApi"/> (para Notificaciones) y <see cref="ArchivoDeCalendarioPorEnlaceHandler"/> (la descarga por
/// Enlace de gestión), con la misma regla.
/// </summary>
public sealed class CalendarioDeLaCita(IOptions<OpcionesDeEnlaceDeGestion> opciones, IContenidoApi contenido, IClock reloj)
{
    private readonly ClaveDeGestion _clave = opciones.Value.ClaveDeGestion();
    private readonly string _urlBase = opciones.Value.UrlBase!.TrimEnd('/');

    /// <summary>El Enlace de gestión completo de <paramref name="citaId"/>.</summary>
    public string Enlace(CitaId citaId) =>
        $"{_urlBase}/c/{EnlaceDeGestion.Derivar(_clave, citaId).Token}";

    /// <summary>
    /// El archivo de <paramref name="cita"/>, o nulo si no hay Cita, no está Agendada o su inicio ya pasó (RN-22);
    /// "ya pasó" se mide desde el inicio (RN-31).
    /// </summary>
    public ArchivoDeCalendario? Archivo(CitaDelCalendario? cita)
    {
        var ahora = reloj.UtcNow;
        if (cita is null || cita.Estado != EstadoCita.Agendada || cita.Inicio <= ahora)
        {
            return null;
        }

        return GeneradorDeCalendario.Generar(
            cita.Id,
            cita.Inicio,
            cita.Fin,
            cita.TipoDeVisita,
            contenido.DatosDelConsultorio,
            contenido.DatosDeLaDoctora.Nombre,
            Enlace(cita.Id),
            new Uri(_urlBase).Host,
            ahora);
    }
}
