using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Si el Paciente que se está capturando en "Agendar cita" ya existe (entrada 5.4, UJ-4), y si existe, su próxima Cita
/// Agendada y la última que ya empezó. Pacientes contesta quién es, sin crearlo (RN-25: el mismo teléfono con otro
/// nombre es otro Paciente); Agenda pone las Citas (RN-29). Solo informa: no cambia lo que se agenda. Solo el
/// Administrador (AD-22). Solo lee. Nombre y teléfono nunca van a registros (AD-23): <see cref="ToString"/> no los
/// muestra.
/// </summary>
/// <param name="Nombre">Como se capturó.</param>
/// <param name="Telefono">Celular de México, en la forma que acepta el comando.</param>
public sealed record PacienteReconocido(string Nombre, string Telefono) : IQuery<PacienteDeLaCita>, IRequiereAdministrador
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append("Nombre = oculto, Telefono = oculto");
        return true;
    }
}

/// <summary>Lo que se sabe del Paciente capturado.</summary>
/// <param name="Existe">Ya hay un Paciente con ese teléfono y ese nombre.</param>
/// <param name="Proxima">Su Cita Agendada futura más cercana; nula si no tiene o si no existe.</param>
/// <param name="Anterior">La última que ya empezó, en cualquier estado; nula si no tiene o si no existe.</param>
public sealed record PacienteDeLaCita(bool Existe, CitaDelPaciente? Proxima, CitaDelPaciente? Anterior)
{
    /// <summary>No hay un Paciente con ese teléfono y ese nombre.</summary>
    public static PacienteDeLaCita Desconocido { get; } = new(false, null, null);
}

/// <summary>Una Cita del Paciente reconocido, con su estado como se ve en la Agenda.</summary>
/// <param name="Fecha">Fecha local de inicio.</param>
/// <param name="Hora">Hora local de inicio.</param>
/// <param name="TipoDeVisita">El nombre de su tipo; nulo si el tipo ya no existe.</param>
/// <param name="Estado">Como se ve en la Agenda: Atendida se deriva (RN-31).</param>
/// <param name="Cancelacion">Por qué se canceló; solo en <see cref="EstadoEnLaAgenda.Cancelada"/>.</param>
public sealed record CitaDelPaciente(
    DateOnly Fecha, TimeOnly Hora, string? TipoDeVisita, EstadoEnLaAgenda Estado, CancelacionEnLaAgenda? Cancelacion);
