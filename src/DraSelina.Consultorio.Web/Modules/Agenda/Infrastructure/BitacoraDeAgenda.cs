using System.Text.Json;
using DraSelina.Consultorio.Agenda.Features;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// La bitácora de Agenda sobre su contexto: la fila se inserta en la transacción abierta de
/// <see cref="AgendaDbContext"/>, la misma del cambio que registra (RN-24).
/// </summary>
public sealed class BitacoraDeAgenda(AgendaDbContext db) : IBitacoraDeAgenda
{
    public async Task AnotarAsync(EntradaDeBitacora entrada, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentException.ThrowIfNullOrWhiteSpace(entrada.EntidadId);

        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                $"Anotar '{entrada.Entidad} {entrada.Accion}' en la bitácora exige la transacción abierta del handler: "
                + "la entrada se guarda junto con el cambio.");
        }

        var id = Guid.CreateVersion7();
        var ocurrido = entrada.OcurridoEn.ToUniversalTime();
        var actor = entrada.Actor.ToString();
        var entidad = entrada.Entidad.ToString();
        var accion = entrada.Accion.ToString();
        var detalle = entrada.Detalle is null ? null : JsonSerializer.Serialize(entrada.Detalle, entrada.Detalle.GetType());

        // Una inserción directa, no SaveChanges: anotar no guarda de paso lo que el handler tenga sin guardar. Las
        // columnas son las de FilaDeBitacora; los CHECK de AgendaDbContext rechazan un valor fuera de su conjunto.
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into agenda.bitacora
                (id, ocurrido_en, actor_tipo, actor_id, entidad, entidad_id, accion, detalle)
            values
                ({id}, {ocurrido}, {actor}, {entrada.ActorId}, {entidad}, {entrada.EntidadId}, {accion}, {detalle}::jsonb)
            """,
            cancellationToken);
    }
}
