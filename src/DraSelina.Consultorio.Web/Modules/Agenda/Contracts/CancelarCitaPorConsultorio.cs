using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El Administrador cancela una Cita a nombre del Consultorio (CAP-14, FR-17, AD-9). Solo una Cita Agendada cuyo inicio
/// no ha llegado (RN-31); desde el inicio vuelve <see cref="CodigosDeAgenda.YaEmpezo"/>, y una Cancelada por el Paciente
/// o que No se presentó, <see cref="CodigosDeAgenda.CitaNoAgendada"/>. Sin umbral de tardía. Cancelar otra vez una Cita
/// ya cancelada por el Consultorio no cambia nada y devuelve lo guardado. Publica <see cref="CitaCancelada"/> con motivo
/// <see cref="CitaCancelada.MotivoConsultorio"/>: Notificaciones manda la disculpa al Paciente con correo. Solo el
/// Administrador (AD-22).
/// </summary>
public sealed class CancelarCitaPorConsultorio : ICommand<CitaCanceladaPorConsultorio>, IRequiereAdministrador
{
    [Required(ErrorMessage = "Falta la cita.")]
    public CitaId? CitaId { get; set; }

    public override string ToString() => $"CancelarCitaPorConsultorio(CitaId = {CitaId})";
}

/// <summary>La Cita ya cancelada por el Consultorio.</summary>
/// <param name="CitaId">El id de la Cita.</param>
/// <param name="Estado">Cómo se ve en la Agenda: <see cref="EstadoEnLaAgenda.Cancelada"/>.</param>
/// <param name="Cancelacion">Por qué: <see cref="CancelacionEnLaAgenda.PorElConsultorio"/>.</param>
public sealed record CitaCanceladaPorConsultorio(CitaId CitaId, EstadoEnLaAgenda Estado, CancelacionEnLaAgenda? Cancelacion);
