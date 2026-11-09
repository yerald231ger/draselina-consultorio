using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// <see cref="IAgendaApi"/>. El enlace se deriva cada vez; no se guarda ni se registra (AD-19, AD-23). El archivo de
/// calendario sale de <see cref="GeneradorDeCalendario"/>, con el lugar que da Contenido (AD-15); leerlo no escribe nada.
/// La Agenda del día sale de <see cref="ITramosDeLaFecha"/> y de <see cref="ICitasDelDia"/>, sin rastrear.
/// </summary>
public sealed class AgendaApi(
    IOptions<OpcionesDeEnlaceDeGestion> opciones,
    ICitasDelCalendario citas,
    ITramosDeLaFecha tramosDeLaFecha,
    ICitasDelDia citasDelDia,
    IContenidoApi contenido,
    IClock reloj) : IAgendaApi
{
    private readonly ClaveDeGestion _clave = opciones.Value.ClaveDeGestion();
    private readonly string _urlBase = opciones.Value.UrlBase!.TrimEnd('/');

    public string ObtenerEnlaceDeGestion(CitaId citaId) =>
        $"{_urlBase}/c/{EnlaceDeGestion.Derivar(_clave, citaId).Token}";

    public async Task<ArchivoDeCalendario?> ObtenerArchivoDeCalendarioAsync(CitaId citaId, CancellationToken cancellationToken) =>
        Archivo(await citas.PorIdAsync(citaId, cancellationToken));

    public async Task<string?> ObtenerTipoDeVisitaAsync(CitaId citaId, CancellationToken cancellationToken) =>
        (await citas.PorIdAsync(citaId, cancellationToken))?.TipoDeVisita;

    // Los Tramos los decide la agenda de atención (INV-V1): la custom de esa fecha o la base. Las Citas, todas las
    // Agendadas que empiezan ese día local, aunque esté fuera de los Tramos (una Cita del Consultorio en sábado).
    public async Task<AgendaDelDia> AgendaDelDiaAsync(DateOnly fecha, CancellationToken cancellationToken)
    {
        var tramos = await tramosDeLaFecha.VigentesAsync(fecha, cancellationToken);
        var citas = await citasDelDia.AgendadasEntreAsync(
            HoraLocal.AUtc(fecha, TimeOnly.MinValue), HoraLocal.AUtc(fecha.AddDays(1), TimeOnly.MinValue), cancellationToken);
        return new AgendaDelDia(!tramos.SinTramos, citas);
    }

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
