using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// <see cref="ICandadoDeLaAgendaDeAtencion"/> con un candado consultivo de transacción de PostgreSQL
/// (<c>pg_advisory_xact_lock</c>) sobre la conexión de la transacción abierta de <see cref="AgendaDbContext"/>, como
/// <see cref="CandadoDeFecha"/> pero con su propia clase de llave y una sola llave.
/// </summary>
public sealed class CandadoDeLaAgendaDeAtencion(AgendaDbContext db) : ICandadoDeLaAgendaDeAtencion
{
    /// <summary>Primera parte de la llave: "AGAT" en ASCII, el candado de la agenda de atención.</summary>
    internal const int ClaseDeCandado = 0x41474154;

    public async Task TomarAsync(CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "El candado de la agenda de atención exige la transacción abierta del handler: fuera de ella se soltaría en el acto.");
        }

        await db.Database.ExecuteSqlAsync($"select pg_advisory_xact_lock({ClaseDeCandado}, 0)", cancellationToken);
    }
}
