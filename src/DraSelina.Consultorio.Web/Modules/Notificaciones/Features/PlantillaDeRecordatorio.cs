using DraSelina.Consultorio.Contenido.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// El correo "Mañana tienes cita" (EXPERIENCE.md, "Mensajes por correo"; FR-22). La misma estructura y el mismo
/// formato que <see cref="PlantillaDeConfirmacion"/>, con <see cref="MarcoDeCorreo"/>:
/// <list type="bullet">
/// <item>Encabezado con el nombre de la doctora y la especialidad, sin imágenes.</item>
/// <item>La fecha y la hora en grande, el tipo de visita y el lugar con dirección y ciudad; "Abrir en el mapa" si
/// Contenido da enlace.</item>
/// <item>Un solo botón, "Ver o cambiar mi cita", al Enlace de gestión. No pide confirmar ni lleva adjunto.</item>
/// </list>
/// </summary>
public static class PlantillaDeRecordatorio
{
    public const string Titulo = "Mañana tienes cita";

    /// <summary>Lo que lleva el Recordatorio de una Cita.</summary>
    /// <param name="Destinatario">El correo del Paciente.</param>
    /// <param name="Nombre">El nombre del Paciente, como se guardó: el saludo.</param>
    /// <param name="Fecha">Fecha local del consultorio.</param>
    /// <param name="Hora">Hora local de inicio.</param>
    /// <param name="TipoDeVisita">"Primera consulta"; nulo si Agenda no lo dio.</param>
    /// <param name="Consultorio">Lugar, dirección, ciudad y enlace del mapa, de Contenido.</param>
    /// <param name="EnlaceDeGestion">El de Agenda.</param>
    public sealed record Datos(
        string Destinatario,
        string Nombre,
        DateOnly Fecha,
        TimeOnly Hora,
        string? TipoDeVisita,
        DatosDelConsultorio Consultorio,
        string EnlaceDeGestion)
    {
        // Lleva la dirección, el nombre y el Enlace de gestión (AD-23).
        public override string ToString() => "PlantillaDeRecordatorio.Datos(oculto)";
    }

    /// <summary>"Mañana tienes cita: 4:00 pm" `[ASSUMPTION]`.</summary>
    public static string Asunto(TimeOnly hora) => $"{Titulo}: {FormatoDeFechaEnCorreo.Hora(hora)}";

    public static MensajeDeCorreo Armar(Datos datos) => new(
        datos.Destinatario,
        Asunto(datos.Hora),
        Html(datos),
        Texto(datos),
        []);

    private static string Texto(Datos datos)
    {
        var texto = MarcoDeCorreo.Texto(datos.Nombre, Titulo);
        texto.AppendLine(FormatoDeFechaEnCorreo.Largo(datos.Fecha));
        texto.AppendLine(FormatoDeFechaEnCorreo.Hora(datos.Hora));
        if (datos.TipoDeVisita is { } tipo)
        {
            texto.AppendLine(tipo);
        }

        MarcoDeCorreo.LugarEnTexto(texto, datos.Consultorio);
        texto.AppendLine();
        texto.AppendLine($"{PlantillaDeConfirmacion.Boton}: {datos.EnlaceDeGestion}");
        return texto.ToString();
    }

    private static string Html(Datos datos)
    {
        var cuerpo = $"""
            {MarcoDeCorreo.FechaYHora(datos.Fecha, datos.Hora)}
            {MarcoDeCorreo.Tipo(datos.TipoDeVisita)}
            {MarcoDeCorreo.Lugar(datos.Consultorio)}
            {MarcoDeCorreo.Boton(datos.EnlaceDeGestion, PlantillaDeConfirmacion.Boton)}
            """;

        return MarcoDeCorreo.Html(Asunto(datos.Hora), datos.Nombre, Titulo, cuerpo);
    }
}
