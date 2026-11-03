using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ICitasActivas"/> sobre <see cref="AgendaDbContext"/>: proyecta solo el rango, sin rastrear.</summary>
public sealed class CitasActivas(AgendaDbContext db) : ICitasActivas
{
    public async Task<IReadOnlyList<RangoDeTiempo>> EntreAsync(
        DateTimeOffset desde,
        DateTimeOffset hasta,
        CancellationToken cancellationToken)
    {
        // Semiabierto, como la restricción de exclusión (AD-8): una Cita que termina justo en "desde" no ocupa nada.
        var filas = await db.Citas
            .AsNoTracking()
            .Where(c => c.Estado != EstadoCita.Cancelada && c.Rango.Inicio < hasta && c.Rango.Fin > desde)
            .Select(c => new { c.Rango.Inicio, c.Rango.Fin })
            .ToListAsync(cancellationToken);

        return [.. filas.Select(f => RangoDeTiempo.Reconstituir(f.Inicio, f.Fin))];
    }
}
