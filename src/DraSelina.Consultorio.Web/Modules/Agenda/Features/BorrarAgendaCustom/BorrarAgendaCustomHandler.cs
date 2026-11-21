using System.Globalization;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Borra la Agenda custom de una fecha: el día vuelve a la base (RN-12). Igual que guardarla (RN-9, AD-6): abre la
/// transacción, toma el candado de la fecha (AD-26) y solo después lee la agenda y las Citas; la borra
/// <see cref="AgendaAtencion.BorrarCustom"/>; si la lista que vio el Administrador no es la de las Citas que quedan
/// fuera de los Tramos de la base ese día (<see cref="CitasQueQuedanFuera"/>), no borra nada. Ya borrada, y en la misma
/// transacción, deja la bitácora <c>AgendaCustom/Borrada</c> con el antes, a nombre del Administrador con su id (RN-24),
/// y, si eligió cancelar alguna, <see cref="AgendaCustomFueBorrada"/> para la política P-1 (T-30). No toca ninguna Cita.
/// </summary>
public sealed class BorrarAgendaCustomHandler(
    ITransaccionDeAgenda transaccion,
    ICandadoDeFecha candado,
    ICandadoDeLaAgendaDeAtencion candadoDeLaAgenda,
    IAgendaAtencion agendaAtencion,
    CitasQueQuedanFuera citasQueQuedanFuera,
    IBitacoraDeAgenda bitacora,
    IOutboxDeAgenda outbox,
    IActorActual actorActual,
    IClock reloj) : ICommandHandler<BorrarAgendaCustom, AgendaCustomBorrada>
{
    public async Task<Resultado<AgendaCustomBorrada>> HandleAsync(BorrarAgendaCustom comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // La validación de forma (AD-12) ya garantizó la fecha y una lista sin Citas repetidas.
        var fecha = comando.Fecha!.Value;
        var ahora = reloj.UtcNow;
        var hoy = HoraLocal.DesdeUtc(ahora).Fecha;

        if (fecha < hoy)
        {
            return Resultado.Fallo(CodigosDeAgenda.FechaPasada).Como<AgendaCustomBorrada>();
        }

        // El comando es solo del Administrador (AD-22): su id va a la bitácora (RN-24).
        var actor = await actorActual.ObtenerAsync();
        var actorId = await actorActual.IdDelAdministradorAsync();

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // AD-26: la fila sobre la fecha va antes de leer.
        await candado.TomarAsync(fecha, cancellationToken);

        // Después, el de toda la agenda de atención (entrada 6.10).
        await candadoDeLaAgenda.TomarAsync(cancellationToken);

        var agenda = await agendaAtencion.ObtenerAsync(hoy, cancellationToken);
        var antes = agenda.Customs.FirstOrDefault(c => c.Fecha == fecha);
        if (antes is null || agenda.BorrarCustom(fecha) is not null)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.FechaSinCustom).Como<AgendaCustomBorrada>();
        }

        // Ya sin custom, los Tramos vigentes son los de la base de ese día.
        var fuera = await citasQueQuedanFuera.DeAsync(agenda.TramosVigentes(fecha), cancellationToken);
        if (!CitasQueQuedanFuera.Coincide(fuera, comando.Citas))
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.CitasFuera).Como<AgendaCustomBorrada>();
        }

        await agendaAtencion.GuardarAsync(agenda, cancellationToken);

        var aCancelar = CitasQueQuedanFuera.ElegidasParaCancelar(comando.Citas);
        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                ahora,
                actor,
                actorId,
                EntidadDeBitacora.AgendaCustom,
                fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                AccionDeBitacora.Borrada,
                new
                {
                    antes = RastroDeAgendaCustom.Detalle(antes),
                    despues = (object?)null,
                    citasACancelar = aCancelar.Select(c => c.Valor),
                }),
            cancellationToken);

        // Sin Citas elegidas no hay nada que P-1 deba hacer, y nadie más escucha el evento (T-23).
        if (aCancelar.Count > 0)
        {
            var eventoId = Guid.CreateVersion7();
            await RastroDeAgendaCustom.PublicarAsync(
                outbox, new AgendaCustomFueBorrada(eventoId, ahora, fecha, aCancelar, actor, actorId), cancellationToken);
        }

        await tx.ConfirmarAsync(cancellationToken);

        return Resultado.Exito(new AgendaCustomBorrada(fecha));
    }
}
