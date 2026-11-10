using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// El historial de Citas de un Paciente para el Administrador (CAP-15, FR-18; entrada 5.7): su contacto (lo da Pacientes,
/// RN-29), un resumen y todas sus Citas, de la más reciente a la más antigua, con su estado como en la Agenda. Lo reúne
/// Agenda; la página no compone módulos (AD-10). Solo el Administrador (AD-22). Solo lee. Un Paciente que Pacientes no
/// conoce vuelve <see cref="CodigosDeAgenda.PacienteNoEncontrado"/>.
/// </summary>
/// <param name="PatientId">El Paciente: lo único que viaja en la dirección (AD-11, AD-23).</param>
public sealed record HistorialDelPaciente(PatientId PatientId) : IQuery<PacienteConHistorial>, IRequiereAdministrador;

/// <summary>
/// Un Paciente con su historial. Nombre, teléfono y correo nunca van a registros (AD-23): <see cref="ToString"/> no los
/// muestra. Sin motivo de consulta ni nota: se ven en el Detalle de cada Cita.
/// </summary>
/// <param name="PatientId">El Paciente.</param>
/// <param name="Paciente">Su contacto tal como se guardó.</param>
/// <param name="Resumen">Lo que cuenta Agenda de sus Citas.</param>
/// <param name="Citas">Todas sus Citas, en cualquier estado, de la de inicio más reciente a la más antigua.</param>
public sealed record PacienteConHistorial(
    PatientId PatientId,
    ContactoDelPaciente Paciente,
    ResumenDelHistorial Resumen,
    IReadOnlyList<CitaDelHistorial> Citas)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append($"PatientId = {PatientId}, Paciente = oculto, Resumen = {Resumen}, Citas = {Citas.Count}");
        return true;
    }
}

/// <summary>
/// El resumen del historial, calculado por Agenda en la misma consulta: "5 citas · 1 no se presentó · 2 cancelaciones
/// tardías" (Gerardo, 2026-11-10).
/// </summary>
/// <param name="Citas">Todas sus Citas, en cualquier estado.</param>
/// <param name="NoSePresento">Las marcadas No se presentó.</param>
/// <param name="CancelacionesTardias">Las canceladas tarde por el Paciente (RN-17).</param>
public sealed record ResumenDelHistorial(int Citas, int NoSePresento, int CancelacionesTardias);

/// <summary>Una Cita del historial, con su estado como se ve en la Agenda.</summary>
/// <param name="CitaId">La Cita: su Detalle está en <c>/admin/citas/{CitaId}</c>.</param>
/// <param name="Fecha">Fecha local de inicio.</param>
/// <param name="Hora">Hora local de inicio.</param>
/// <param name="TipoDeVisita">El nombre de su tipo; nulo si el tipo ya no existe.</param>
/// <param name="Origen">Quién la creó.</param>
/// <param name="Estado">Como se ve en la Agenda: Atendida se deriva (RN-19, RN-31).</param>
/// <param name="Cancelacion">Por qué se canceló; solo en <see cref="EstadoEnLaAgenda.Cancelada"/>.</param>
public sealed record CitaDelHistorial(
    CitaId CitaId,
    DateOnly Fecha,
    TimeOnly Hora,
    string? TipoDeVisita,
    OrigenEnLaAgenda Origen,
    EstadoEnLaAgenda Estado,
    CancelacionEnLaAgenda? Cancelacion);
