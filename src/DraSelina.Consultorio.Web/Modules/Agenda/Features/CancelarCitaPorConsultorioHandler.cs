using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Cancela una Cita a nombre del Consultorio (CAP-14, FR-17). Orquesta y no decide (T-13): abre la transacción y, con
/// <see cref="CancelacionPorConsultorio"/> (el mismo camino que la política P-1), lee la
/// Cita por su id con la fila bloqueada, así otro cambio a la misma Cita espera al primero y parte de lo que dejó. No
/// toma candado de fecha: cancelar no crea ni mueve rangos (AD-26). La cancela <see cref="Cita.CancelarPorConsultorio"/>,
/// que decide si su inicio ya llegó (RN-31). Ya guardada, y en la misma transacción, deja su bitácora a nombre del
/// Administrador, con su id (RN-24, AD-22), y publica <see cref="CitaCancelada"/> con motivo consultorio por el outbox
/// (AD-3, T-30): la disculpa al Paciente y el borrado del Recordatorio los hace Notificaciones al recibirlo. Una Cita ya
/// cancelada por el Consultorio es éxito con lo guardado, sin otra bitácora ni otro evento (Gerardo, 2026-11-06). Ni
/// motivo de consulta, ni nota, ni contacto van a la bitácora ni al evento (AD-23).
/// </summary>
public sealed class CancelarCitaPorConsultorioHandler(
    ITransaccionDeAgenda transaccion,
    CancelacionPorConsultorio cancelacion,
    IActorActual actorActual,
    IClock reloj) : ICommandHandler<CancelarCitaPorConsultorio, CitaCanceladaPorConsultorio>
{
    public async Task<Resultado<CitaCanceladaPorConsultorio>> HandleAsync(
        CancelarCitaPorConsultorio comando,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);

        // La validación de forma (AD-12) ya garantizó que no es nulo.
        var id = comando.CitaId!.Value;
        var ahora = reloj.UtcNow;

        // El comando es solo del Administrador (AD-22): su id va a la bitácora (RN-24).
        var actor = await actorActual.ObtenerAsync();
        var actorId = await actorActual.IdDelAdministradorAsync();

        await using var tx = await transaccion.AbrirAsync(cancellationToken);

        var hecha = await cancelacion.CancelarAsync(id, actor, actorId, ahora, cancellationToken);
        switch (hecha.Resultado)
        {
            case ResultadoDeCancelacion.Cancelada:
                await tx.ConfirmarAsync(cancellationToken);
                return Cancelada(hecha.Cita!, ahora);

            // Un segundo envío: lo guardado, sin otra bitácora ni otro evento (Gerardo, 2026-11-06).
            case ResultadoDeCancelacion.YaCancelada:
                await tx.DeshacerAsync(cancellationToken);
                return Cancelada(hecha.Cita!, ahora);

            case ResultadoDeCancelacion.Rechazada:
                await tx.DeshacerAsync(cancellationToken);
                return Resultado.Fallo(Codigo(hecha.Rechazo!.Value)).Como<CitaCanceladaPorConsultorio>();

            default:
                await tx.DeshacerAsync(cancellationToken);
                return Resultado.Fallo(CodigosDeAgenda.CitaNoEncontrada).Como<CitaCanceladaPorConsultorio>();
        }
    }

    private static Resultado<CitaCanceladaPorConsultorio> Cancelada(Cita cita, DateTimeOffset ahora) =>
        Resultado.Exito(
            new CitaCanceladaPorConsultorio(
                cita.Id,
                EstadoMostrado.Estado(cita.Estado, cita.Rango.Inicio, ahora),
                EstadoMostrado.Cancelacion(cita.MotivoDeCancelacion)));

    private static string Codigo(RechazoAlCancelar rechazo) => rechazo switch
    {
        RechazoAlCancelar.CitaNoAgendada => CodigosDeAgenda.CitaNoAgendada,
        RechazoAlCancelar.YaEmpezo => CodigosDeAgenda.YaEmpezo,
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo sin código."),
    };
}
