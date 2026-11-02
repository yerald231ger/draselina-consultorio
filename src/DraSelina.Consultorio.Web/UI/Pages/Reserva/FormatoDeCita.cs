namespace DraSelina.Consultorio.UI.Pages.Reserva;

/// <summary>
/// Cómo se escriben la fecha y la hora de una Cita en las pantallas del Paciente, en hora local del consultorio:
/// "Mar 6 oct · 4:30 pm", "Martes", "6 de octubre, 4:30 pm". Con nombres propios en español, sin depender de la
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

    /// <summary>"6 de octubre, 4:30 pm".</summary>
    public static string FechaYHora(DateOnly fecha, TimeOnly hora) =>
        $"{fecha.Day} de {Meses[fecha.Month - 1]}, {Hora(hora)}";

    /// <summary>"4:30 pm", "12:00 pm", "9:00 am".</summary>
    public static string Hora(TimeOnly hora)
    {
        var doce = hora.Hour % 12 == 0 ? 12 : hora.Hour % 12;
        return $"{doce}:{hora.Minute:00} {(hora.Hour < 12 ? "am" : "pm")}";
    }

    private static string Mayuscula(string texto) => char.ToUpperInvariant(texto[0]) + texto[1..];
}
