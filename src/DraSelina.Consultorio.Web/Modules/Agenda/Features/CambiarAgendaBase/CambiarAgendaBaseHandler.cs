using System.Globalization;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Cambia la semana tipo (CAP-10, FR-12). Orquesta y no decide (T-13): lee la agenda de atención y pasa por
/// <see cref="AgendaAtencion.CambiarBase"/> solo los días cuyos Tramos cambian, con la duración del Tipo de visita activo
/// más corto (INV-B5); si uno no vale, no guarda ninguno. Ya guardada, y en la misma transacción, deja una bitácora
/// <c>AgendaBase/Cambiada</c> por día cambiado, a nombre del Administrador con su id (RN-24, AD-16), con el día ISO y los
/// Tramos antes y después. No cancela ni mueve Citas (RN-6, RN-8), no toca las custom (RN-7) y no publica eventos (nadie
/// los escucha, T-23; Gerardo, 2026-11-17). No toma candado de fecha [ASSUMPTION: AD-26 solo lo pide para la custom]; sí el de toda la agenda de atención
/// (entrada 6.10), el mismo que guardar o borrar una custom.
/// </summary>
public sealed class CambiarAgendaBaseHandler(
    DuracionMinima minima,
    ITransaccionDeAgenda transaccion,
    ICandadoDeLaAgendaDeAtencion candadoDeLaAgenda,
    IAgendaAtencion agendaAtencion,
    IBitacoraDeAgenda bitacora,
    IActorActual actorActual,
    IClock reloj) : ICommandHandler<CambiarAgendaBase, AgendaBaseCambiada>
{
    public async Task<Resultado<AgendaBaseCambiada>> HandleAsync(CambiarAgendaBase comando, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // La validación de forma (AD-12) ya garantizó los siete días, cada uno una vez, con Tramos completos y al derecho.
        var ahora = reloj.UtcNow;
        var hoy = HoraLocal.DesdeUtc(ahora).Fecha;
        var duracionMinima = await minima.ObtenerAsync(cancellationToken);

        // El comando es solo del Administrador (AD-22): su id va a la bitácora (RN-24).
        var actor = await actorActual.ObtenerAsync();
        var actorId = await actorActual.IdDelAdministradorAsync();

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        // Un guardado de la agenda de atención a la vez, y se lee después: guardar sincroniza la base y las custom.
        await candadoDeLaAgenda.TomarAsync(cancellationToken);

        var agenda = await agendaAtencion.ObtenerAsync(hoy, cancellationToken);
        var cambiados = new List<(DayOfWeek Dia, IReadOnlyList<Tramo> Antes)>();

        for (var i = 0; i < comando.Dias.Count; i++)
        {
            var dia = comando.Dias[i];
            var nuevos = dia.Tramos.Select(t => Tramo.Crear(t.Inicio!.Value, t.Fin!.Value)).OrderBy(t => t.Inicio).ToList();
            var antes = agenda.TramosBase(dia.Dia);
            if (antes.SequenceEqual(nuevos))
            {
                continue;
            }

            if (agenda.CambiarBase(dia.Dia, nuevos, duracionMinima) is { } rechazo)
            {
                await tx.DeshacerAsync(cancellationToken);
                return Resultado.Invalido(CambiarAgendaBase.PropiedadDelDia(i), Mensaje(rechazo)).Como<AgendaBaseCambiada>();
            }

            cambiados.Add((dia.Dia, antes));
        }

        var fechasConCustom = agenda.FechasConCustom();
        if (cambiados.Count == 0)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Exito(new AgendaBaseCambiada([], fechasConCustom));
        }

        await agendaAtencion.GuardarAsync(agenda, cancellationToken);

        foreach (var (dia, antes) in cambiados)
        {
            await bitacora.AnotarAsync(
                new EntradaDeBitacora(
                    ahora,
                    actor,
                    actorId,
                    EntidadDeBitacora.AgendaBase,
                    DiaIso(dia).ToString(CultureInfo.InvariantCulture),
                    AccionDeBitacora.Cambiada,
                    new { antes = Detalle(antes), despues = Detalle(agenda.TramosBase(dia)) }),
                cancellationToken);
        }

        await tx.ConfirmarAsync(cancellationToken);

        return Resultado.Exito(new AgendaBaseCambiada([.. cambiados.Select(c => c.Dia)], fechasConCustom));
    }

    /// <summary>1 lunes … 7 domingo, como <c>agenda.agenda_base_tramo.dia_de_la_semana</c>.</summary>
    public static int DiaIso(DayOfWeek dia) => dia == DayOfWeek.Sunday ? 7 : (int)dia;

    private static object Detalle(IEnumerable<Tramo> tramos) =>
        tramos.Select(t => new
        {
            inicio = t.Inicio.ToString("HH:mm", CultureInfo.InvariantCulture),
            fin = t.Fin.ToString("HH:mm", CultureInfo.InvariantCulture),
        });

    private static string Mensaje(RechazoDeAgendaAtencion rechazo) => rechazo switch
    {
        RechazoDeAgendaAtencion.TramosEncimados => CambiarAgendaBase.MensajeDeTramosEncimados,
        RechazoDeAgendaAtencion.TramoMasCortoQueLaDuracionMinima => CambiarAgendaBase.MensajeDeTramoCorto,
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo sin mensaje al cambiar la base."),
    };
}
