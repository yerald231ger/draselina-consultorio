using DraSelina.Consultorio.Agenda.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de los tipos de visita activos, reservables en línea o no (FR-15). No pasa por el repositorio del
/// tipo ni rastrea lo que lee.
/// </summary>
public interface ITiposDeVisitaActivos
{
    /// <summary>Los tipos activos, por su orden.</summary>
    Task<IReadOnlyList<TipoDeVisitaActivo>> ListarAsync(CancellationToken cancellationToken);
}
