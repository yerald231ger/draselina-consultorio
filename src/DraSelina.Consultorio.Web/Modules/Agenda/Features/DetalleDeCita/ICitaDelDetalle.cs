using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura del Detalle de Cita (entrada 6): la Cita por su id, sin rastrear y sin transacción, con el nombre
/// de su tipo. Lee lo guardado aunque el contexto tenga la Cita rastreada de un comando anterior del mismo circuito. No
/// registra lo que lee (AD-23).
/// </summary>
public interface ICitaDelDetalle
{
    /// <summary>La Cita <paramref name="id"/>, sin rastrear; nula si no existe.</summary>
    Task<CitaLeidaDelDetalle?> LeerAsync(CitaId id, CancellationToken cancellationToken);
}

/// <summary>La Cita leída, sin rastrear, para preguntarle sus reglas, y el nombre de su tipo.</summary>
/// <param name="Cita">La Cita, sin rastrear: no se cambia ni se guarda.</param>
/// <param name="TipoDeVisita">El nombre de su tipo; nulo si el tipo ya no existe.</param>
public sealed record CitaLeidaDelDetalle(Cita Cita, string? TipoDeVisita);
