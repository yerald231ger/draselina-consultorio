using DraSelina.Consultorio.Contenido.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// El correo "Cita reprogramada" (EXPERIENCE.md, "Mensajes por correo"; FR-23). La estructura de
/// <see cref="PlantillaDeConfirmacion"/>, con <see cref="MarcoDeCorreo"/>:
/// <list type="bullet">
/// <item>La nueva fecha y hora en grande, el tipo de visita y el lugar con dirección y ciudad; "Abrir en el mapa" si
/// Contenido da enlace.</item>
/// <item>Un solo botón, "Ver o cambiar mi cita", al Enlace de gestión.</item>
/// <item>El archivo de calendario actualizado, si Agenda lo da: lleva el mismo UID, así que el calendario del Paciente
/// mueve el evento `[ASSUMPTION]`. No pide confirmar.</item>
/// </list>
/// </summary>
public static class PlantillaDeReprogramacion
{
    public const string Titulo = "Tu cita se reprogramó";

    /// <summary>Lo que lleva el aviso de una Cita reprogramada.</summary>
    /// <param name="Destinatario">El correo del Paciente.</param>
    /// <param name="Nombre">El nombre del Paciente, como se guardó: el saludo.</param>
    /// <param name="Fecha">La nueva fecha, local del consultorio.</param>
    /// <param name="Hora">La nueva hora local de inicio.</param>
    /// <param name="TipoDeVisita">"Primera consulta"; nulo si Agenda no lo dio.</param>
    /// <param name="Consultorio">Lugar, dirección, ciudad y enlace del mapa, de Contenido.</param>
    /// <param name="EnlaceDeGestion">El de Agenda.</param>
    /// <param name="Calendario">El archivo de calendario; nulo si Agenda no lo dio.</param>
    public sealed record Datos(
        string Destinatario,
        string Nombre,
        DateOnly Fecha,
        TimeOnly Hora,
        string? TipoDeVisita,
        DatosDelConsultorio Consultorio,
        string EnlaceDeGestion,
        AdjuntoDeCorreo? Calendario)
    {
        // Lleva la dirección, el nombre y el Enlace de gestión (AD-23).
        public override string ToString() => "PlantillaDeReprogramacion.Datos(oculto)";
    }

    /// <summary>"Tu cita se reprogramó: Jueves 5 de noviembre, 5:00 pm" `[ASSUMPTION]`.</summary>
    public static string Asunto(DateOnly fecha, TimeOnly hora) =>
        $"{Titulo}: {FormatoDeFechaEnCorreo.Largo(fecha)}, {FormatoDeFechaEnCorreo.Hora(hora)}";

    public static MensajeDeCorreo Armar(Datos datos) => new(
        datos.Destinatario,
        Asunto(datos.Fecha, datos.Hora),
        Html(datos),
        Texto(datos),
        datos.Calendario is { } calendario ? [calendario] : []);

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

        return MarcoDeCorreo.Html(Asunto(datos.Fecha, datos.Hora), datos.Nombre, Titulo, cuerpo);
    }
}
