using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El único camino para cancelar una Cita a nombre del Consultorio (CAP-14, FR-17, AD-6): lo usan el comando
/// <see cref="CancelarCitaPorConsultorio"/> y la política P-1 (<see cref="CancelarCitasElegidas"/>). Corre dentro de la
/// transacción que abrió quien lo llama: lee la Cita con su fila bloqueada, la cancela con
/// <see cref="Cita.CancelarPorConsultorio"/> (que decide RN-31) y, ya guardada, deja su bitácora con el actor y su id
/// (RN-24, AD-22) y publica <see cref="CitaCancelada"/> con motivo consultorio por el outbox (AD-3, T-30); la disculpa la
/// manda Notificaciones. No confirma ni deshace: eso es de quien lo llama, según el resultado. Ni motivo de consulta, ni
/// nota, ni contacto van a la bitácora ni al evento (AD-23).
/// </summary>
public sealed class CancelacionPorConsultorio(ICitas citas, IBitacoraDeAgenda bitacora, IOutboxDeAgenda outbox)
{
    /// <summary>Cancela la Cita <paramref name="id"/> a nombre de <paramref name="actor"/>.</summary>
    public async Task<CancelacionHecha> CancelarAsync(
        CitaId id, Actor actor, string? actorId, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        // Con su fila bloqueada: otro cambio a la misma Cita espera aquí a que este termine.
        var cita = await citas.ObtenerPorIdParaCambiarAsync(id, cancellationToken);
        if (cita is null)
        {
            return new CancelacionHecha(null, ResultadoDeCancelacion.NoEncontrada, null);
        }

        // Ya cancelada por el Consultorio: un segundo envío o una segunda entrega. Nada más que hacer.
        if (cita is { Estado: EstadoCita.Cancelada, MotivoDeCancelacion: MotivoDeCancelacion.Consultorio })
        {
            return new CancelacionHecha(cita, ResultadoDeCancelacion.YaCancelada, null);
        }

        var anterior = cita.Estado;
        var rechazo = cita.CancelarPorConsultorio(ahora);
        if (rechazo is not null)
        {
            return new CancelacionHecha(cita, ResultadoDeCancelacion.Rechazada, rechazo);
        }

        // Cancelar no mueve el rango: la restricción de exclusión no puede rechazarlo. Si aun así no se guarda, es un
        // error, no un resultado.
        if (await citas.GuardarAsync(cancellationToken) is not GuardadoDeCita.Guardada)
        {
            throw new InvalidOperationException($"La Cita {cita.Id} no se pudo guardar al cancelarla.");
        }

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
                    inicio = cita.Rango.Inicio,
                    fin = cita.Rango.Fin,
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

        return new CancelacionHecha(cita, ResultadoDeCancelacion.Cancelada, null);
    }
}

/// <summary>Qué pasó al intentar cancelar.</summary>
/// <param name="Cita">La Cita leída; nula si no existe.</param>
/// <param name="Resultado">Qué pasó.</param>
/// <param name="Rechazo">Por qué no, si la Cita lo rechazó.</param>
public sealed record CancelacionHecha(Cita? Cita, ResultadoDeCancelacion Resultado, RechazoAlCancelar? Rechazo);

/// <summary>El resultado de <see cref="CancelacionPorConsultorio.CancelarAsync"/>.</summary>
public enum ResultadoDeCancelacion
{
    /// <summary>Se canceló, con bitácora y <see cref="CitaCancelada"/>: quien llama confirma.</summary>
    Cancelada = 1,

    /// <summary>Ya estaba cancelada por el Consultorio: nada nuevo.</summary>
    YaCancelada = 2,

    /// <summary>La Cita lo rechazó (no está Agendada o ya empezó).</summary>
    Rechazada = 3,

    /// <summary>No hay Cita con ese id.</summary>
    NoEncontrada = 4,
}
