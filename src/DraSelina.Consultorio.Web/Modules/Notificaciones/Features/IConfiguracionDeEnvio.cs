using DraSelina.Consultorio.Notificaciones.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>Lo que Notificaciones lee y escribe de <c>configuracion_de_envio</c>: horas y destinatarios internos (AD-14).</summary>
public interface IConfiguracionDeEnvio
{
    /// <summary>A quién va la alerta de cancelación tardía (FR-24). Sin rastrear.</summary>
    Task<string> CorreoDelAdministradorAsync(CancellationToken cancellationToken);

    /// <summary>A quién va "Tus citas de hoy" (FR-25). Sin rastrear.</summary>
    Task<string> CorreoDeLaDoctoraAsync(CancellationToken cancellationToken);

    /// <summary>La hora local en que sale "Tus citas de hoy" (FR-25). Sin rastrear.</summary>
    Task<TimeOnly> HoraDeLaAgendaDelDiaAsync(CancellationToken cancellationToken);

    /// <summary>Los cuatro valores de la única fila. Sin rastrear.</summary>
    Task<ConfiguracionDeEnvioValores> LeerAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Deja la única fila con <paramref name="valores"/>, en la transacción abierta del llamador, y devuelve la hora del
    /// Recordatorio que tenía antes. Aparta la fila (<c>FOR UPDATE</c>): otro cambio espera a que este termine.
    /// </summary>
    Task<TimeOnly> CambiarAsync(ConfiguracionDeEnvioValores valores, CancellationToken cancellationToken);
}
