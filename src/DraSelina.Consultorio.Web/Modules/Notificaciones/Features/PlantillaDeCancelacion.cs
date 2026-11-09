namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// Los correos de una Cita cancelada (EXPERIENCE.md, "Mensajes por correo"; FR-23), con <see cref="MarcoDeCorreo"/>.
/// Ninguno pide confirmar ni lleva Enlace de gestión: la Cita ya no se puede cambiar.
/// <list type="bullet">
/// <item><see cref="Clase.PorElPaciente"/> y <see cref="Clase.Tardia"/>: "Tu cita está cancelada", con la fecha y la
/// hora en grande, el tipo de visita y el botón "Agendar otra cita" a <c>/reservar</c>. La tardía agrega
/// <see cref="NotaTardia"/>, la frase de la pantalla de la 3.4.</item>
/// <item><see cref="Clase.PorElConsultorio"/>: la disculpa del Consultorio, sin excusas técnicas, y el botón
/// "Elegir otro horario" a <c>/reservar</c>.</item>
/// </list>
/// </summary>
public static class PlantillaDeCancelacion
{
    /// <summary>El título y el asunto de la cancelación del Paciente, como en la pantalla `[ASSUMPTION]`.</summary>
    public const string Titulo = "Tu cita está cancelada";

    /// <summary>
    /// La frase de una cancelación tardía (RN-16). Copia <c>UI.Pages.Gestion.ConfirmacionDeCancelacion.NotaTardia</c>,
    /// que Notificaciones no puede usar (AD-1); una prueba de integración compara las dos.
    /// </summary>
    public const string NotaTardia = "Gracias por avisarnos. Quedó registrada como cancelación tardía.";

    public const string AgendarOtraCita = "Agendar otra cita";

    /// <summary>El título y el asunto de la cancelación del Consultorio `[ASSUMPTION]`.</summary>
    public const string TituloPorElConsultorio = "Tuvimos que cancelar tu cita";

    public const string ElegirOtroHorario = "Elegir otro horario";

    /// <summary>Qué correo de cancelación es, según el motivo que decidió Agenda (E-16).</summary>
    public enum Clase
    {
        PorElPaciente = 1,
        Tardia = 2,
        PorElConsultorio = 3,
    }

    /// <summary>Lo que lleva el aviso de una Cita cancelada.</summary>
    /// <param name="Destinatario">El correo del Paciente.</param>
    /// <param name="Nombre">El nombre del Paciente, como se guardó: el saludo.</param>
    /// <param name="Fecha">Fecha local del consultorio de la Cita cancelada.</param>
    /// <param name="Hora">Hora local de inicio.</param>
    /// <param name="TipoDeVisita">"Primera consulta"; nulo si Agenda no lo dio.</param>
    /// <param name="UrlDeReservar">La dirección absoluta de <c>/reservar</c> en el sitio.</param>
    /// <param name="Clase">Qué correo de cancelación es.</param>
    public sealed record Datos(
        string Destinatario,
        string Nombre,
        DateOnly Fecha,
        TimeOnly Hora,
        string? TipoDeVisita,
        string UrlDeReservar,
        Clase Clase)
    {
        // Lleva la dirección y el nombre (AD-23).
        public override string ToString() => "PlantillaDeCancelacion.Datos(oculto)";
    }

    /// <summary>"Tu cita está cancelada" o "Tuvimos que cancelar tu cita" `[ASSUMPTION]`.</summary>
    public static string Asunto(Clase clase) => clase == Clase.PorElConsultorio ? TituloPorElConsultorio : Titulo;

    /// <summary>
    /// "Lo sentimos mucho: tuvimos que cancelar tu cita del martes 3 de noviembre, 4:00 pm. Elige otro horario cuando te
    /// acomode." `[ASSUMPTION: texto]`. El día va en minúscula: queda a media frase.
    /// </summary>
    public static string Disculpa(DateOnly fecha, TimeOnly hora)
    {
        var largo = FormatoDeFechaEnCorreo.Largo(fecha);
        var enLaFrase = $"{char.ToLowerInvariant(largo[0])}{largo[1..]}";
        return $"Lo sentimos mucho: tuvimos que cancelar tu cita del {enLaFrase}, {FormatoDeFechaEnCorreo.Hora(hora)}. "
            + "Elige otro horario cuando te acomode.";
    }

    public static MensajeDeCorreo Armar(Datos datos) => new(
        datos.Destinatario,
        Asunto(datos.Clase),
        Html(datos),
        Texto(datos),
        []);

    private static string Texto(Datos datos)
    {
        var texto = MarcoDeCorreo.Texto(datos.Nombre, Asunto(datos.Clase));
        if (datos.Clase == Clase.PorElConsultorio)
        {
            texto.AppendLine(Disculpa(datos.Fecha, datos.Hora));
            texto.AppendLine();
            texto.AppendLine($"{ElegirOtroHorario}: {datos.UrlDeReservar}");
            return texto.ToString();
        }

        texto.AppendLine(FormatoDeFechaEnCorreo.Largo(datos.Fecha));
        texto.AppendLine(FormatoDeFechaEnCorreo.Hora(datos.Hora));
        if (datos.TipoDeVisita is { } tipo)
        {
            texto.AppendLine(tipo);
        }

        if (datos.Clase == Clase.Tardia)
        {
            texto.AppendLine();
            texto.AppendLine(NotaTardia);
        }

        texto.AppendLine();
        texto.AppendLine($"{AgendarOtraCita}: {datos.UrlDeReservar}");
        return texto.ToString();
    }

    private static string Html(Datos datos)
    {
        var cuerpo = datos.Clase == Clase.PorElConsultorio
            ? $"""
                {MarcoDeCorreo.Parrafo(Disculpa(datos.Fecha, datos.Hora))}
                {MarcoDeCorreo.Boton(datos.UrlDeReservar, ElegirOtroHorario)}
                """
            : $"""
                {MarcoDeCorreo.FechaYHora(datos.Fecha, datos.Hora)}
                {MarcoDeCorreo.Tipo(datos.TipoDeVisita)}
                {(datos.Clase == Clase.Tardia ? MarcoDeCorreo.Parrafo(NotaTardia) : "")}
                {MarcoDeCorreo.Boton(datos.UrlDeReservar, AgendarOtraCita)}
                """;

        return MarcoDeCorreo.Html(Asunto(datos.Clase), datos.Nombre, Asunto(datos.Clase), cuerpo);
    }
}
