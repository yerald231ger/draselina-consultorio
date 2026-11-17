using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Contracts;

/// <summary>
/// La vista previa del Calendario de atención (CAP-11, RN-9; entrada 6.3): qué Citas quedarían fuera si una fecha toma
/// los Tramos <paramref name="Tramos"/> (al guardar su horario propio) o, con <paramref name="Quitar"/>, los de la Agenda
/// base de ese día (al quitarlo). Con la misma regla que los comandos (AD-6). Solo el Administrador (AD-22). Solo lee:
/// los comandos vuelven a comparar al guardar.
/// </summary>
/// <param name="Fecha">La fecha local.</param>
/// <param name="Tramos">Los Tramos nuevos, en hora local; se ignoran con <paramref name="Quitar"/>.</param>
/// <param name="Quitar">Verdadero: se compara con la Agenda base de ese día de la semana.</param>
public sealed record CitasFueraDelHorario(DateOnly Fecha, IReadOnlyList<TramoDeLaAgenda> Tramos, bool Quitar)
    : IQuery<HorarioConCitasFuera>, IRequiereAdministrador
{
    /// <summary>Al guardar el horario propio con esos Tramos.</summary>
    public static CitasFueraDelHorario AlGuardar(DateOnly fecha, IReadOnlyList<TramoDeLaAgenda> tramos) => new(fecha, tramos, Quitar: false);

    /// <summary>Al quitar el horario propio: el día vuelve a la base.</summary>
    public static CitasFueraDelHorario AlQuitar(DateOnly fecha) => new(fecha, [], Quitar: true);
}

/// <summary>Los Tramos con que se comparó y las Citas que quedan fuera.</summary>
/// <param name="Tramos">Los Tramos nuevos, en orden; al quitar, los de la base de ese día (vacío: no se atiende).</param>
/// <param name="Citas">Las Citas Agendadas que todavía no empiezan y no caben, en orden de hora.</param>
public sealed record HorarioConCitasFuera(IReadOnlyList<TramoDeLaAgenda> Tramos, IReadOnlyList<CitaFueraDelHorario> Citas);

/// <summary>Una Cita que quedaría fuera. El nombre no va a registros (AD-23): <see cref="ToString"/> no lo muestra.</summary>
/// <param name="CitaId">La Cita.</param>
/// <param name="Inicio">Hora local de inicio.</param>
/// <param name="Fin">Hora local de fin.</param>
/// <param name="Paciente">El nombre del Paciente; nulo si Pacientes no lo conoce.</param>
/// <param name="SinCorreo">El Paciente no tiene correo: hay que avisarle a mano (también si Pacientes no lo conoce).</param>
/// <param name="DelConsultorio">La registró el Administrador (origen consultorio); si no, es en línea.</param>
public sealed record CitaFueraDelHorario(CitaId CitaId, TimeOnly Inicio, TimeOnly Fin, string? Paciente, bool SinCorreo, bool DelConsultorio)
{
    private bool PrintMembers(System.Text.StringBuilder texto)
    {
        texto.Append(
            $"CitaId = {CitaId}, Inicio = {Inicio:HH:mm}, Fin = {Fin:HH:mm}, Paciente = oculto, SinCorreo = {SinCorreo}, "
            + $"DelConsultorio = {DelConsultorio}");
        return true;
    }
}
