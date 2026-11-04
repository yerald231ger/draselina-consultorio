using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// <see cref="IAgendaApi"/>. El enlace se deriva cada vez; no se guarda ni se registra (AD-19, AD-23). El archivo de
/// calendario sale de <see cref="GeneradorDeCalendario"/>, con el lugar que da Contenido (AD-15); leerlo no escribe nada.
/// </summary>
public sealed class AgendaApi(
    IOptions<OpcionesDeEnlaceDeGestion> opciones,
    ICitasDelCalendario citas,
    IContenidoApi contenido,
    IClock reloj) : IAgendaApi
{
    private readonly ClaveDeGestion _clave = opciones.Value.ClaveDeGestion();
    private readonly string _urlBase = opciones.Value.UrlBase!.TrimEnd('/');

    public string ObtenerEnlaceDeGestion(CitaId citaId) =>
        $"{_urlBase}/c/{EnlaceDeGestion.Derivar(_clave, citaId).Token}";

    public async Task<ArchivoDeCalendario?> ObtenerArchivoDeCalendarioAsync(CitaId citaId, CancellationToken cancellationToken) =>
        Archivo(await citas.PorIdAsync(citaId, cancellationToken));

    /// <summary>
    /// El archivo de <paramref name="cita"/>, o nulo si no hay Cita, no está Agendada o su inicio ya pasó (RN-22);
    /// "ya pasó" se mide desde el inicio (RN-31). La misma regla para la descarga por Enlace de gestión
    /// (<see cref="ArchivoDeCalendarioPorEnlaceHandler"/>) y para Notificaciones.
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
            ObtenerEnlaceDeGestion(cita.Id),
            new Uri(_urlBase).Host,
            ahora);
    }
}
