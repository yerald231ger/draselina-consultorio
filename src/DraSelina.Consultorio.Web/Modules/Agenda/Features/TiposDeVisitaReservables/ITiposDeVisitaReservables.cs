using DraSelina.Consultorio.Agenda.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de los tipos de visita que se agendan en línea (T-17). No pasa por el repositorio del tipo ni
/// rastrea lo que lee.
/// </summary>
public interface ITiposDeVisitaReservables
{
    /// <summary>Los tipos activos y reservables en línea, por su orden.</summary>
    Task<IReadOnlyList<TipoDeVisitaReservable>> ListarAsync(CancellationToken cancellationToken);
}
