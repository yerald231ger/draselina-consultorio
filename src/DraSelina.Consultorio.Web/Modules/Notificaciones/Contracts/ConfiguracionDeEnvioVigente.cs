using DraSelina.Consultorio.BuildingBlocks.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Contracts;

/// <summary>
/// Las horas de envío y los destinatarios internos vigentes, de <c>configuracion_de_envio</c> (CAP-16, AD-14): lo que la
/// pantalla de Configuración pinta. Solo el Administrador (AD-22). Solo lee.
/// </summary>
public sealed record ConfiguracionDeEnvioVigente : IQuery<ConfiguracionDeEnvioValores>, IRequiereAdministrador;

/// <summary>
/// Los cuatro valores de <c>configuracion_de_envio</c>. Las direcciones nunca van a registros (AD-23):
/// <see cref="ToString"/> no las muestra.
/// </summary>
/// <param name="HoraDelRecordatorio">Hora local en que sale el Recordatorio, el día anterior a la Cita (FR-22).</param>
/// <param name="HoraDeLaAgendaDelDia">Hora local en que sale "Tus citas de hoy" (FR-25).</param>
/// <param name="CorreoDeLaDoctora">A quién va "Tus citas de hoy".</param>
/// <param name="CorreoDelAdministrador">A quién va la alerta de cancelación tardía (FR-24).</param>
public sealed record ConfiguracionDeEnvioValores(
    TimeOnly HoraDelRecordatorio,
    TimeOnly HoraDeLaAgendaDelDia,
    string CorreoDeLaDoctora,
    string CorreoDelAdministrador)
{
    public override string ToString() =>
        $"ConfiguracionDeEnvioValores(HoraDelRecordatorio = {HoraDelRecordatorio}, HoraDeLaAgendaDelDia = {HoraDeLaAgendaDelDia}, "
        + "CorreoDeLaDoctora = oculto, CorreoDelAdministrador = oculto)";
}
