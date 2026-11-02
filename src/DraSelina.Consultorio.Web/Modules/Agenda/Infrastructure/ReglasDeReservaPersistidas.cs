using DraSelina.Consultorio.Agenda.Domain;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary><see cref="IReglasDeReserva"/> sobre <see cref="AgendaDbContext"/>: la única fila de <c>agenda.reglas_de_reserva</c>.</summary>
public sealed class ReglasDeReservaPersistidas(AgendaDbContext db) : IReglasDeReserva
{
    public Task<ReglasDeReserva> ObtenerAsync(CancellationToken cancellationToken) =>
        db.ReglasDeReserva.SingleAsync(cancellationToken);

    public async Task GuardarAsync(ReglasDeReserva reglas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reglas);
        if (db.Entry(reglas).State == EntityState.Detached)
        {
            throw new InvalidOperationException("Las reglas de reserva que se guardan son las obtenidas con este mismo puerto.");
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
