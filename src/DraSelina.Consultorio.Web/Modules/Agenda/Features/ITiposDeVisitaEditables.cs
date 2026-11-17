using DraSelina.Consultorio.Agenda.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>Puerto de lectura de los Tipos de visita activos con todo lo que se edita (entrada 6.7). Sin rastrear.</summary>
public interface ITiposDeVisitaEditables
{
    /// <summary>Los activos, por su orden y luego por nombre.</summary>
    Task<IReadOnlyList<TipoDeVisitaEditable>> ListarAsync(CancellationToken cancellationToken);
}
