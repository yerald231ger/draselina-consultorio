namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>
/// La única fila de <c>notificaciones.configuracion_de_envio</c> (AD-14): las horas del Recordatorio y de la Agenda
/// del día, y los correos de la doctora y del administrador. Esta entrada solo la crea y la siembra; la leen el
/// Recordatorio, la alerta de cancelación tardía y la Agenda del día (entradas 2, 5 y 6).
/// </summary>
public sealed class FilaDeConfiguracionDeEnvio
{
    public const string Tabla = "configuracion_de_envio";

    /// <summary>Siempre 1, con <c>CHECK (id = 1)</c>.</summary>
    public short Id { get; set; }

    public TimeOnly HoraRecordatorio { get; set; }

    public TimeOnly HoraAgendaDelDia { get; set; }

    /// <summary>Destino de "Tus citas de hoy".</summary>
    public string CorreoDoctora { get; set; } = "";

    /// <summary>Destino de la alerta de cancelación tardía.</summary>
    public string CorreoAdministrador { get; set; } = "";

    /// <summary>
    /// La semilla (AD-25): 09:00, 14:00 y dos direcciones que Mailpit atrapa en local; las reales se ponen con el
    /// comando de configuración, sin dejar direcciones personales en el repositorio (Gerardo, 2026-11-08).
    /// </summary>
    public static FilaDeConfiguracionDeEnvio Semilla { get; } = new()
    {
        Id = 1,
        HoraRecordatorio = new TimeOnly(9, 0),
        HoraAgendaDelDia = new TimeOnly(14, 0),
        CorreoDoctora = "doctora@consultorio.local",
        CorreoAdministrador = "administrador@consultorio.local",
    };
}
