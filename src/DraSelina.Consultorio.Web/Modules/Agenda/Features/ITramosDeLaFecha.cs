using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de la agenda de atención para una fecha: los Tramos que valen ese día (INV-V1, RN-4), sin rastrear
/// y sin transacción. Para escribir está <see cref="IAgendaAtencion"/>.
/// </summary>
public interface ITramosDeLaFecha
{
    /// <summary>Los Tramos de <paramref name="fecha"/> (local): los de su agenda custom si existe; si no, los de la base.</summary>
    Task<TramosVigentes> VigentesAsync(DateOnly fecha, CancellationToken cancellationToken);
}
