using System.ComponentModel.DataAnnotations;
using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Contracts;

/// <summary>
/// El Administrador cambia las horas de envío y los destinatarios internos (CAP-16, AD-14) sin desplegar: el siguiente
/// envío ya usa los valores nuevos. Al cambiar la hora del Recordatorio, los Recordatorios pendientes se mueven a la
/// hora nueva, y el que quede con su hora ya pasada se omite (Gerardo, 2026-11-09). Solo el Administrador (AD-22).
/// Devuelve los valores guardados. Las direcciones nunca van a registros (AD-23): <see cref="ToString"/> no las muestra.
/// </summary>
public sealed class CambiarConfiguracionDeEnvio : ICommand<ConfiguracionDeEnvioValores>, IRequiereAdministrador
{
    public const string MensajeDeCorreo = "Revisa el correo: debe ser como nombre@correo.com.";

    /// <summary>Hora local del Recordatorio, el día anterior a la Cita.</summary>
    [Required(ErrorMessage = "Escribe la hora del Recordatorio.")]
    public TimeOnly? HoraDelRecordatorio { get; set; }

    /// <summary>Hora local de "Tus citas de hoy".</summary>
    [Required(ErrorMessage = "Escribe la hora de la Agenda del día.")]
    public TimeOnly? HoraDeLaAgendaDelDia { get; set; }

    /// <summary>A quién va "Tus citas de hoy". Vacío o con solo espacios cuenta como no escrito.</summary>
    [Required(ErrorMessage = "Escribe el correo de la doctora.")]
    [EmailAddress(ErrorMessage = MensajeDeCorreo)]
    [StringLength(254, ErrorMessage = "El correo tiene a lo más 254 caracteres.")]
    public string? CorreoDeLaDoctora
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>A quién va la alerta de cancelación tardía. Vacío o con solo espacios cuenta como no escrito.</summary>
    [Required(ErrorMessage = "Escribe el correo del Administrador.")]
    [EmailAddress(ErrorMessage = MensajeDeCorreo)]
    [StringLength(254, ErrorMessage = "El correo tiene a lo más 254 caracteres.")]
    public string? CorreoDelAdministrador
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public override string ToString() =>
        $"CambiarConfiguracionDeEnvio(HoraDelRecordatorio = {HoraDelRecordatorio}, HoraDeLaAgendaDelDia = {HoraDeLaAgendaDelDia}, "
        + "CorreoDeLaDoctora = oculto, CorreoDelAdministrador = oculto)";
}
