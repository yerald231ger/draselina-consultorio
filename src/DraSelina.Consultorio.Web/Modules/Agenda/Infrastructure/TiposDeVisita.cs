using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ITiposDeVisita"/> sobre <see cref="AgendaDbContext"/>.</summary>
public sealed class TiposDeVisita(AgendaDbContext db) : ITiposDeVisita
{
    public Task<TipoDeVisita?> ObtenerAsync(TipoDeVisitaId id, CancellationToken cancellationToken) =>
        db.TiposDeVisita.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
}
