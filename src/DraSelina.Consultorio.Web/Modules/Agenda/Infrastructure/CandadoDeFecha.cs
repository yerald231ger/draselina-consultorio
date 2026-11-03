using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// <see cref="ICandadoDeFecha"/> con un candado consultivo de transacción de PostgreSQL (<c>pg_advisory_xact_lock</c>)
/// sobre la conexión de la transacción abierta de <see cref="AgendaDbContext"/>. La llave tiene dos partes: una fija
/// que distingue los candados de fecha de Agenda de cualquier otro, y el número de día de la fecha local (la de
/// <c>(inicio AT TIME ZONE 'America/Mexico_City')::date</c> para una Cita).
/// </summary>
public sealed class CandadoDeFecha(AgendaDbContext db) : ICandadoDeFecha
{
    /// <summary>Primera parte de la llave: "AGFE" en ASCII, los candados de fecha de Agenda.</summary>
    internal const int ClaseDeCandado = 0x41474645;

    public async Task TomarAsync(DateOnly fecha, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "El candado de la fecha exige la transacción abierta del handler: fuera de ella se soltaría en el acto.");
        }

        var dia = fecha.DayNumber;
        await db.Database.ExecuteSqlAsync($"select pg_advisory_xact_lock({ClaseDeCandado}, {dia})", cancellationToken);
    }
}
