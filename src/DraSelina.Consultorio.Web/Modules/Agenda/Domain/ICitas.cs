namespace DraSelina.Consultorio.Agenda.Domain;

/// <summary>
/// El puerto de escritura de <see cref="Cita"/> (T-16). Guarda de forma explícita dentro de la transacción del
/// handler y traduce el choque de traslape a un valor; nunca deja escapar la excepción de la base (T-22, AD-8).
/// </summary>
public interface ICitas
{
    void Agregar(Cita cita);

    /// <summary>La Cita creada por esa solicitud en línea; nulo si no hay (AD-27).</summary>
    Task<Cita?> ObtenerPorSolicitudAsync(Guid solicitudId, CancellationToken cancellationToken);

    /// <summary>
    /// La Cita cuyo <c>token_hash</c> es <paramref name="tokenHash"/>, rastreada para cambiarla y con su fila bloqueada
    /// hasta el fin de la transacción (<c>FOR UPDATE</c>); nula si no hay. Otro cambio a la misma Cita espera aquí y,
    /// cuando la recibe, ya ve lo que confirmó el primero. Va siempre después del candado de fecha: fecha y luego fila,
    /// en ese orden, para que dos cambios no se bloqueen mutuamente.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si no hay una transacción abierta: fuera de ella el bloqueo se
    /// soltaría en el acto.</exception>
    Task<Cita?> ObtenerPorTokenHashParaCambiarAsync(byte[] tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda lo agregado. Si una Cita choca con otra no cancelada en la restricción de exclusión, devuelve
    /// <see cref="GuardadoDeCita.HorarioOcupado"/>; si otra ya lleva su solicitud, <see cref="GuardadoDeCita.SolicitudRepetida"/>.
    /// En los dos casos la transacción queda abortada y el handler la deshace.
    /// </summary>
    Task<GuardadoDeCita> GuardarAsync(CancellationToken cancellationToken);
}

/// <summary>Cómo terminó <see cref="ICitas.GuardarAsync"/>.</summary>
public enum GuardadoDeCita
{
    Guardada = 1,

    /// <summary>Otra Cita no cancelada ya ocupa parte del rango (RN-1).</summary>
    HorarioOcupado = 2,

    /// <summary>Otra Cita ya lleva el mismo <c>solicitud_id</c>: un envío repetido que llegó a la vez (AD-27).</summary>
    SolicitudRepetida = 3,
}
