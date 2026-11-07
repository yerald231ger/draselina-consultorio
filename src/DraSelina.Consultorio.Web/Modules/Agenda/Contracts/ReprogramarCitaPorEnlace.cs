using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El Paciente mueve su Cita a otro Horario libre desde su Enlace de gestión (CAP-7, FR-9). Es la misma Cita, con su
/// mismo enlace (AD-7, RN-15): no se crea otra ni se cancela la anterior. Se busca por el hash del token, nunca por el
/// id (AD-19). Sin solicitud: enviar otra vez la misma fecha y hora no cambia nada (Gerardo, 2026-11-06). El token nunca
/// va a registros (AD-23): <see cref="ToString"/> no lo muestra.
/// </summary>
public sealed class ReprogramarCitaPorEnlace : ICommand<CitaReprogramadaPorEnlace>
{
    /// <summary>Lo que va después de <c>/c/</c> en el Enlace de gestión.</summary>
    [Required(ErrorMessage = "Falta el enlace de tu cita.")]
    public string? Token { get; set; }

    /// <summary>Fecha local del consultorio (AD-17).</summary>
    [Required(ErrorMessage = "Falta el día de la cita.")]
    public DateOnly? Fecha { get; set; }

    /// <summary>Hora local del consultorio (AD-17).</summary>
    [Required(ErrorMessage = "Falta la hora de la cita.")]
    public TimeOnly? Hora { get; set; }

    public override string ToString() => $"ReprogramarCitaPorEnlace(Token = oculto, Fecha = {Fecha}, Hora = {Hora})";
}

/// <summary>La Cita ya en su nuevo horario.</summary>
/// <param name="Fecha">Fecha local del consultorio.</param>
/// <param name="Hora">Hora local de inicio.</param>
public sealed record CitaReprogramadaPorEnlace(DateOnly Fecha, TimeOnly Hora);
