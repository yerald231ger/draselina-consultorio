using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El Administrador quita el horario propio de una fecha: el día vuelve a la Agenda base (RN-12). Borrar cuenta igual que
/// guardar (RN-9): trae la lista completa de Citas que vio fuera de los Tramos de la base ese día, con su decisión por
/// cada una; si ya no coincide, no borra nada y vuelve <see cref="CodigosDeAgenda.CitasFuera"/>. Una fecha sin custom
/// vuelve <see cref="CodigosDeAgenda.FechaSinCustom"/>; una anterior a hoy, <see cref="CodigosDeAgenda.FechaPasada"/>.
/// Las elegidas para cancelar se cancelan después, cada una por su cuenta, a nombre del Administrador (P-1). Solo el
/// Administrador (AD-22).
/// </summary>
public sealed class BorrarAgendaCustom : ICommand<AgendaCustomBorrada>, IRequiereAdministrador, IValidatableObject
{
    /// <summary>Fecha local del consultorio (AD-17).</summary>
    [Required(ErrorMessage = "Falta la fecha.")]
    public DateOnly? Fecha { get; set; }

    /// <summary>Las Citas que quedan fuera al volver a la base, como las vio el Administrador, con su decisión.</summary>
    public List<DecisionSobreCita> Citas { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        DecisionSobreCita.Validar(Citas, nameof(Citas));

    public override string ToString() => $"BorrarAgendaCustom {{ Fecha = {Fecha:yyyy-MM-dd}, Citas = {Citas.Count} }}";
}

/// <summary>La fecha que volvió a la Agenda base.</summary>
public sealed record AgendaCustomBorrada(DateOnly Fecha);
