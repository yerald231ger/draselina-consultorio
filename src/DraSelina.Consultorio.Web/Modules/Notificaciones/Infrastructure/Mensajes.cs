using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary><see cref="IMensajes"/> sobre <see cref="NotificacionesDbContext"/>.</summary>
public sealed class Mensajes(NotificacionesDbContext db) : IMensajes
{
    // Por el índice único (evento_id, tipo).
    public Task<bool> ExisteAsync(Guid eventoId, TipoDeMensaje tipo, CancellationToken cancellationToken)
    {
        var texto = tipo.ToString();
        return db.Mensajes.AsNoTracking().AnyAsync(m => m.EventoId == eventoId && m.Tipo == texto, cancellationToken);
    }

    public async Task RegistrarAsync(MensajeRegistrado mensaje, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Registrar un mensaje exige la transacción abierta del handler.");
        }

        db.Mensajes.Add(new FilaDeMensaje
        {
            Id = mensaje.Id,
            EventoId = mensaje.EventoId,
            CitaId = mensaje.CitaId?.Valor,
            Tipo = mensaje.Tipo.ToString(),
            Canal = mensaje.Canal,
            Estado = mensaje.Estado.ToString(),
            RegistradoEn = mensaje.RegistradoEn,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
