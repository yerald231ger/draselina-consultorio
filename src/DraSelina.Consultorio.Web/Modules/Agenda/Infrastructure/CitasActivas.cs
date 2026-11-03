using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ICitasActivas"/> sobre <see cref="AgendaDbContext"/>: proyecta o cuenta, sin rastrear.</summary>
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

    // Por el índice ix_cita_paciente_id_inicio. Cuenta por el inicio: una Cita es de la semana en que empieza.
    public async Task<int> AgendadasDeAsync(
        IReadOnlyCollection<PatientId> pacientes,
        DateTimeOffset desde,
        DateTimeOffset hasta,
        CancellationToken cancellationToken)
    {
        if (pacientes.Count == 0)
        {
            return 0;
        }

        var ids = pacientes.ToList();
        return await db.Citas
            .AsNoTracking()
            .CountAsync(
                c => ids.Contains(c.PacienteId)
                     && c.Estado == EstadoCita.Agendada
                     && c.Rango.Inicio >= desde
                     && c.Rango.Inicio < hasta,
                cancellationToken);
    }
}
