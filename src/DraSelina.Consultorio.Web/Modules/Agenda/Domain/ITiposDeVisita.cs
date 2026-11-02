using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>El puerto de <see cref="TipoDeVisita"/> (T-16). Hoy solo lee: agregar y guardar llegan con su edición.</summary>
public interface ITiposDeVisita
{
    /// <summary>El tipo de visita con ese id; nulo si no existe.</summary>
    Task<TipoDeVisita?> ObtenerAsync(TipoDeVisitaId id, CancellationToken cancellationToken);
}
