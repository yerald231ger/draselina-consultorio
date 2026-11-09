using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary><see cref="IRecordatorios"/> sobre <see cref="NotificacionesDbContext"/>.</summary>
public sealed class Recordatorios(NotificacionesDbContext db) : IRecordatorios
{
    public async Task<TimeOnly> HoraDelRecordatorioAsync(CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            return await db.ConfiguracionDeEnvio.AsNoTracking().Select(c => c.HoraRecordatorio).SingleAsync(cancellationToken);
        }

        // En una transacción, con FOR SHARE: un cambio de la hora (CambiarConfiguracionDeEnvio) espera a que esta
        // termine, y esta espera a que el cambio termine. Así ninguna fila queda programada con la hora anterior.
        return (await db.Database
            .SqlQuery<TimeOnly>(
                $"""select hora_recordatorio as "Value" from "notificaciones"."configuracion_de_envio" where id = 1 for share""")
            .ToListAsync(cancellationToken))
            .Single();
    }

    public Task ProgramarAsync(RecordatorioProgramado recordatorio, CancellationToken cancellationToken)
    {
        ExigirTransaccion();
        var (cita, paciente, inicio, para) = Valores(recordatorio);
        return db.Database.ExecuteSqlAsync(
            $"""
            insert into "notificaciones"."recordatorio_programado" (cita_id, paciente_id, inicio, programado_para)
            values ({cita}, {paciente}, {inicio}, {para})
            on conflict (cita_id) do nothing
            """,
            cancellationToken);
    }

    public Task MoverAsync(RecordatorioProgramado recordatorio, CancellationToken cancellationToken)
    {
        ExigirTransaccion();
        var (cita, paciente, inicio, para) = Valores(recordatorio);
        return db.Database.ExecuteSqlAsync(
            $"""
            insert into "notificaciones"."recordatorio_programado" (cita_id, paciente_id, inicio, programado_para)
            values ({cita}, {paciente}, {inicio}, {para})
            on conflict (cita_id) do update
            set paciente_id = excluded.paciente_id, inicio = excluded.inicio, programado_para = excluded.programado_para
            """,
            cancellationToken);
    }

    public Task BorrarAsync(CitaId citaId, CancellationToken cancellationToken)
    {
        ExigirTransaccion();
        return db.Database.ExecuteSqlAsync(
            $"""delete from "notificaciones"."recordatorio_programado" where cita_id = {citaId.Valor}""",
            cancellationToken);
    }

    public async Task<IReadOnlyList<RecordatorioProgramado>> ApartarTodosAsync(CancellationToken cancellationToken)
    {
        ExigirTransaccion();

        // Sin componer nada sobre la consulta: EF la manda tal cual, con su FOR UPDATE.
        var filas = await db.RecordatoriosProgramados
            .FromSql(
                $"""
                select * from "notificaciones"."recordatorio_programado"
                order by programado_para, cita_id
                for update
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. filas.Select(f => new RecordatorioProgramado(new CitaId(f.CitaId), new PatientId(f.PacienteId), f.Inicio, f.ProgramadoPara))];
    }

    public async Task<RecordatorioProgramado?> ApartarElSiguienteVencidoAsync(
        DateTimeOffset ahora, IReadOnlyCollection<CitaId> excepto, CancellationToken cancellationToken)
    {
        ExigirTransaccion();
        var hasta = ahora.ToUniversalTime();
        var fuera = excepto.Select(c => c.Valor).ToArray();

        // Sin componer nada sobre la consulta: EF la manda tal cual, con su FOR UPDATE.
        var filas = await db.RecordatoriosProgramados
            .FromSql(
                $"""
                select * from "notificaciones"."recordatorio_programado"
                where programado_para <= {hasta} and not (cita_id = any({fuera}))
                order by programado_para, cita_id
                limit 1
                for update skip locked
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return filas is [var fila]
            ? new RecordatorioProgramado(new CitaId(fila.CitaId), new PatientId(fila.PacienteId), fila.Inicio, fila.ProgramadoPara)
            : null;
    }

    private static (Guid Cita, Guid Paciente, DateTimeOffset Inicio, DateTimeOffset ProgramadoPara) Valores(RecordatorioProgramado r) =>
        (r.CitaId.Valor, r.PacienteId.Valor, r.Inicio.ToUniversalTime(), r.ProgramadoPara.ToUniversalTime());

    private void ExigirTransaccion()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Los Recordatorios se leen y se escriben en la transacción abierta del llamador.");
        }
    }
}
