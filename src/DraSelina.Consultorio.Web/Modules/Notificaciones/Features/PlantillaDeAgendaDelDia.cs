namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// "Tus citas de hoy" a la doctora (CAP-20, FR-25; UJ-5), con <see cref="MarcoDeCorreo"/>: una línea por Cita, con la
/// hora, el Paciente y el motivo (Decision del 2026-11-08); sin Citas, "No hay citas hoy". Sin botones.
/// </summary>
public static class PlantillaDeAgendaDelDia
{
    public const string Titulo = "Tus citas de hoy";

    /// <summary>"Hola, doctora:" `[ASSUMPTION]`.</summary>
    public const string Saludo = "doctora";

    public const string SinCitas = "No hay citas hoy";

    /// <summary>Lo que lleva el correo.</summary>
    /// <param name="Destinatario">El correo de la doctora.</param>
    /// <param name="Fecha">La fecha local del consultorio.</param>
    /// <param name="Citas">En orden de inicio; vacía si no hay.</param>
    public sealed record Datos(string Destinatario, DateOnly Fecha, IReadOnlyList<Cita> Citas)
    {
        // Lleva la dirección, los nombres y los motivos (AD-23).
        public override string ToString() => "PlantillaDeAgendaDelDia.Datos(oculto)";
    }

    /// <summary>Una Cita del día.</summary>
    /// <param name="Hora">Hora local de inicio.</param>
    /// <param name="Paciente">El nombre del Paciente, como se guardó.</param>
    /// <param name="Motivo">Nulo si la Cita no lo tiene.</param>
    public sealed record Cita(TimeOnly Hora, string Paciente, string? Motivo)
    {
        // Lleva el nombre y el motivo (AD-23).
        public override string ToString() => "PlantillaDeAgendaDelDia.Cita(oculto)";
    }

    /// <summary>"Tus citas de hoy: Martes 3 de noviembre" `[ASSUMPTION]`.</summary>
    public static string Asunto(DateOnly fecha) => $"{Titulo}: {FormatoDeFechaEnCorreo.Largo(fecha)}";

    /// <summary>"4:00 pm · Ana Sofía López · Dolor de rodilla", sin el motivo cuando no hay.</summary>
    public static string Linea(Cita cita) => cita.Motivo is { } motivo
        ? $"{FormatoDeFechaEnCorreo.Hora(cita.Hora)} · {cita.Paciente} · {motivo}"
        : $"{FormatoDeFechaEnCorreo.Hora(cita.Hora)} · {cita.Paciente}";

    public static MensajeDeCorreo Armar(Datos datos)
    {
        var lineas = datos.Citas.Count == 0 ? [SinCitas] : datos.Citas.Select(Linea).ToList();

        var texto = MarcoDeCorreo.Texto(Saludo, Titulo);
        foreach (var linea in lineas)
        {
            texto.AppendLine(linea);
        }

        var cuerpo = string.Join('\n', lineas.Select(MarcoDeCorreo.Parrafo));
        var asunto = Asunto(datos.Fecha);

        return new MensajeDeCorreo(datos.Destinatario, asunto, MarcoDeCorreo.Html(asunto, Saludo, Titulo, cuerpo), texto.ToString(), []);
    }
}
