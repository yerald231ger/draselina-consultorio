using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// Puerto de lectura de la bitácora de una Cita (RN-24, CAP-17; entrada 6): las filas de <c>agenda.bitacora</c> con
/// entidad <c>Cita</c> y su id, sin rastrear y sin transacción, en orden de ocurrencia. Del detalle guardado solo saca lo
/// que la bitácora muestra; nunca hay ahí motivo de consulta, nota ni contacto (AD-23).
/// </summary>
public interface IBitacoraDeLaCita
{
    /// <summary>Todas las entradas de la Cita, en orden de ocurrencia; vacía si no tiene.</summary>
    Task<IReadOnlyList<EntradaLeidaDeLaBitacora>> DeLaCitaAsync(CitaId cita, CancellationToken cancellationToken);
}

/// <summary>Una entrada como la lee <see cref="IBitacoraDeLaCita"/>.</summary>
/// <param name="OcurridoEn">En UTC.</param>
/// <param name="Actor">Quién.</param>
/// <param name="Accion">Qué.</param>
/// <param name="Origen">El <c>origen</c> de su detalle, en una Agendada; nulo si no lo trae.</param>
/// <param name="Motivo">El <c>motivo</c> de su detalle, en una Cancelada; nulo si no lo trae.</param>
/// <param name="InicioAnterior">El <c>inicioAnterior</c> de su detalle, en UTC, en una Reprogramada; nulo si no lo trae.</param>
/// <param name="InicioNuevo">El <c>inicioNuevo</c> de su detalle, en UTC, en una Reprogramada; nulo si no lo trae.</param>
public sealed record EntradaLeidaDeLaBitacora(
    DateTimeOffset OcurridoEn,
    Actor Actor,
    AccionDeBitacora Accion,
    OrigenDeCita? Origen,
    MotivoDeCancelacion? Motivo,
    DateTimeOffset? InicioAnterior,
    DateTimeOffset? InicioNuevo);
