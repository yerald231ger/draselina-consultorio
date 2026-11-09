namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// La fecha y la hora de una Cita en los correos, en hora local del consultorio y con el mismo formato que las
/// pantallas: "Martes 3 de noviembre", "4:00 pm". Copia <c>Largo</c> y <c>Hora</c> de
/// <c>UI.Pages.Reserva.FormatoDeCita</c>, que Notificaciones no puede usar (AD-1); una prueba de integración compara
/// las dos para que no se separen.
/// </summary>
public static class FormatoDeFechaEnCorreo
{
    private static readonly string[] Dias = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    private static readonly string[] Meses =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    /// <summary>"Martes 3 de noviembre".</summary>
    public static string Largo(DateOnly fecha)
    {
        var dia = Dias[(int)fecha.DayOfWeek];
        return $"{char.ToUpperInvariant(dia[0])}{dia[1..]} {fecha.Day} de {Meses[fecha.Month - 1]}";
    }

    /// <summary>"4:30 pm", "12:00 pm", "9:00 am".</summary>
    public static string Hora(TimeOnly hora)
    {
        var doce = hora.Hour % 12 == 0 ? 12 : hora.Hour % 12;
        return $"{doce}:{hora.Minute:00} {(hora.Hour < 12 ? "am" : "pm")}";
    }
}
