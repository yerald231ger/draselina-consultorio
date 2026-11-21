using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Los días de un mes del Calendario de atención. Una consulta: no abre transacción ni cambia estado. Lee una vez la
/// agenda de atención del mes (<see cref="IAgendaDelRango"/>); qué Tramos valen cada día lo decide
/// <see cref="Domain.AgendaAtencion.TramosVigentes"/> (INV-V1) y cómo se distingue, <see cref="ClasificacionDelDia"/>.
/// </summary>
public sealed class MesDeAtencionHandler(IAgendaDelRango agenda) : IQueryHandler<MesDeAtencion, IReadOnlyList<DiaDeAtencion>>
{
    public async Task<Resultado<IReadOnlyList<DiaDeAtencion>>> HandleAsync(MesDeAtencion consulta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        if (consulta.Anio is < 2000 or > 9999)
        {
            return Resultado.Invalido(nameof(MesDeAtencion.Anio), "El año va de 2000 a 9999.").Como<IReadOnlyList<DiaDeAtencion>>();
        }

        if (consulta.Mes is < 1 or > 12)
        {
            return Resultado.Invalido(nameof(MesDeAtencion.Mes), "El mes va de 1 a 12.").Como<IReadOnlyList<DiaDeAtencion>>();
        }

        var primero = new DateOnly(consulta.Anio, consulta.Mes, 1);
        var ultimo = primero.AddMonths(1).AddDays(-1);
        var atencion = await agenda.LeerAsync(primero, ultimo, cancellationToken);
        var customs = atencion.Customs.ToDictionary(c => c.Fecha);

        IReadOnlyList<DiaDeAtencion> dias =
        [
            .. Enumerable.Range(0, ultimo.Day).Select(i =>
            {
                var fecha = primero.AddDays(i);
                var vigentes = atencion.TramosVigentes(fecha);
                return new DiaDeAtencion(
                    fecha,
                    ClasificacionDelDia.De(atencion, fecha),
                    customs.GetValueOrDefault(fecha)?.Motivo,
                    [.. vigentes.Tramos.Select(t => new TramoDeLaAgenda(t.Inicio, t.Fin))]);
            }),
        ];

        return Resultado.Exito(dias);
    }
}
