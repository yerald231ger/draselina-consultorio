using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// <see cref="IAgendaApi"/>. El enlace y el archivo de calendario salen de <see cref="CalendarioDeLaCita"/> (AD-19,
/// AD-23, AD-15); leerlos no escribe nada. La Agenda del día sale de <see cref="ITramosDeLaFecha"/> y de
/// <see cref="ICitasDelDia"/>, sin rastrear.
/// </summary>
public sealed class AgendaApi(
    CalendarioDeLaCita calendario,
    ICitasDelCalendario citas,
    ITramosDeLaFecha tramosDeLaFecha,
    ICitasDelDia citasDelDia) : IAgendaApi
{
    public string ObtenerEnlaceDeGestion(CitaId citaId) => calendario.Enlace(citaId);

    public async Task<ArchivoDeCalendario?> ObtenerArchivoDeCalendarioAsync(CitaId citaId, CancellationToken cancellationToken) =>
        calendario.Archivo(await citas.PorIdAsync(citaId, cancellationToken));

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
}
