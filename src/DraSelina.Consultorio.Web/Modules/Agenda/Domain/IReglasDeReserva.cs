namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>El puerto de escritura de <see cref="ReglasDeReserva"/> (T-16). Hay una sola fila, sembrada con la migración.</summary>
public interface IReglasDeReserva
{
    Task<ReglasDeReserva> ObtenerAsync(CancellationToken cancellationToken);

    /// <summary>Guarda lo cambiado en las reglas obtenidas con <see cref="ObtenerAsync"/>.</summary>
    Task GuardarAsync(ReglasDeReserva reglas, CancellationToken cancellationToken);
}
