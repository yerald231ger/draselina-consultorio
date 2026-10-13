using System.Text.Json;
using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// El outbox de Agenda sobre su contexto: la fila se inserta en la transacción abierta de
/// <see cref="AgendaDbContext"/>, la misma del cambio que origina el evento.
/// </summary>
public sealed class OutboxDeAgenda(AgendaDbContext db) : IOutboxDeAgenda
{
    public async Task PublicarAsync<TEvento>(
        TEvento evento,
        Guid eventoId,
        Guid claveDeOrden,
        DateTimeOffset ocurridoEn,
        CancellationToken cancellationToken)
        where TEvento : notnull
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                $"Publicar '{typeof(TEvento).Name}' exige la transacción abierta del handler: el evento se guarda junto con el cambio.");
        }

        // El tipo real del evento, no el de la variable de quien llama: con un evento guardado como object o como
        // una interfaz, el nombre y el contenido serían los de ese tipo y ningún handler lo recibiría.
        var tipo = evento.GetType().Name;
        var contenido = JsonSerializer.Serialize(evento, evento.GetType());
        var ocurrido = ocurridoEn.ToUniversalTime();

        // Una inserción directa, no SaveChanges: publicar no guarda de paso lo que el handler tenga sin guardar.
        // Las columnas son las de FilaDeOutbox.
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into agenda.outbox
                (id, tipo, contenido, ocurrido_en, clave_de_orden, estado, intentos, proximo_intento_en, entregado_en)
            values
                ({eventoId}, {tipo}, {contenido}::jsonb, {ocurrido}, {claveDeOrden}, {FilaDeOutbox.Pendiente}, 0, {ocurrido}, null)
            """,
            cancellationToken);
    }
}
