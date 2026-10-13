using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>Un reloj que la prueba mueve: el tiempo solo pasa cuando la prueba llama a <see cref="Avanzar"/>.</summary>
public sealed class RelojDePrueba(DateTimeOffset inicio) : IClock
{
    private readonly Lock _candado = new();
    private DateTimeOffset _ahora = inicio.ToUniversalTime();

    /// <summary>Un reloj que arranca en un instante fijo cualquiera.</summary>
    public RelojDePrueba() : this(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero))
    {
    }

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_candado) return _ahora;
        }
    }

    public void Avanzar(TimeSpan cuanto)
    {
        lock (_candado) _ahora += cuanto;
    }
}
