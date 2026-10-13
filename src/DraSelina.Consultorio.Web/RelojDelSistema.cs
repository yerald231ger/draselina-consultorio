using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Web;

/// <summary>El reloj de la aplicación: la hora del sistema. Es el único lugar que la lee (AD-17).</summary>
public sealed class RelojDelSistema : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
