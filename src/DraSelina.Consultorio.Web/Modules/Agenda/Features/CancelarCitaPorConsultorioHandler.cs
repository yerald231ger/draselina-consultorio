using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Cancela una Cita a nombre del Consultorio (CAP-14, FR-17). Orquesta y no decide (T-13): abre la transacción y lee la
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
    ICitas citas,
    IBitacoraDeAgenda bitacora,
    IOutboxDeAgenda outbox,
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

        // Con su fila bloqueada: otro cambio a la misma Cita espera aquí a que este termine.
        var cita = await citas.ObtenerPorIdParaCambiarAsync(id, cancellationToken);
        if (cita is null)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(CodigosDeAgenda.CitaNoEncontrada).Como<CitaCanceladaPorConsultorio>();
        }

        // Ya cancelada por el Consultorio: un segundo envío. Lo guardado, sin otra bitácora ni otro evento (Gerardo,
        // 2026-11-06). Cancelada por el Paciente la rechaza la Cita.
        if (cita is { Estado: EstadoCita.Cancelada, MotivoDeCancelacion: MotivoDeCancelacion.Consultorio })
        {
            await tx.DeshacerAsync(cancellationToken);
            return Cancelada(cita, ahora);
        }

        var anterior = cita.Estado;
        var rechazo = cita.CancelarPorConsultorio(ahora);
        if (rechazo is not null)
        {
            await tx.DeshacerAsync(cancellationToken);
            return Resultado.Fallo(Codigo(rechazo.Value)).Como<CitaCanceladaPorConsultorio>();
        }

        // Cancelar no mueve el rango: la restricción de exclusión no puede rechazarlo. Si aun así no se guarda, es un
        // error, no un resultado.
        if (await citas.GuardarAsync(cancellationToken) is not GuardadoDeCita.Guardada)
        {
            throw new InvalidOperationException($"La Cita {cita.Id} no se pudo guardar al cancelarla.");
        }

        await DejarRastroAsync(cita, anterior, actor, actorId, ahora, cancellationToken);

        await tx.ConfirmarAsync(cancellationToken);

        return Cancelada(cita, ahora);
    }

    private static Resultado<CitaCanceladaPorConsultorio> Cancelada(Cita cita, DateTimeOffset ahora) =>
        Resultado.Exito(
            new CitaCanceladaPorConsultorio(
                cita.Id,
                EstadoMostrado.Estado(cita.Estado, cita.Rango.Inicio, ahora),
                EstadoMostrado.Cancelacion(cita.MotivoDeCancelacion)));

    // RN-24 y AD-3, dentro de la transacción y después de guardar: si el cambio no se confirma, tampoco quedan.
    private async Task DejarRastroAsync(
        Cita cita, EstadoCita anterior, Actor actor, string? actorId, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        await bitacora.AnotarAsync(
            new EntradaDeBitacora(
                ahora,
                actor,
                actorId,
                EntidadDeBitacora.Cita,
                cita.Id.ToString(),
                AccionDeBitacora.Cancelada,
                new
                {
                    estadoAnterior = anterior.ToString(),
                    estadoNuevo = cita.Estado.ToString(),
                    motivo = CitaCancelada.MotivoConsultorio,
                }),
            cancellationToken);

        var eventoId = Guid.CreateVersion7();
        await outbox.PublicarAsync(
            new CitaCancelada(eventoId, ahora, cita.Id, cita.PacienteId, actor, cita.Rango.Inicio, CitaCancelada.MotivoConsultorio),
            eventoId,
            claveDeOrden: cita.Id.Valor,
            ahora,
            cancellationToken);
    }

    private static string Codigo(RechazoAlCancelar rechazo) => rechazo switch
    {
        RechazoAlCancelar.CitaNoAgendada => CodigosDeAgenda.CitaNoAgendada,
        RechazoAlCancelar.YaEmpezo => CodigosDeAgenda.YaEmpezo,
        _ => throw new ArgumentOutOfRangeException(nameof(rechazo), rechazo, "Rechazo sin código."),
    };
}
