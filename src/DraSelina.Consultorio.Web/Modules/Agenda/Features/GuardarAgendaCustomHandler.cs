using System.Globalization;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Registra o cambia la Agenda custom de una fecha (CAP-11, FR-13). Orquesta y no decide (T-13): abre la transacción,
/// toma el candado de la fecha (AD-26, RN-20) y solo después lee la agenda de atención y las Citas; la custom la
/// registra o la cambia <see cref="AgendaAtencion"/>, que decide sus invariantes (INV-A1 a A4, A6) con la duración del
/// Tipo de visita activo más corto. Si la lista que vio el Administrador no es la de las Citas que quedan fuera con los
/// Tramos nuevos (<see cref="CitasQueQuedanFuera"/>, RN-9), no guarda nada. Ya guardada, y en la misma transacción, deja
/// la bitácora <c>AgendaCustom</c> a nombre del Administrador, con su id y el antes y el después (RN-24, AD-16, AD-22),
/// y, si eligió cancelar alguna, el evento interno con esas Citas y el actor (AD-6, T-30); las cancela después la
/// política P-1 (<see cref="CancelarCitasElegidas"/>). No toca ninguna Cita (RN-8, RN-10). Los mismos Tramos y motivo
/// sin ninguna Cita elegida para cancelar son éxito sin guardar ni anotar nada; con alguna, se anota y se publica igual.
/// </summary>
public sealed class GuardarAgendaCustomHandler(
    DuracionMinima minima,
    ITransaccionDeAgenda transaccion,
    ICandadoDeFecha candado,
    IAgendaAtencion agendaAtencion,
    CitasQueQuedanFuera citasQueQuedanFuera,
    IBitacoraDeAgenda bitacora,
    IOutboxDeAgenda outbox,
    IActorActual actorActual,
    IClock reloj) : ICommandHandler<GuardarAgendaCustom, AgendaCustomGuardada>
{
    public async Task<Resultado<AgendaCustomGuardada>> HandleAsync(GuardarAgendaCustom comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // La validación de forma (AD-12) ya garantizó fecha, motivo y Tramos completos con inicio antes del fin.
        var fecha = comando.Fecha!.Value;
        var motivo = comando.Motivo!.Trim();
        var tramos = comando.Tramos.Select(t => Tramo.Crear(t.Inicio!.Value, t.Fin!.Value)).ToList();
        var ahora = reloj.UtcNow;
        var hoy = HoraLocal.DesdeUtc(ahora).Fecha;

        if (fecha < hoy)
        {
            return Resultado.Fallo(CodigosDeAgenda.FechaPasada).Como<AgendaCustomGuardada>();
        }

        var duracionMinima = await minima.ObtenerAsync(cancellationToken);

        // El comando es solo del Administrador (AD-22): su id va a la bitácora (RN-24).
        var actor = await actorActual.ObtenerAsync();
        var actorId = await actorActual.IdDelAdministradorAsync();

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // AD-26: la fila sobre la fecha va antes de leer; una Cita que se reserva en el mismo instante espera aquí.
        await candado.TomarAsync(fecha, cancellationToken);

        var agenda = await agendaAtencion.ObtenerAsync(hoy, cancellationToken);
        var antes = agenda.Customs.FirstOrDefault(c => c.Fecha == fecha);

        // Los mismos Tramos y motivo: la custom no cambia, pero el Administrador aún puede elegir cancelar una Cita que
        // quedó fuera y se conservó antes (RN-9). Solo sin ninguna elegida es un guardado sin cambios.
        var mismos = antes is not null && antes.Motivo == motivo && MismosTramos(antes.Tramos, tramos);
        if (!mismos)
        {
            var rechazo = antes is null
                ? agenda.RegistrarCustom(fecha, tramos, motivo, duracionMinima)
                : agenda.CambiarCustom(fecha, tramos, motivo, duracionMinima);
            if (rechazo is not null)
            {
                await tx.DeshacerAsync(cancellationToken);
                return Resultado.Fallo(Codigo(rechazo.Value)).Como<AgendaCustomGuardada>();
            }
        }

        var fuera = await citasQueQuedanFuera.DeAsync(agenda.TramosVigentes(fecha), cancellationToken);
        if (!CitasQueQuedanFuera.Coincide(fuera, comando.Citas))
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.CitasFuera).Como<AgendaCustomGuardada>();
        }

        var aCancelar = CitasQueQuedanFuera.ElegidasParaCancelar(comando.Citas);
        if (mismos && aCancelar.Count == 0)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Exito(new AgendaCustomGuardada(fecha, Registrada: false, SinCambios: true));
        }

        if (!mismos)
        {
            await agendaAtencion.GuardarAsync(agenda, cancellationToken);
        }

        var despues = agenda.Customs.First(c => c.Fecha == fecha);
        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                ahora,
                actor,
                actorId,
                EntidadDeBitacora.AgendaCustom,
                fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                antes is null ? AccionDeBitacora.Registrada : AccionDeBitacora.Cambiada,
                new
                {
                    antes = antes is null ? null : Detalle(antes),
                    despues = Detalle(despues),
                    citasACancelar = aCancelar.Select(c => c.Valor),
                }),
            cancellationToken);

        // Sin Citas elegidas no hay nada que P-1 deba hacer, y nadie más escucha el evento (T-23).
        if (aCancelar.Count > 0)
        {
            var eventoId = Guid.CreateVersion7();
            IEventoDeAgendaCustom evento = antes is null
                ? new AgendaCustomFueRegistrada(eventoId, ahora, fecha, aCancelar, actor, actorId)
                : new AgendaCustomFueCambiada(eventoId, ahora, fecha, aCancelar, actor, actorId);
            await PublicarAsync(outbox, evento, cancellationToken);
        }

        await tx.ConfirmarAsync(cancellationToken);

        return Resultado.Exito(new AgendaCustomGuardada(fecha, Registrada: antes is null, SinCambios: false));
    }

    /// <summary>Guarda el evento interno en el outbox con su tipo concreto: el nombre del tipo es su columna <c>tipo</c>.</summary>
    internal static Task PublicarAsync(IOutboxDeAgenda outbox, IEventoDeAgendaCustom evento, CancellationToken cancellationToken) =>
        evento switch
        {
            AgendaCustomFueRegistrada e => outbox.PublicarAsync(e, e.EventoId, e.EventoId, e.OcurridoEn, cancellationToken),
            AgendaCustomFueCambiada e => outbox.PublicarAsync(e, e.EventoId, e.EventoId, e.OcurridoEn, cancellationToken),
            AgendaCustomFueBorrada e => outbox.PublicarAsync(e, e.EventoId, e.EventoId, e.OcurridoEn, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(evento), evento.GetType().Name, "Evento de Agenda custom desconocido."),
        };

    private static bool MismosTramos(IReadOnlyList<Tramo> guardados, IEnumerable<Tramo> nuevos) =>
        guardados.SequenceEqual(nuevos.OrderBy(t => t.Inicio));

    internal static object Detalle(AgendaCustom custom) => new
    {
        motivo = custom.Motivo,
        tramos = custom.Tramos.Select(t => new
        {
            inicio = t.Inicio.ToString("HH:mm", CultureInfo.InvariantCulture),
            fin = t.Fin.ToString("HH:mm", CultureInfo.InvariantCulture),
        }),
    };

    private static string Codigo(RechazoDeAgendaAtencion rechazo) => rechazo switch
    {
        RechazoDeAgendaAtencion.TramosEncimados => CodigosDeAgenda.TramosEncimados,
        RechazoDeAgendaAtencion.TramoMasCortoQueLaDuracionMinima => CodigosDeAgenda.TramoCorto,
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo sin código al guardar una Agenda custom."),
    };
}
