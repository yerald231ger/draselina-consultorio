using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El Administrador marca que el Paciente no se presentó a su Cita (CAP-14, FR-16, AD-9). Solo una Cita Agendada cuyo
/// inicio ya llegó (RN-31); antes vuelve <see cref="CodigosDeAgenda.AunNoEmpieza"/>, y una Cancelada,
/// <see cref="CodigosDeAgenda.CitaNoAgendada"/>. Marcar otra vez una Cita que ya No se presentó no cambia nada y devuelve
/// lo guardado. No publica evento (T-23). Solo el Administrador (AD-22).
/// </summary>
public sealed class MarcarNoSePresento : ICommand<CitaMarcadaNoSePresento>, IRequiereAdministrador
{
    [Required(ErrorMessage = "Falta la cita.")]
    public CitaId? CitaId { get; set; }

    public override string ToString() => $"MarcarNoSePresento(CitaId = {CitaId})";
}

/// <summary>La Cita ya marcada.</summary>
/// <param name="CitaId">El id de la Cita.</param>
/// <param name="Estado">Cómo se ve en la Agenda: <see cref="EstadoEnLaAgenda.NoSePresento"/>.</param>
public sealed record CitaMarcadaNoSePresento(CitaId CitaId, EstadoEnLaAgenda Estado);
