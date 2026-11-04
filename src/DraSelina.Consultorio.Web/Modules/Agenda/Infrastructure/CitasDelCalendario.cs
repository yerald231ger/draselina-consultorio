using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ICitasDelCalendario"/> sobre <see cref="AgendaDbContext"/>: proyecta, sin rastrear.</summary>
public sealed class CitasDelCalendario(AgendaDbContext db) : ICitasDelCalendario
{
    // Por el índice único ux_cita_token_hash.
    public Task<CitaDelCalendario?> PorTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        Proyectar(db.Citas.AsNoTracking().Where(c => c.TokenHash == tokenHash), cancellationToken);

    public Task<CitaDelCalendario?> PorIdAsync(CitaId citaId, CancellationToken cancellationToken) =>
        Proyectar(db.Citas.AsNoTracking().Where(c => c.Id == citaId), cancellationToken);

    private Task<CitaDelCalendario?> Proyectar(IQueryable<Cita> citas, CancellationToken cancellationToken) =>
        citas
            .Join(db.TiposDeVisita.AsNoTracking(), c => c.TipoDeVisitaId, t => t.Id,
                (c, t) => new CitaDelCalendario(c.Id, c.Rango.Inicio, c.Rango.Fin, c.Estado, t.Nombre))
            .SingleOrDefaultAsync(cancellationToken);
}
