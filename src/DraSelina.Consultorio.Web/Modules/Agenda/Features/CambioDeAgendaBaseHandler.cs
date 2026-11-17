using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// La vista previa de un cambio de la semana tipo. Una consulta: no abre transacción. Compara cada día con la base
/// guardada y, para los que cambian, pide a <see cref="CitasQueQuedanFuera.DeLaBaseAsync"/> las Citas que quedarían
/// fuera; con los nombres y el correo en lote de Pacientes (AD-10). Las fechas con custom son las de hoy en adelante.
/// </summary>
public sealed class CambioDeAgendaBaseHandler(
    IAgendaDelRango agenda,
    CitasQueQuedanFuera citasQueQuedanFuera,
    IPacientesApi pacientes,
    IClock reloj) : IQueryHandler<CambioDeAgendaBase, CambioDeBaseRevisado>
{
    public async Task<Resultado<CambioDeBaseRevisado>> HandleAsync(CambioDeAgendaBase consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (consulta.Dias is null
            || consulta.Dias.Any(d => d is null || !Enum.IsDefined(d.Dia) || d.Tramos is null || d.Tramos.Any(t => t is null || t.Inicio >= t.Fin))
            || consulta.Dias.Select(d => d.Dia).Distinct().Count() != consulta.Dias.Count)
        {
            return Resultado.Invalido(nameof(CambioDeAgendaBase.Dias), CambiarAgendaBase.MensajeDeSemana).Como<CambioDeBaseRevisado>();
        }

        var hoy = HoraLocal.DesdeUtc(reloj.UtcNow).Fecha;
        var atencion = await agenda.LeerAsync(hoy, DateOnly.MaxValue, cancellationToken);
        var fechasConCustom = atencion.FechasConCustom();

        var cambiados = new Dictionary<DayOfWeek, IReadOnlyList<Tramo>>();
        foreach (var dia in consulta.Dias)
        {
            IReadOnlyList<Tramo> nuevos = [.. dia.Tramos.Select(t => Tramo.Crear(t.Inicio, t.Fin)).OrderBy(t => t.Inicio)];
            if (!atencion.TramosBase(dia.Dia).SequenceEqual(nuevos))
            {
                cambiados[dia.Dia] = nuevos;
            }
        }

        var fuera = await citasQueQuedanFuera.DeLaBaseAsync(cambiados, fechasConCustom, cancellationToken);
        var contactos = await pacientes.ObtenerContactosAsync([.. fuera.Select(c => c.PacienteId)], cancellationToken);

        return Resultado.Exito(new CambioDeBaseRevisado(
            [
                .. fuera.Select(c =>
                {
                    var (fecha, inicio) = HoraLocal.DesdeUtc(c.Inicio);
                    var contacto = contactos.GetValueOrDefault(c.PacienteId);
                    return new CitaFueraDeLaBase(
                        c.Id, fecha, inicio, HoraLocal.DesdeUtc(c.Fin).Hora, contacto?.Nombre, contacto?.Correo is null,
                        c.Origen == OrigenDeCita.Consultorio);
                }),
            ],
            fechasConCustom));
    }
}
