using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de la disponibilidad (T-17): los rangos de las Citas no canceladas, de cualquier origen, que
/// ocupan algo de un periodo. No pasa por el repositorio de la Cita ni rastrea lo que lee.
/// </summary>
public interface ICitasActivas
{
    /// <summary>
    /// Los rangos <c>[inicio, fin)</c> de las Citas no canceladas que traslapan <c>[desde, hasta)</c>, en UTC.
    /// </summary>
    Task<IReadOnlyList<RangoDeTiempo>> EntreAsync(DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken);
}
