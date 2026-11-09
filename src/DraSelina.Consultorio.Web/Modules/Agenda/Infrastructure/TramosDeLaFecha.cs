using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// <see cref="ITramosDeLaFecha"/> sobre <see cref="AgendaDbContext"/>, sin rastrear. Lee solo la base de ese día de la
/// semana y la custom de esa fecha, y deja que <see cref="AgendaAtencion"/> decida cuáles valen (INV-V1).
/// </summary>
public sealed class TramosDeLaFecha(AgendaDbContext db) : ITramosDeLaFecha
{
    public async Task<TramosVigentes> VigentesAsync(DateOnly fecha, CancellationToken cancellationToken)
    {
        var diaIso = FilaDeTramoBase.DiaIso(fecha.DayOfWeek);
        var tramosBase = await db.TramosBase
            .AsNoTracking()
            .Where(t => t.DiaDeLaSemana == diaIso)
            .ToListAsync(cancellationToken);
        var customs = await db.AgendasCustom
            .AsNoTracking()
            .Include(c => c.Tramos)
            .Where(c => c.Fecha == fecha)
            .ToListAsync(cancellationToken);

        return AgendaAtencion.Reconstituir(
                tramosBase.Select(t => (FilaDeTramoBase.Dia(t.DiaDeLaSemana), Tramo.Reconstituir(t.Inicio, t.Fin))),
                customs.Select(c => (c.Fecha, c.Motivo, c.Tramos.Select(t => Tramo.Reconstituir(t.Inicio, t.Fin)))),
                fecha)
            .TramosVigentes(fecha);
    }
}
