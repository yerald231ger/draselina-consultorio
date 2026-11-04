using System.Globalization;
using System.Text;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Agenda.Features;

/// <summary>
/// El único generador del archivo de calendario (Consistency Conventions, "Archivo de calendario"): un VEVENT de
/// iCalendar (RFC 5545). Inicio y fin en UTC (el teléfono lo muestra en su hora local); título con el tipo de visita y
/// la doctora; lugar y dirección de <see cref="IContenidoApi"/> (AD-15); el Enlace de gestión en la descripción (FR-5).
/// El <c>UID</c> sale del id de la Cita: el mismo en cada descarga. Nada del Paciente ni el motivo (AD-23).
/// </summary>
public static class GeneradorDeCalendario
{
    /// <summary>Quién atiende, como lo dice la confirmación.</summary>
    public const string Doctora = "Dra. Selina Medina Castillo";

    private const string Producto = "-//Dra. Selina Medina Castillo//Citas//ES";

    /// <summary>
    /// El archivo del evento.
    /// </summary>
    /// <param name="citaId">Da el <c>UID</c>.</param>
    /// <param name="inicio">Inicio de la Cita.</param>
    /// <param name="fin">Fin de la Cita.</param>
    /// <param name="tipoDeVisita">Nombre del tipo de visita, para el título.</param>
    /// <param name="consultorio">Lugar, dirección y ciudad.</param>
    /// <param name="enlaceDeGestion">El Enlace de gestión completo, para la descripción.</param>
    /// <param name="dominio">El dominio del sitio, para que el <c>UID</c> sea único fuera de él.</param>
    /// <param name="ahora">El <c>DTSTAMP</c> del evento.</param>
    public static ArchivoDeCalendario Generar(
        CitaId citaId,
        DateTimeOffset inicio,
        DateTimeOffset fin,
        string tipoDeVisita,
        DatosDelConsultorio consultorio,
        string enlaceDeGestion,
        string dominio,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(consultorio);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDeVisita);
        ArgumentException.ThrowIfNullOrWhiteSpace(enlaceDeGestion);
        ArgumentException.ThrowIfNullOrWhiteSpace(dominio);

        var archivo = new StringBuilder();
        Linea(archivo, "BEGIN:VCALENDAR");
        Linea(archivo, "VERSION:2.0");
        Linea(archivo, $"PRODID:{Producto}");
        Linea(archivo, "CALSCALE:GREGORIAN");
        Linea(archivo, "METHOD:PUBLISH");
        Linea(archivo, "BEGIN:VEVENT");
        Linea(archivo, $"UID:cita-{citaId.Valor:D}@{dominio}");
        Linea(archivo, $"DTSTAMP:{Utc(ahora)}");
        Linea(archivo, $"DTSTART:{Utc(inicio)}");
        Linea(archivo, $"DTEND:{Utc(fin)}");
        Linea(archivo, $"SUMMARY:{Texto($"{tipoDeVisita} con la {Doctora}")}");
        Linea(archivo, $"LOCATION:{Texto($"{consultorio.Lugar}, {consultorio.Direccion}, {consultorio.Ciudad}")}");
        Linea(archivo, $"DESCRIPTION:{Texto($"Para ver, reprogramar o cancelar tu cita: {enlaceDeGestion}")}");
        Linea(archivo, $"URL:{enlaceDeGestion}");
        Linea(archivo, "STATUS:CONFIRMED");
        Linea(archivo, "TRANSP:OPAQUE");
        Linea(archivo, "END:VEVENT");
        Linea(archivo, "END:VCALENDAR");

        return new ArchivoDeCalendario(archivo.ToString());
    }

    // Forma UTC de RFC 5545 (3.3.5): 19970714T173000Z.
    private static string Utc(DateTimeOffset instante) =>
        instante.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    // TEXT de RFC 5545 (3.3.11): se escapan "\", ";", "," y los saltos de línea.
    private static string Texto(string valor) =>
        valor.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);

    // Una línea de contenido con CRLF, plegada a 75 octetos de UTF-8 sin partir un carácter (RFC 5545, 3.1): cada
    // continuación empieza con un espacio, que cuenta dentro de sus 75.
    private static void Linea(StringBuilder archivo, string linea)
    {
        var octetos = 0;
        const int limite = 75;
        var i = 0;
        while (i < linea.Length)
        {
            var largo = char.IsSurrogatePair(linea, i) ? 2 : 1;
            var bytes = Encoding.UTF8.GetByteCount(linea.AsSpan(i, largo));
            if (octetos + bytes > limite)
            {
                archivo.Append("\r\n ");
                octetos = 1;
            }

            archivo.Append(linea, i, largo);
            octetos += bytes;
            i += largo;
        }

        archivo.Append("\r\n");
    }
}
