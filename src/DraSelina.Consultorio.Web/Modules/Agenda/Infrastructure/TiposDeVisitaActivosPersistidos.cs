using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ITiposDeVisitaActivos"/> sobre <see cref="AgendaDbContext"/>: proyecta, sin rastrear.</summary>
public sealed class TiposDeVisitaActivosPersistidos(AgendaDbContext db) : ITiposDeVisitaActivos
{
    public async Task<IReadOnlyList<TipoDeVisitaActivo>> ListarAsync(CancellationToken cancellationToken) =>
        await db.TiposDeVisita
            .AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden)
            .ThenBy(t => t.Nombre)
            .Select(t => new TipoDeVisitaActivo(t.Id, t.Nombre, t.DuracionMin, t.ReservableEnLinea))
            .ToListAsync(cancellationToken);
}
