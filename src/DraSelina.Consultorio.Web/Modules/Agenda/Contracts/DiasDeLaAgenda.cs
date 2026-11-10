using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// Los días de la Agenda del Administrador (CAP-12, FR-14; entrada 5.2), de <paramref name="Desde"/> a
/// <paramref name="Hasta"/>, ambas incluidas, en fechas locales del consultorio (AD-17): a lo más
/// <see cref="MaximoDeDias"/>. Cada día trae su tipo (con el motivo de su Agenda custom), sus Tramos vigentes y sus Citas
/// en cualquier estado, en orden de inicio, ya marcadas. Solo el Administrador (AD-22). Solo lee. Lo que se ve de cada
/// Cita lo decide Agenda: Atendida se deriva (RN-19, RN-31) y "fuera de horario" sale de los Tramos vigentes (INV-C6).
/// </summary>
/// <param name="Desde">El primer día, local.</param>
/// <param name="Hasta">El último día, local; igual a <paramref name="Desde"/> para un solo día.</param>
public sealed record DiasDeLaAgenda(DateOnly Desde, DateOnly Hasta) : IQuery<IReadOnlyList<DiaDeLaAgenda>>, IRequiereAdministrador
{
    /// <summary>Una semana: lo más que se pide de una vez.</summary>
    public const int MaximoDeDias = 7;

    /// <summary>Un solo día.</summary>
    public static DiasDeLaAgenda DelDia(DateOnly fecha) => new(fecha, fecha);
}

/// <summary>Un día de la Agenda. El motivo de la Agenda custom nunca va a registros (AD-23).</summary>
/// <param name="Fecha">La fecha local.</param>
/// <param name="Tipo">Cómo se distingue el día.</param>
/// <param name="MotivoDeCustom">El motivo de su Agenda custom (INV-A4); nulo si no tiene.</param>
/// <param name="Tramos">Los Tramos vigentes ese día (INV-V1), en orden; vacío si no se atiende.</param>
/// <param name="Citas">Las Citas que empiezan ese día, en cualquier estado, en orden de inicio.</param>
public sealed record DiaDeLaAgenda(
    DateOnly Fecha,
    TipoDeDia Tipo,
    string? MotivoDeCustom,
    IReadOnlyList<TramoDeLaAgenda> Tramos,
    IReadOnlyList<CitaDeLaAgenda> Citas)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append(
            $"Fecha = {Fecha:yyyy-MM-dd}, Tipo = {Tipo}, MotivoDeCustom = {(MotivoDeCustom is null ? "ninguno" : "oculto")}, "
            + $"Tramos = {Tramos.Count}, Citas = {Citas.Count}");
        return true;
    }
}

/// <summary>
/// Cómo se distingue un día en la Agenda (EXPERIENCE.md, "Fecha con Agenda custom"). Las tres de Agenda custom llevan
/// su motivo.
/// </summary>
public enum TipoDeDia
{
    /// <summary>La Agenda base de ese día de la semana, con Tramos.</summary>
    Normal = 1,

    /// <summary>La Agenda base de ese día de la semana no tiene Tramos (INV-B3): no se atiende.</summary>
    SinAtencion = 2,

    /// <summary>Una Agenda custom sin Tramos (INV-A3): "No se atiende · motivo".</summary>
    CustomSinAtencion = 3,

    /// <summary>Una Agenda custom con Tramos en un día cuya base no tiene: "Día abierto · motivo".</summary>
    DiaAbierto = 4,

    /// <summary>Cualquier otra Agenda custom, que recorta o alarga el día: "Horario especial · motivo".</summary>
    HorarioEspecial = 5,
}

/// <summary>Un Tramo vigente, en hora local, <c>[Inicio, Fin)</c>.</summary>
public sealed record TramoDeLaAgenda(TimeOnly Inicio, TimeOnly Fin);

/// <summary>
/// Una Cita de la Agenda del Administrador. Nombre, motivo y nota nunca van a registros (AD-23): <see cref="ToString"/>
/// no los muestra.
/// </summary>
/// <param name="CitaId">La Cita.</param>
/// <param name="Inicio">Hora local de inicio.</param>
/// <param name="Fin">Hora local de fin.</param>
/// <param name="Paciente">El nombre del Paciente tal como se guardó (RN-29); nulo si Pacientes no lo conoce.</param>
/// <param name="MotivoDeConsulta">Nulo si la Cita (del Consultorio) no lo tiene.</param>
/// <param name="Estado">Cómo se ve en la Agenda.</param>
/// <param name="Cancelacion">Por qué se canceló; solo en <see cref="EstadoEnLaAgenda.Cancelada"/>.</param>
/// <param name="SinCorreo">
/// El Paciente no tiene correo guardado (RN-30): hay que avisarle a mano. También si Pacientes no lo conoce
/// `[ASSUMPTION]`.
/// </param>
/// <param name="FueraDeHorario">
/// Cita del Consultorio que no cabe completa en ningún Tramo vigente de su fecha (INV-C6). Una en línea nunca.
/// </param>
/// <param name="Nota">La nota del Administrador; nula si no tiene.</param>
public sealed record CitaDeLaAgenda(
    CitaId CitaId,
    TimeOnly Inicio,
    TimeOnly Fin,
    string? Paciente,
    string? MotivoDeConsulta,
    EstadoEnLaAgenda Estado,
    CancelacionEnLaAgenda? Cancelacion,
    bool SinCorreo,
    bool FueraDeHorario,
    string? Nota)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append(
            $"CitaId = {CitaId}, Inicio = {Inicio:HH:mm}, Fin = {Fin:HH:mm}, Paciente = oculto, MotivoDeConsulta = oculto, "
            + $"Estado = {Estado}, Cancelacion = {Cancelacion}, SinCorreo = {SinCorreo}, FueraDeHorario = {FueraDeHorario}, "
            + "Nota = oculta");
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
