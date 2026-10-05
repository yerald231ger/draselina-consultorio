namespace DraSelina.Consultorio.Kernel;

/// <summary>El reloj se inyecta: el dominio nunca lee la hora del sistema (AD-17).</summary>
public interface IClock
{
    /// <summary>El instante actual, en UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
