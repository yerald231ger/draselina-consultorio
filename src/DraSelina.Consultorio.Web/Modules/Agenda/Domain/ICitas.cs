namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// El puerto de escritura de <see cref="Cita"/> (T-16). Guarda de forma explícita dentro de la transacción del
/// handler y traduce el choque de traslape a un valor; nunca deja escapar la excepción de la base (T-22, AD-8).
/// </summary>
public interface ICitas
{
    void Agregar(Cita cita);

    /// <summary>
    /// Guarda lo agregado. Si una Cita choca con otra no cancelada en la restricción de exclusión, devuelve
    /// <see cref="GuardadoDeCita.HorarioOcupado"/>: la transacción queda abortada y el handler la deshace.
    /// </summary>
    Task<GuardadoDeCita> GuardarAsync(CancellationToken cancellationToken);
}

/// <summary>Cómo terminó <see cref="ICitas.GuardarAsync"/>.</summary>
public enum GuardadoDeCita
{
    Guardada = 1,

    /// <summary>Otra Cita no cancelada ya ocupa parte del rango (RN-1).</summary>
    HorarioOcupado = 2,
}
