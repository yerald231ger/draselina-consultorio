using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ICitasDeLaFecha"/> sobre <see cref="AgendaDbContext"/>: proyecta, sin rastrear.</summary>
public sealed class CitasDeLaFechaPersistidas(AgendaDbContext db) : ICitasDeLaFecha
{
    public async Task<IReadOnlyList<CitaLeidaDeLaFecha>> EntreAsync(
        DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken) =>
        await db.Citas
            .AsNoTracking()
            .Where(c => c.Rango.Inicio >= desde && c.Rango.Inicio < hasta)
            .OrderBy(c => c.Rango.Inicio)
            .Select(c => new CitaLeidaDeLaFecha(
                c.Id, c.PacienteId, c.Rango.Inicio, c.Rango.Fin, c.Estado, c.MotivoDeCancelacion, c.MotivoDeConsulta))
            .ToListAsync(cancellationToken);
}
