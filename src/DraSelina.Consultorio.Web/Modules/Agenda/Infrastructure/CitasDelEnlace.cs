using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ICitasDelEnlace"/> sobre <see cref="AgendaDbContext"/>: proyecta, sin rastrear.</summary>
public sealed class CitasDelEnlace(AgendaDbContext db) : ICitasDelEnlace
{
    // Por el índice único ux_cita_token_hash.
    public Task<CitaLeidaPorEnlace?> PorTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken) =>
        db.Citas.AsNoTracking()
            .Where(c => c.TokenHash == tokenHash)
            .Join(db.TiposDeVisita.AsNoTracking(), c => c.TipoDeVisitaId, t => t.Id,
                (c, t) => new CitaLeidaPorEnlace(
                    c.Id, c.Rango.Inicio, c.Rango.Fin, c.Estado, c.MotivoDeCancelacion, c.PacienteId, c.TipoDeVisitaId, t.Nombre))
            .SingleOrDefaultAsync(cancellationToken);
}
