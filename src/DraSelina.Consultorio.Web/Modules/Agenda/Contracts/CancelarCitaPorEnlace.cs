using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El Paciente cancela su Cita desde su Enlace de gestión (CAP-8, FR-10, UJ-2 pasos 4 a 7). Se busca por el hash del
/// token, nunca por el id (AD-19). Cancelar otra vez una Cita ya cancelada no cambia nada y devuelve lo guardado
/// (Gerardo, 2026-11-06). Si es tardía lo decide la Cita (RN-16). El token nunca va a registros (AD-23):
/// <see cref="ToString"/> no lo muestra.
/// </summary>
public sealed class CancelarCitaPorEnlace : ICommand<CitaCanceladaPorEnlace>
{
    /// <summary>Lo que va después de <c>/c/</c> en el Enlace de gestión.</summary>
    [Required(ErrorMessage = "Falta el enlace de tu cita.")]
    public string? Token { get; set; }

    public override string ToString() => "CancelarCitaPorEnlace(Token = oculto)";
}

/// <summary>La Cita ya cancelada.</summary>
/// <param name="Fecha">Fecha local del consultorio.</param>
/// <param name="Hora">Hora local de inicio.</param>
/// <param name="TipoDeVisita">El nombre de hoy del tipo de visita.</param>
/// <param name="Tardia">Si quedó como cancelación tardía (RN-16).</param>
/// <param name="PorElConsultorio">Si ya la había cancelado el Consultorio: un envío sobre una Cita ya cancelada devuelve
/// el motivo guardado.</param>
public sealed record CitaCanceladaPorEnlace(
    DateOnly Fecha,
    TimeOnly Hora,
    string TipoDeVisita,
    bool Tardia,
    bool PorElConsultorio);
