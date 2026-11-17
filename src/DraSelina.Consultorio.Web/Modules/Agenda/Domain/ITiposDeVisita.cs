using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>El puerto de escritura de <see cref="TipoDeVisita"/> (T-16): obtener, agregar y guardar.</summary>
public interface ITiposDeVisita
{
    /// <summary>El tipo de visita con ese id, sin rastrear; nulo si no existe.</summary>
    Task<TipoDeVisita?> ObtenerAsync(TipoDeVisitaId id, CancellationToken cancellationToken);

    /// <summary>El tipo de visita con ese id, rastreado para cambiarlo; nulo si no existe.</summary>
    Task<TipoDeVisita?> ObtenerParaCambiarAsync(TipoDeVisitaId id, CancellationToken cancellationToken);

    /// <summary>
    /// Cuántos tipos activos hay, con sus filas bloqueadas hasta el fin de la transacción (<c>FOR UPDATE</c>): dos
    /// desactivaciones a la vez no dejan el sistema sin tipos activos. Lanza si no hay una transacción abierta.
    /// </summary>
    Task<int> ContarActivosParaCambiarAsync(CancellationToken cancellationToken);

    /// <summary>Agrega un tipo nuevo; se guarda con <see cref="GuardarAsync"/>.</summary>
    void Agregar(TipoDeVisita tipo);

    /// <summary>Guarda lo agregado y lo cambiado.</summary>
    Task GuardarAsync(CancellationToken cancellationToken);
}
