using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El Administrador cambia la semana tipo (CAP-10, FR-12): trae los siete días, cada uno con sus Tramos (vacío: ese día
/// no se atiende, INV-B3), como un solo cambio (Gerardo, 2026-11-17). Aplica de inmediato (RN-5) a las fechas sin Agenda
/// custom; no cancela ni mueve ninguna Cita (RN-6) y no cambia las fechas con custom (RN-7). Si un día no vale, no se
/// guarda ninguno y el error va bajo <c>Dias[i]</c>. Solo el Administrador (AD-22).
/// </summary>
public sealed class CambiarAgendaBase : ICommand<AgendaBaseCambiada>, IRequiereAdministrador, IValidatableObject
{
    public const string MensajeDeSemana = "La semana tiene los siete días, cada uno una vez.";
    public const string MensajeDeTramosEncimados = "Dos tramos de este día se enciman. Revisa las horas.";
    public const string MensajeDeTramoCorto = "En cada tramo de este día debe caber al menos una cita del tipo de consulta más corto.";

    /// <summary>Los siete días de la semana, en cualquier orden.</summary>
    public List<DiaDeLaBaseAGuardar> Dias { get; set; } = [];

    /// <summary>Nombre de la propiedad de error del día en la posición <paramref name="indice"/>.</summary>
    public static string PropiedadDelDia(int indice) => $"{nameof(Dias)}[{indice}]";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Dias.Count != 7 || Dias.Any(d => d is null || !Enum.IsDefined(d.Dia)) || Dias.Select(d => d.Dia).Distinct().Count() != 7)
        {
            yield return new ValidationResult(MensajeDeSemana, [nameof(Dias)]);
            yield break;
        }

        for (var i = 0; i < Dias.Count; i++)
        {
            if (Dias[i].Tramos.Any(t => t?.Inicio is null || t.Fin is null))
            {
                yield return new ValidationResult(GuardarAgendaCustom.MensajeDeTramoIncompleto, [PropiedadDelDia(i)]);
            }
            else if (Dias[i].Tramos.Any(t => t.Inicio >= t.Fin))
            {
                yield return new ValidationResult(GuardarAgendaCustom.MensajeDeTramoAlReves, [PropiedadDelDia(i)]);
            }
        }
    }

    public override string ToString() => $"CambiarAgendaBase {{ Dias = {string.Join(", ", Dias.Select(d => $"{d?.Dia}: {d?.Tramos.Count}"))} }}";
}

/// <summary>Un día de la semana tipo con sus Tramos, en hora local.</summary>
public sealed class DiaDeLaBaseAGuardar
{
    public DayOfWeek Dia { get; set; }

    /// <summary>Vacío: ese día no se atiende.</summary>
    public List<TramoAGuardar> Tramos { get; set; } = [];
}

/// <summary>Lo que quedó al cambiar la semana tipo.</summary>
/// <param name="DiasCambiados">Los días cuyos Tramos cambiaron; vacío si la semana era la misma.</param>
/// <param name="FechasConCustom">Las fechas con Agenda custom de hoy en adelante: no cambiaron (RN-7).</param>
public sealed record AgendaBaseCambiada(IReadOnlyList<DayOfWeek> DiasCambiados, IReadOnlyList<DateOnly> FechasConCustom);
