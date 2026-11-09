namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// La alerta de cancelación tardía al Administrador (EXPERIENCE.md, "Mensajes por correo"; FR-24), con
/// <see cref="MarcoDeCorreo"/>: el Paciente y la fecha y hora de la Cita. Sin el botón "Abrir la Agenda": lo agrega la
/// épica 5 cuando exista la Agenda de administración (Gerardo, 2026-11-08).
/// </summary>
public static class PlantillaDeAlertaTardia
{
    public const string Titulo = "Cancelación tardía";

    /// <summary>Lo que lleva la alerta.</summary>
    /// <param name="Destinatario">El correo del Administrador.</param>
    /// <param name="Paciente">El nombre del Paciente, como se guardó.</param>
    /// <param name="Fecha">Fecha local del consultorio de la Cita cancelada.</param>
    /// <param name="Hora">Hora local de inicio.</param>
    public sealed record Datos(string Destinatario, string Paciente, DateOnly Fecha, TimeOnly Hora)
    {
        // Lleva la dirección y el nombre (AD-23).
        public override string ToString() => "PlantillaDeAlertaTardia.Datos(oculto)";
    }

    /// <summary>"Cancelación tardía: Ana Sofía López, Martes 3 de noviembre, 4:00 pm" `[ASSUMPTION]`.</summary>
    public static string Asunto(Datos datos) =>
        $"{Titulo}: {datos.Paciente}, {FormatoDeFechaEnCorreo.Largo(datos.Fecha)}, {FormatoDeFechaEnCorreo.Hora(datos.Hora)}";

    /// <summary>"Ana Sofía López canceló su cita tarde." `[ASSUMPTION: texto]`</summary>
    public static string Frase(string paciente) => $"{paciente} canceló su cita tarde.";

    public static MensajeDeCorreo Armar(Datos datos)
    {
        var texto = MarcoDeCorreo.Texto(nombre: null, Titulo);
        texto.AppendLine(Frase(datos.Paciente));
        texto.AppendLine();
        texto.AppendLine(FormatoDeFechaEnCorreo.Largo(datos.Fecha));
        texto.AppendLine(FormatoDeFechaEnCorreo.Hora(datos.Hora));

        var cuerpo = $"""
            {MarcoDeCorreo.Parrafo(Frase(datos.Paciente))}
            {MarcoDeCorreo.FechaYHora(datos.Fecha, datos.Hora)}
            """;

        return new MensajeDeCorreo(
            datos.Destinatario, Asunto(datos), MarcoDeCorreo.Html(Asunto(datos), nombre: null, Titulo, cuerpo), texto.ToString(), []);
    }
}
