using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ICitasDeLaAgenda"/> sobre <see cref="AgendaDbContext"/>: proyecta, sin rastrear.</summary>
public sealed class CitasDeLaAgenda(AgendaDbContext db) : ICitasDeLaAgenda
{
    public async Task<IReadOnlyList<CitaLeidaDeLaAgenda>> EntreAsync(
        DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken) =>
        await db.Citas
            .AsNoTracking()
            .Where(c => c.Rango.Inicio >= desde && c.Rango.Inicio < hasta)
            .OrderBy(c => c.Rango.Inicio)
            .Select(c => new CitaLeidaDeLaAgenda(
                c.Id, c.PacienteId, c.Rango.Inicio, c.Rango.Fin, c.Estado, c.MotivoDeCancelacion, c.Origen, c.Nota,
                c.MotivoDeConsulta))
            .ToListAsync(cancellationToken);
}
