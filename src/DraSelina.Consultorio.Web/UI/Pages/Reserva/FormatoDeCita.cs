namespace DraSelina.Consultorio.UI.Pages.Reserva;

/// <summary>
/// Cómo se escriben la fecha y la hora de una Cita en las pantallas del Paciente, en hora local del consultorio:
/// "Mar 6 oct · 4:30 pm", "Martes", "Martes 6 de octubre", "6 de octubre, 4:30 pm". Con nombres propios en español, sin depender de la
/// cultura del servidor.
/// </summary>
public static class FormatoDeCita
{
    private static readonly string[] Dias = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    private static readonly string[] Meses =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    /// <summary>"Mar 6 oct · 4:30 pm".</summary>
    public static string Corto(DateOnly fecha, TimeOnly hora) =>
        $"{Mayuscula(Dias[(int)fecha.DayOfWeek][..3])} {fecha.Day} {Meses[fecha.Month - 1][..3]} · {Hora(hora)}";

    /// <summary>"Martes".</summary>
    public static string Dia(DateOnly fecha) => Mayuscula(Dias[(int)fecha.DayOfWeek]);

    /// <summary>"Mar", la abreviatura del día en el chip de Día y hora.</summary>
    public static string DiaCorto(DateOnly fecha) => Mayuscula(Dias[(int)fecha.DayOfWeek][..3]);

    /// <summary>"Martes 6 de octubre".</summary>
    public static string Largo(DateOnly fecha) => $"{Dia(fecha)} {DiaYMes(fecha)}";

    /// <summary>"6 de octubre".</summary>
    public static string DiaYMes(DateOnly fecha) => $"{fecha.Day} de {Meses[fecha.Month - 1]}";

    /// <summary>"oct", el mes corto en el chip de Día y hora.</summary>
    public static string MesCorto(DateOnly fecha) => Meses[fecha.Month - 1][..3];

    /// <summary>"Octubre 2026".</summary>
    public static string MesYAnio(DateOnly fecha) => $"{Mayuscula(Meses[fecha.Month - 1])} {fecha.Year}";

    /// <summary>"6 de octubre, 4:30 pm".</summary>
    public static string FechaYHora(DateOnly fecha, TimeOnly hora) =>
        $"{fecha.Day} de {Meses[fecha.Month - 1]}, {Hora(hora)}";

    /// <summary>"4:30 pm", "12:00 pm", "9:00 am".</summary>
    public static string Hora(TimeOnly hora)
    {
        var doce = hora.Hour % 12 == 0 ? 12 : hora.Hour % 12;
        return $"{doce}:{hora.Minute:00} {(hora.Hour < 12 ? "am" : "pm")}";
    }

    /// <summary>
    /// "hasta 2 horas antes", "hasta 1 hora antes", "hasta 90 minutos antes"; con 0, "hasta la hora de tu cita". Es el
    /// umbral de reprogramación de las reglas de reserva, en minutos.
    /// </summary>
    public static string HastaAntes(int minutos) => minutos <= 0 ? "hasta la hora de tu cita" : $"hasta {Plazo(minutos)} antes";

    /// <summary>
    /// "2 horas", "1 hora", "90 minutos", "1 minuto": un plazo en minutos, en horas si son exactas. Lo usa el panel
    /// "Faltan menos de 2 horas".
    /// </summary>
    public static string Plazo(int minutos) => minutos switch
    {
        60 => "1 hora",
        > 0 when minutos % 60 == 0 => $"{minutos / 60} horas",
        1 => "1 minuto",
        _ => $"{minutos} minutos",
    };

    /// <summary>
    /// "desde mañana y hasta 2 meses", la ventana de reserva de las reglas: anticipación mínima en días ("desde hoy",
    /// "desde mañana", "desde dentro de 3 días") y ventana en meses ("hasta 1 mes", "hasta 2 meses").
    /// </summary>
    public static string Ventana(int anticipacionMinimaDias, int ventanaMeses)
    {
        var desde = anticipacionMinimaDias switch
        {
            <= 0 => "desde hoy",
            1 => "desde mañana",
            _ => $"desde dentro de {anticipacionMinimaDias} días",
        };
        var hasta = ventanaMeses == 1 ? "hasta 1 mes" : $"hasta {ventanaMeses} meses";
        return $"{desde} y {hasta}";
    }

    private static string Mayuscula(string texto) => char.ToUpperInvariant(texto[0]) + texto[1..];
}
