using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ITiposDeVisitaEditables"/> sobre <see cref="AgendaDbContext"/>: proyecta, sin rastrear.</summary>
public sealed class TiposDeVisitaEditables(AgendaDbContext db) : ITiposDeVisitaEditables
{
    public async Task<IReadOnlyList<TipoDeVisitaEditable>> ListarAsync(CancellationToken cancellationToken) =>
        await db.TiposDeVisita
            .AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden)
            .ThenBy(t => t.Nombre)
            .Select(t => new TipoDeVisitaEditable(t.Id, t.Nombre, t.Descripcion, t.DuracionMin, t.ReservableEnLinea, t.Orden))
            .ToListAsync(cancellationToken);
}
