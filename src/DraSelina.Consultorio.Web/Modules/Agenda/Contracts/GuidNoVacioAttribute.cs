using System.ComponentModel.DataAnnotations;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Un <see cref="Guid"/> distinto de <see cref="Guid.Empty"/>: <c>[Required]</c> deja pasar un campo oculto escrito a
/// mano con ceros. Un valor nulo pasa: que falte lo dice <c>[Required]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class GuidNoVacioAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is Guid guid && guid != Guid.Empty;
}
