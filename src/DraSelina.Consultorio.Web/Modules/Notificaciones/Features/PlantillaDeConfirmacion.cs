using DraSelina.Consultorio.Contenido.Contracts;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// El correo "Cita confirmada" (EXPERIENCE.md, "Mensajes por correo"; FR-21). De tú, en español de México:
/// <list type="bullet">
/// <item>Encabezado con el nombre de la doctora y la especialidad, sin imágenes `[ASSUMPTION]`.</item>
/// <item>La fecha y la hora en grande, el tipo de visita y el lugar con dirección y ciudad; "Abrir en el mapa" si
/// Contenido da enlace.</item>
/// <item>Un solo botón, "Ver o cambiar mi cita", al Enlace de gestión.</item>
/// <item>El archivo de calendario adjunto, si Agenda lo da. No pide confirmar.</item>
/// </list>
/// Con <see cref="MarcoDeCorreo"/>: HTML con estilos en línea (los clientes de correo no leen hojas de estilo) y los
/// colores del sitio, y texto plano legible por sí solo.
/// </summary>
public static class PlantillaDeConfirmacion
{
    /// <summary>Como en el encabezado del sitio (<c>UI/Layout/LayoutPublico.razor</c>).</summary>
    public const string Doctora = "Dra. Selina Medina Castillo";

    /// <summary>Como en el encabezado del sitio.</summary>
    public const string Especialidad = "Traumatología y Ortopedia";

    public const string Titulo = "Tu cita está confirmada";

    public const string Boton = "Ver o cambiar mi cita";

    public const string AbrirEnElMapa = "Abrir en el mapa";

    /// <summary>Lo que lleva el correo de una Cita.</summary>
    /// <param name="Destinatario">El correo del Paciente.</param>
    /// <param name="Nombre">El nombre del Paciente, como se guardó: el saludo.</param>
    /// <param name="Fecha">Fecha local del consultorio.</param>
    /// <param name="Hora">Hora local de inicio.</param>
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
        public override string ToString() => "PlantillaDeConfirmacion.Datos(oculto)";
    }

    /// <summary>"Tu cita está confirmada: Martes 3 de noviembre, 4:00 pm" `[ASSUMPTION]`.</summary>
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
        texto.AppendLine($"{Boton}: {datos.EnlaceDeGestion}");
        return texto.ToString();
    }

    private static string Html(Datos datos)
    {
        var cuerpo = $"""
            {MarcoDeCorreo.FechaYHora(datos.Fecha, datos.Hora)}
            {MarcoDeCorreo.Tipo(datos.TipoDeVisita)}
            {MarcoDeCorreo.Lugar(datos.Consultorio)}
            {MarcoDeCorreo.Boton(datos.EnlaceDeGestion, Boton)}
            """;

        return MarcoDeCorreo.Html(Asunto(datos.Fecha, datos.Hora), datos.Nombre, Titulo, cuerpo);
    }
}
