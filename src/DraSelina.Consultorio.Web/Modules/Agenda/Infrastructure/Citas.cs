using DraSelina.Consultorio.Agenda.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="ICitas"/> sobre <see cref="AgendaDbContext"/>.</summary>
public sealed class Citas(AgendaDbContext db) : ICitas
{
    public void Agregar(Cita cita) => db.Citas.Add(cita);

    public Task<Cita?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken cancellationToken) =>
        db.Citas.SingleOrDefaultAsync(c => c.SolicitudId == solicitudId, cancellationToken);

    public async Task<Cita?> ObtenerPorTokenHashParaCambiarAsync(byte[] tokenHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Leer la Cita para cambiarla exige la transacción abierta del handler: fuera de ella el bloqueo de su fila se soltaría en el acto.");
        }

        // Primero el bloqueo de la fila, por el índice único ux_cita_token_hash; espera si otro la tiene. Después la
        // lectura rastreada, que en READ COMMITTED ya ve lo que el otro confirmó. En dos sentencias porque FromSql no
        // sabe mapear las columnas de las propiedades complejas (Rango, Consentimiento).
        await db.Database.ExecuteSqlAsync(
            $"select 1 from agenda.cita where token_hash = {tokenHash} for update",
            cancellationToken);
        return await db.Citas.SingleOrDefaultAsync(c => c.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<GuardadoDeCita> GuardarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return GuardadoDeCita.Guardada;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
                                              {
                                                  SqlState: PostgresErrorCodes.ExclusionViolation,
                                                  ConstraintName: AgendaDbContext.RestriccionSinTraslape,
                                              })
        {
            // 23P01: el traslape vuelve como resultado, nunca como excepción (AD-8, T-22).
            return GuardadoDeCita.HorarioOcupado;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
                                              {
                                                  SqlState: PostgresErrorCodes.UniqueViolation,
                                                  ConstraintName: AgendaDbContext.IndiceDeSolicitud,
                                              })
        {
            // 23505: otro envío de la misma solicitud ya guardó su Cita (AD-27).
            return GuardadoDeCita.SolicitudRepetida;
        }
    }
}
