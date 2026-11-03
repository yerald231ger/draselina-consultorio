using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ITiposDeVisitaReservables"/> sobre <see cref="AgendaDbContext"/>: proyecta, sin rastrear.</summary>
public sealed class TiposDeVisitaReservablesPersistidos(AgendaDbContext db) : ITiposDeVisitaReservables
{
    public async Task<IReadOnlyList<TipoDeVisitaReservable>> ListarAsync(CancellationToken cancellationToken) =>
        await db.TiposDeVisita
            .AsNoTracking()
            .Where(t => t.Activo && t.ReservableEnLinea)
            .OrderBy(t => t.Orden)
            .ThenBy(t => t.Nombre)
            .Select(t => new TipoDeVisitaReservable(t.Id, t.Nombre, t.Descripcion, t.DuracionMin))
            .ToListAsync(cancellationToken);
}
