using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Cómo se distingue un día (EXPERIENCE.md, "Fecha con Agenda custom"): una sola clasificación para la Agenda (entrada
/// 5.2) y el Calendario de atención (entrada 6.1). Design Notes de la 5.2: custom sin Tramos, no se atiende; custom en un
/// día cuya base no tiene Tramos, día abierto; cualquier otra custom, horario especial (recorta o alarga).
/// </summary>
public static class ClasificacionDelDia
{
    /// <summary>El tipo del día, dados sus Tramos vigentes y si la base de ese día de la semana tiene Tramos.</summary>
    public static TipoDeDia De(TramosVigentes vigentes, bool baseConTramos)
    {
        ArgumentNullException.ThrowIfNull(vigentes);
        return (vigentes.Origen, vigentes.SinTramos, baseConTramos) switch
        {
            (OrigenDeTramos.Base, false, _) => TipoDeDia.Normal,
            (OrigenDeTramos.Base, true, _) => TipoDeDia.SinAtencion,
            (OrigenDeTramos.Custom, true, _) => TipoDeDia.CustomSinAtencion,
            (OrigenDeTramos.Custom, false, false) => TipoDeDia.DiaAbierto,
            (OrigenDeTramos.Custom, false, true) => TipoDeDia.HorarioEspecial,
            _ => throw new InvalidOperationException($"Origen de Tramos desconocido: {vigentes.Origen}."),
        };
    }

    /// <summary>El tipo de <paramref name="fecha"/> en <paramref name="agenda"/>.</summary>
    public static TipoDeDia De(AgendaAtencion agenda, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(agenda);
        return De(agenda.TramosVigentes(fecha), agenda.TramosBase(fecha.DayOfWeek).Count > 0);
    }
}
