using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace DraSelina.Consultorio.Notificaciones.Features;

/// <summary>
/// La estructura que comparten los avisos de reprogramación y cancelación (<see cref="PlantillaDeReprogramacion"/>,
/// <see cref="PlantillaDeCancelacion"/>): la misma de <see cref="PlantillaDeConfirmacion"/> y
/// <see cref="PlantillaDeRecordatorio"/>, con el encabezado de la doctora y la especialidad sin imágenes, el saludo, el
/// título, estilos en línea y los colores del sitio; y su versión de texto plano, legible por sí sola.
/// </summary>
internal static class MarcoDeCorreo
{
    // Escapa lo que el HTML exige (<, >, &, comillas) y deja los acentos como texto: el correo va en UTF-8.
    private static readonly HtmlEncoder Codificador = HtmlEncoder.Create(UnicodeRanges.All);

    public static string H(string texto) => Codificador.Encode(texto);

    // "Hola, Ana Sofía López:" o, en un correo interno, "Hola:".
    private static string Saludo(string? nombre) => nombre is { } n ? $"Hola, {n}:" : "Hola:";

    /// <summary>
    /// El encabezado, el saludo y el título del texto plano, con una línea en blanco al final. Sin
    /// <paramref name="nombre"/>, el saludo es "Hola:" (los correos internos).
    /// </summary>
    public static StringBuilder Texto(string? nombre, string titulo)
    {
        var texto = new StringBuilder();
        texto.AppendLine(PlantillaDeConfirmacion.Doctora);
        texto.AppendLine(PlantillaDeConfirmacion.Especialidad);
        texto.AppendLine();
        texto.AppendLine(Saludo(nombre));
        texto.AppendLine();
        texto.AppendLine($"{titulo}.");
        texto.AppendLine();
        return texto;
    }

    /// <summary>La fecha y la hora en grande.</summary>
    public static string FechaYHora(DateOnly fecha, TimeOnly hora) => $"""
        <p style="margin:0;font-size:26px;font-weight:700;">{H(FormatoDeFechaEnCorreo.Largo(fecha))}</p>
        <p style="margin:4px 0 0;font-size:26px;font-weight:700;">{H(FormatoDeFechaEnCorreo.Hora(hora))}</p>
        """;

    /// <summary>El tipo de visita bajo la fecha; vacío si no hay.</summary>
    public static string Tipo(string? tipo) => tipo is { } t
        ? $"""<p style="margin:8px 0 0;font-size:16px;color:#4A3039;">{H(t)}</p>"""
        : "";

    /// <summary>Un párrafo de texto.</summary>
    public static string Parrafo(string texto) =>
        $"""<p style="margin:16px 0 0;font-size:16px;color:#4A3039;">{H(texto)}</p>""";

    /// <summary>El único botón del correo.</summary>
    public static string Boton(string enlace, string texto) =>
        $"""<p style="margin:28px 0 0;"><a href="{H(enlace)}" style="display:inline-block;padding:14px 24px;background:#6B2A45;color:#ffffff;text-decoration:none;font-weight:700;border-radius:8px;font-size:16px;">{H(texto)}</a></p>""";

    /// <summary>El documento completo: encabezado, saludo, título y <paramref name="cuerpo"/>, ya en HTML.</summary>
    public static string Html(string asunto, string? nombre, string titulo, string cuerpo) => $"""
        <!doctype html>
        <html lang="es-MX">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{H(asunto)}</title>
        </head>
        <body style="margin:0;padding:0;background:#FBF6F5;font-family:Arial,Helvetica,sans-serif;color:#3A1F29;">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#FBF6F5;">
        <tr><td align="center" style="padding:24px 16px;">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:12px;">
        <tr><td style="padding:24px 24px 16px;border-bottom:1px solid #EADBD9;">
        <p style="margin:0;font-size:18px;font-weight:700;">{H(PlantillaDeConfirmacion.Doctora)}</p>
        <p style="margin:4px 0 0;font-size:14px;color:#6E5560;">{H(PlantillaDeConfirmacion.Especialidad)}</p>
        </td></tr>
        <tr><td style="padding:24px;">
        <p style="margin:0 0 8px;font-size:16px;">{H(Saludo(nombre))}</p>
        <h1 style="margin:0 0 16px;font-size:22px;">{H(titulo)}</h1>
        {cuerpo}
        </td></tr>
        </table>
        </td></tr>
        </table>
        </body>
        </html>
        """;
}
