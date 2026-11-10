using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// <see cref="ICitaDelDetalle"/> sobre <see cref="AgendaDbContext"/>, sin rastrear: en el circuito de administración el
/// contexto dura lo que el circuito, y una lectura rastreada devolvería la Cita que un comando anterior dejó en caché,
/// no la que está guardada.
/// </summary>
public sealed class CitaDelDetalle(AgendaDbContext db) : ICitaDelDetalle
{
    public async Task<CitaLeidaDelDetalle?> LeerAsync(CitaId id, CancellationToken cancellationToken)
    {
        var cita = await db.Citas.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (cita is null)
        {
            return null;
        }

        var tipo = await db.TiposDeVisita.AsNoTracking()
            .Where(t => t.Id == cita.TipoDeVisitaId)
            .Select(t => t.Nombre)
            .FirstOrDefaultAsync(cancellationToken);

        return new CitaLeidaDelDetalle(cita, tipo);
    }
}
