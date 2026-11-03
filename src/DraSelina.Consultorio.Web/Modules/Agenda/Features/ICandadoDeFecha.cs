namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El candado por fecha local (AD-26, RN-20): crear o mover una Cita en línea, y guardar o borrar una agenda custom,
/// hacen fila sobre la fecha y leen los tramos vigentes después de tomarlo. Se toma dentro de la transacción abierta
/// del handler y se suelta solo al confirmarla o deshacerla.
/// </summary>
public interface ICandadoDeFecha
{
    /// <summary>
    /// Espera hasta tener el candado de la fecha local <paramref name="fecha"/>. Lanza si no hay una transacción
    /// abierta: fuera de ella el candado se soltaría en el acto.
    /// </summary>
    Task TomarAsync(DateOnly fecha, CancellationToken cancellationToken);
}
