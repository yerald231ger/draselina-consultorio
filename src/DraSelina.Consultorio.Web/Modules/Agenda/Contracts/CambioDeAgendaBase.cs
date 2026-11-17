using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// La vista previa de un cambio de la semana tipo (CAP-10, RN-6, RN-7; entrada 6.4): las Citas Agendadas que todavía no
/// empiezan y quedarían fuera de la base nueva, solo en los días que cambian y en fechas sin Agenda custom, y las fechas
/// con custom que no cambian. Nada se cancela por ellas. Solo el Administrador (AD-22). Solo lee.
/// </summary>
/// <param name="Dias">Los días de la semana con sus Tramos nuevos; los que no vienen no cambian.</param>
public sealed record CambioDeAgendaBase(IReadOnlyList<DiaDeLaBase> Dias) : IQuery<CambioDeBaseRevisado>, IRequiereAdministrador;

/// <summary>Un día de la semana tipo con sus Tramos, en hora local.</summary>
public sealed record DiaDeLaBase(DayOfWeek Dia, IReadOnlyList<TramoDeLaAgenda> Tramos);

/// <summary>Lo que afecta el cambio de la semana tipo.</summary>
/// <param name="Citas">Las Citas que quedarían fuera, en orden de fecha y hora.</param>
/// <param name="FechasConCustom">Las fechas con Agenda custom de hoy en adelante, en orden.</param>
public sealed record CambioDeBaseRevisado(IReadOnlyList<CitaFueraDeLaBase> Citas, IReadOnlyList<DateOnly> FechasConCustom);

/// <summary>Una Cita que quedaría fuera de la base nueva. El nombre no va a registros (AD-23).</summary>
/// <param name="Fecha">La fecha local.</param>
/// <param name="Inicio">Hora local de inicio.</param>
/// <param name="Fin">Hora local de fin.</param>
/// <param name="Paciente">El nombre; nulo si Pacientes no lo conoce.</param>
/// <param name="SinCorreo">El Paciente no tiene correo (también si Pacientes no lo conoce).</param>
/// <param name="DelConsultorio">La registró el Administrador; si no, es en línea.</param>
public sealed record CitaFueraDeLaBase(
    CitaId CitaId, DateOnly Fecha, TimeOnly Inicio, TimeOnly Fin, string? Paciente, bool SinCorreo, bool DelConsultorio)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append(
            $"CitaId = {CitaId}, Fecha = {Fecha:yyyy-MM-dd}, Inicio = {Inicio:HH:mm}, Fin = {Fin:HH:mm}, Paciente = oculto, "
            + $"SinCorreo = {SinCorreo}, DelConsultorio = {DelConsultorio}");
        return true;
    }
}
