using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Las Citas de una fecha local del consultorio, para la Agenda del Administrador (CAP-12, FR-14): todas, en cualquier
/// estado, en orden de inicio, con el nombre del Paciente y el motivo de consulta. Solo el Administrador (AD-22). Solo
/// lee. Lo que se ve de cada una lo decide Agenda: Atendida se deriva (RN-19, RN-31).
/// </summary>
/// <param name="Fecha">La fecha local del consultorio.</param>
public sealed record CitasDeLaFecha(DateOnly Fecha) : IQuery<IReadOnlyList<CitaDeLaFecha>>, IRequiereAdministrador;

/// <summary>
/// Una Cita de la Agenda del Administrador. Nombre y motivo nunca van a registros (AD-23): <see cref="ToString"/> no los
/// muestra.
/// </summary>
/// <param name="CitaId">La Cita.</param>
/// <param name="Inicio">Hora local de inicio.</param>
/// <param name="Fin">Hora local de fin.</param>
/// <param name="Paciente">El nombre del Paciente tal como se guardó (RN-29); nulo si Pacientes no lo conoce.</param>
/// <param name="MotivoDeConsulta">Nulo si la Cita (del Consultorio) no lo tiene.</param>
/// <param name="Estado">Cómo se ve en la Agenda.</param>
/// <param name="Cancelacion">Por qué se canceló; solo en <see cref="EstadoEnLaAgenda.Cancelada"/>.</param>
public sealed record CitaDeLaFecha(
    CitaId CitaId,
    TimeOnly Inicio,
    TimeOnly Fin,
    string? Paciente,
    string? MotivoDeConsulta,
    EstadoEnLaAgenda Estado,
    CancelacionEnLaAgenda? Cancelacion)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append(
            $"CitaId = {CitaId}, Inicio = {Inicio:HH:mm}, Fin = {Fin:HH:mm}, Paciente = oculto, MotivoDeConsulta = oculto, "
            + $"Estado = {Estado}, Cancelacion = {Cancelacion}");
        return true;
    }
}

/// <summary>
/// El estado de una Cita como lo ve el Administrador. <see cref="Atendida"/> no se guarda: es una Cita Agendada cuyo
/// inicio ya llegó (RN-19, RN-31).
/// </summary>
public enum EstadoEnLaAgenda
{
    Agendada = 1,
    Atendida = 2,
    NoSePresento = 3,
    Cancelada = 4,
}

/// <summary>Por qué se canceló una Cita, como lo distingue la Agenda: por el Paciente, tarde o por el Consultorio.</summary>
public enum CancelacionEnLaAgenda
{
    PorElPaciente = 1,
    Tardia = 2,
    PorElConsultorio = 3,
}
