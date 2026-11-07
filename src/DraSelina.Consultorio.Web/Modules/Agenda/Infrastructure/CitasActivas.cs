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
        CitaId? aparte,
        CancellationToken cancellationToken)
    {
        // Un periodo vacío no traslapa nada; y tstzrange rechaza un límite inferior mayor que el superior.
        if (hasta <= desde)
        {
            return [];
        }

        // El mismo traslape de rangos semiabiertos [inicio, fin) y el mismo predicado que la restricción de exclusión
        // (AD-8): así la consulta va por su índice GiST y recorre la ventana, no el historial. Una Cita que termina
        // justo en "desde" no ocupa nada. 'Cancelada' va escrito, no como parámetro, para que PostgreSQL vea que el
        // filtro es el del índice parcial (TextoDeEnumeraciones). La Cita aparte se quita con un filtro más, sobre las
        // filas que ya trajo el índice; Guid.Empty no es de ninguna Cita.
        var excluida = aparte?.Valor ?? Guid.Empty;
        var filas = await db.Database
            .SqlQuery<FilaDeRango>($"""
                select inicio as "Inicio", fin as "Fin" from agenda.cita
                where estado <> 'Cancelada'
                  and tstzrange(inicio, fin, '[)') && tstzrange({desde}, {hasta}, '[)')
                  and id <> {excluida}
                """)
            .ToListAsync(cancellationToken);

        return [.. filas.Select(f => RangoDeTiempo.Reconstituir(f.Inicio, f.Fin))];
    }

    private sealed class FilaDeRango
    {
        public DateTimeOffset Inicio { get; init; }

        public DateTimeOffset Fin { get; init; }
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
