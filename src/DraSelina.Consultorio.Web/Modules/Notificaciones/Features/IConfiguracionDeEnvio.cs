namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>Lo que Notificaciones lee de <c>configuracion_de_envio</c> para sus destinatarios internos (AD-14).</summary>
public interface IConfiguracionDeEnvio
{
    /// <summary>A quién va la alerta de cancelación tardía (FR-24). Sin rastrear.</summary>
    Task<string> CorreoDelAdministradorAsync(CancellationToken cancellationToken);

    /// <summary>A quién va "Tus citas de hoy" (FR-25). Sin rastrear.</summary>
    Task<string> CorreoDeLaDoctoraAsync(CancellationToken cancellationToken);

    /// <summary>La hora local en que sale "Tus citas de hoy" (FR-25). Sin rastrear.</summary>
    Task<TimeOnly> HoraDeLaAgendaDelDiaAsync(CancellationToken cancellationToken);
}
