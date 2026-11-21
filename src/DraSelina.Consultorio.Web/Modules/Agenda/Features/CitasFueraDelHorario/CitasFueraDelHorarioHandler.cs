using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// La vista previa de las Citas que quedan fuera. Una consulta: no abre transacción ni toma candado; los comandos vuelven
/// a comparar con <see cref="CitasQueQuedanFuera"/> al guardar (RN-9). Al quitar, los Tramos son los de la base de ese
/// día (<see cref="AgendaAtencion.TramosBase"/>). Pide los nombres y el correo en lote a Pacientes (AD-10).
/// </summary>
public sealed class CitasFueraDelHorarioHandler(
    IAgendaDelRango agenda,
    CitasQueQuedanFuera citasQueQuedanFuera,
    IPacientesApi pacientes) : IQueryHandler<CitasFueraDelHorario, HorarioConCitasFuera>
{
    public async Task<Resultado<HorarioConCitasFuera>> HandleAsync(CitasFueraDelHorario consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        IReadOnlyList<Tramo> tramos;
        OrigenDeTramos origen;
        if (consulta.Quitar)
        {
            var atencion = await agenda.LeerAsync(consulta.Fecha, consulta.Fecha, cancellationToken);
            tramos = atencion.TramosBase(consulta.Fecha.DayOfWeek);
            origen = OrigenDeTramos.Base;
        }
        else
        {
            if (consulta.Tramos is null || consulta.Tramos.Any(t => t is null || t.Inicio >= t.Fin))
            {
                return Resultado.Invalido(nameof(CitasFueraDelHorario.Tramos), GuardarAgendaCustom.MensajeDeTramoAlReves)
                    .Como<HorarioConCitasFuera>();
            }

            tramos = [.. consulta.Tramos.Select(t => Tramo.Crear(t.Inicio, t.Fin)).OrderBy(t => t.Inicio)];
            origen = OrigenDeTramos.Custom;
        }

        var fuera = await citasQueQuedanFuera.DeAsync(new TramosVigentes(consulta.Fecha, tramos, origen), cancellationToken);
        var contactos = await pacientes.ObtenerContactosAsync([.. fuera.Select(c => c.PacienteId)], cancellationToken);

        return Resultado.Exito(new HorarioConCitasFuera(
            [.. tramos.Select(t => new TramoDeLaAgenda(t.Inicio, t.Fin))],
            [
                .. fuera.Select(c =>
                {
                    var contacto = contactos.GetValueOrDefault(c.PacienteId);
                    return new CitaFueraDelHorario(
                        c.Id,
                        HoraLocal.DesdeUtc(c.Inicio).Hora,
                        HoraLocal.DesdeUtc(c.Fin).Hora,
                        contacto?.Nombre,
                        contacto?.Correo is null,
                        c.Origen == OrigenDeCita.Consultorio);
                }),
            ]));
    }
}
