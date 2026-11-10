using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// <see cref="IAgendaDelRango"/> sobre <see cref="AgendaDbContext"/>, sin rastrear: una lectura de la base y una de las
/// agendas custom del rango, no una por día. Arma <see cref="AgendaAtencion"/> como <see cref="TramosDeLaFecha"/>.
/// </summary>
public sealed class AgendaDelRango(AgendaDbContext db) : IAgendaDelRango
{
    public async Task<AgendaAtencion> LeerAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
    {
        var tramosBase = await db.TramosBase
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var customs = await db.AgendasCustom
            .AsNoTracking()
            .Include(c => c.Tramos)
            .Where(c => c.Fecha >= desde && c.Fecha <= hasta)
            .ToListAsync(cancellationToken);

        return AgendaAtencion.Reconstituir(
            tramosBase.Select(t => (FilaDeTramoBase.Dia(t.DiaDeLaSemana), Tramo.Reconstituir(t.Inicio, t.Fin))),
            customs.Select(c => (c.Fecha, c.Motivo, c.Tramos.Select(t => Tramo.Reconstituir(t.Inicio, t.Fin)))),
            desde);
    }
}
