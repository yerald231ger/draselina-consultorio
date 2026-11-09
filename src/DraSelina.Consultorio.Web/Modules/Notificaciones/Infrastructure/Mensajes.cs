using DraSelina.Consultorio.Kernel;
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

    public Task<bool> CerradoDesdeAsync(CitaId citaId, TipoDeMensaje tipo, DateTimeOffset desde, CancellationToken cancellationToken)
    {
        var texto = tipo.ToString();
        var enviado = EstadoDeMensaje.Enviado.ToString();
        var omitido = EstadoDeMensaje.Omitido.ToString();
        var cita = citaId.Valor;
        var desdeUtc = desde.ToUniversalTime();
        return db.Mensajes.AsNoTracking().AnyAsync(
            m => m.CitaId == cita && m.Tipo == texto && m.RegistradoEn >= desdeUtc
                && (m.Estado == enviado || (m.Estado == omitido && m.EventoId == null)),
            cancellationToken);
    }

    // Por el índice único parcial (tipo, fecha).
    public Task<bool> ExisteDeLaFechaAsync(TipoDeMensaje tipo, DateOnly fecha, CancellationToken cancellationToken)
    {
        var texto = tipo.ToString();
        return db.Mensajes.AsNoTracking().AnyAsync(m => m.Tipo == texto && m.Fecha == fecha, cancellationToken);
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
            Fecha = mensaje.Fecha,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
