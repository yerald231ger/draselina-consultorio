using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de la agenda de atención para un rango de fechas, para la Agenda del Administrador: la base completa
/// y las agendas custom del rango, sin rastrear y sin transacción. Quien decide qué Tramos valen cada día es
/// <see cref="AgendaAtencion.TramosVigentes"/> (INV-V1). Para una sola fecha está <see cref="ITramosDeLaFecha"/>; para
/// escribir, <see cref="IAgendaAtencion"/>.
/// </summary>
public interface IAgendaDelRango
{
    /// <summary>
    /// La agenda de atención con la base y solo las agendas custom de <c>[desde, hasta]</c>, fechas locales. Solo
    /// sirve para contestar por fechas de ese rango.
    /// </summary>
    Task<AgendaAtencion> LeerAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);
}
