using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ICitasDelDia"/> sobre <see cref="AgendaDbContext"/>: proyecta, sin rastrear.</summary>
public sealed class CitasDelDia(AgendaDbContext db) : ICitasDelDia
{
    public async Task<IReadOnlyList<CitaDelDia>> AgendadasEntreAsync(
        DateTimeOffset desde, DateTimeOffset hasta, CancellationToken cancellationToken)
    {
        var filas = await db.Citas
            .AsNoTracking()
            .Where(c => c.Estado == EstadoCita.Agendada && c.Rango.Inicio >= desde && c.Rango.Inicio < hasta)
            .OrderBy(c => c.Rango.Inicio)
            .Select(c => new { c.Rango.Inicio, c.PacienteId, c.MotivoDeConsulta })
            .ToListAsync(cancellationToken);

        return [.. filas.Select(f => new CitaDelDia(HoraLocal.DesdeUtc(f.Inicio).Hora, f.PacienteId, f.MotivoDeConsulta))];
    }
}
