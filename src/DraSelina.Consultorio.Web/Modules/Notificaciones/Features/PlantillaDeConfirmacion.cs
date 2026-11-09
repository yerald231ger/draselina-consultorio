using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
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
/// HTML con estilos en línea (los clientes de correo no leen hojas de estilo) y los colores del sitio, y texto plano
/// legible por sí solo.
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
        var c = datos.Consultorio;
        var texto = new StringBuilder();
        texto.AppendLine(Doctora);
        texto.AppendLine(Especialidad);
        texto.AppendLine();
        texto.AppendLine($"Hola, {datos.Nombre}:");
        texto.AppendLine();
        texto.AppendLine($"{Titulo}.");
        texto.AppendLine();
        texto.AppendLine(FormatoDeFechaEnCorreo.Largo(datos.Fecha));
        texto.AppendLine(FormatoDeFechaEnCorreo.Hora(datos.Hora));
        if (datos.TipoDeVisita is { } tipo)
        {
            texto.AppendLine(tipo);
        }

        texto.AppendLine();
        texto.AppendLine(c.Lugar);
        texto.AppendLine(c.Direccion);
        texto.AppendLine(c.Ciudad);
        if (c.EnlaceDelMapa is { } mapa)
        {
            texto.AppendLine($"{AbrirEnElMapa}: {mapa}");
        }

        texto.AppendLine();
        texto.AppendLine($"{Boton}: {datos.EnlaceDeGestion}");
        return texto.ToString();
    }

    private static string Html(Datos datos)
    {
        var c = datos.Consultorio;
        var tipo = datos.TipoDeVisita is { } t
            ? $"""<p style="margin:8px 0 0;font-size:16px;color:#4A3039;">{H(t)}</p>"""
            : "";
        var mapa = c.EnlaceDelMapa is { } enlace
            ? $"""<p style="margin:8px 0 0;font-size:15px;"><a href="{H(enlace)}" style="color:#6B2A45;font-weight:600;">{H(AbrirEnElMapa)}</a></p>"""
            : "";

        return $"""
            <!doctype html>
            <html lang="es-MX">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{H(Asunto(datos.Fecha, datos.Hora))}</title>
            </head>
            <body style="margin:0;padding:0;background:#FBF6F5;font-family:Arial,Helvetica,sans-serif;color:#3A1F29;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#FBF6F5;">
            <tr><td align="center" style="padding:24px 16px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:12px;">
            <tr><td style="padding:24px 24px 16px;border-bottom:1px solid #EADBD9;">
            <p style="margin:0;font-size:18px;font-weight:700;">{H(Doctora)}</p>
            <p style="margin:4px 0 0;font-size:14px;color:#6E5560;">{H(Especialidad)}</p>
            </td></tr>
            <tr><td style="padding:24px;">
            <p style="margin:0 0 8px;font-size:16px;">Hola, {H(datos.Nombre)}:</p>
            <h1 style="margin:0 0 16px;font-size:22px;">{H(Titulo)}</h1>
            <p style="margin:0;font-size:26px;font-weight:700;">{H(FormatoDeFechaEnCorreo.Largo(datos.Fecha))}</p>
            <p style="margin:4px 0 0;font-size:26px;font-weight:700;">{H(FormatoDeFechaEnCorreo.Hora(datos.Hora))}</p>
            {tipo}
            <div style="margin:20px 0 0;padding:16px 0 0;border-top:1px solid #EADBD9;">
            <p style="margin:0;font-size:16px;font-weight:600;">{H(c.Lugar)}</p>
            <p style="margin:4px 0 0;font-size:15px;color:#4A3039;">{H(c.Direccion)}</p>
            <p style="margin:4px 0 0;font-size:15px;color:#4A3039;">{H(c.Ciudad)}</p>
            {mapa}
            </div>
            <p style="margin:28px 0 0;"><a href="{H(datos.EnlaceDeGestion)}" style="display:inline-block;padding:14px 24px;background:#6B2A45;color:#ffffff;text-decoration:none;font-weight:700;border-radius:8px;font-size:16px;">{H(Boton)}</a></p>
            </td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """;
    }

    // Escapa lo que el HTML exige (<, >, &, comillas) y deja los acentos como texto: el correo va en UTF-8.
    private static readonly HtmlEncoder Codificador = HtmlEncoder.Create(UnicodeRanges.All);

    private static string H(string texto) => Codificador.Encode(texto);
}
