using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ICitasDelPaciente"/> sobre <see cref="AgendaDbContext"/>: proyecta, sin rastrear.</summary>
public sealed class CitasDelPaciente(AgendaDbContext db) : ICitasDelPaciente
{
    public async Task<(CitaLeidaDelPaciente? Proxima, CitaLeidaDelPaciente? Anterior)> ProximaYAnteriorAsync(
        PatientId paciente, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var delPaciente = db.Citas.AsNoTracking().Where(c => c.PacienteId == paciente);

        var proxima = await Leidas(delPaciente.Where(c => c.Estado == EstadoCita.Agendada && c.Rango.Inicio > ahora)
                .OrderBy(c => c.Rango.Inicio))
            .FirstOrDefaultAsync(cancellationToken);

        // RN-31: la que empieza justo ahora ya empezó.
        var anterior = await Leidas(delPaciente.Where(c => c.Rango.Inicio <= ahora)
                .OrderByDescending(c => c.Rango.Inicio))
            .FirstOrDefaultAsync(cancellationToken);

        return (proxima, anterior);
    }

    private IQueryable<CitaLeidaDelPaciente> Leidas(IQueryable<Cita> citas) =>
        citas.Select(c => new CitaLeidaDelPaciente(
            c.Rango.Inicio,
            db.TiposDeVisita.Where(t => t.Id == c.TipoDeVisitaId).Select(t => t.Nombre).FirstOrDefault(),
            c.Estado,
            c.MotivoDeCancelacion));
}
