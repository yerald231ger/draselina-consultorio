using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Contenido.Contracts;

namespace DraSelina.Consultorio.UI.Pages.Sitio;

/// <summary>
/// Los datos estructurados del consultorio para buscadores (FR-29; entrada 7.4): un <c>Physician</c> de schema.org en
/// JSON-LD con los datos de la doctora y del consultorio de Contenido (AD-15) y su horario de los días de
/// <see cref="HorarioDeAtencion"/>, un elemento por Tramo. Lo nulo de Contenido (WhatsApp, pendientes) no aparece.
/// </summary>
public static class FichaDelConsultorio
{
    // El codificador por omisión de System.Text.Json escapa <, > y &: el JSON no puede cerrar el <script> que lo
    // contiene.
    private static readonly JsonSerializerOptions Opciones = new() { WriteIndented = false };

    /// <summary>El JSON-LD del consultorio, listo para ir dentro de <c>&lt;script type="application/ld+json"&gt;</c>.</summary>
    /// <param name="doctora">Los datos de la doctora.</param>
    /// <param name="consultorio">Lugar, dirección y ciudad.</param>
    /// <param name="whatsApp">El WhatsApp de Contenido; nulo o vacío, sin <c>telephone</c>.</param>
    /// <param name="dias">Los días de la Agenda base, de <see cref="HorarioDeAtencion"/>.</param>
    /// <param name="url">La dirección absoluta del sitio: "https://host/".</param>
    public static string JsonLd(
        DatosDeLaDoctora doctora,
        DatosDelConsultorio consultorio,
        string? whatsApp,
        IReadOnlyList<DiaDeLaBase> dias,
        string url)
    {
        var ficha = new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Physician",
            ["name"] = doctora.Nombre,
            ["url"] = url,
            ["identifier"] = new JsonArray(
                Identificador("Cédula profesional", doctora.CedulaProfesional),
                Identificador("Cédula de especialidad", doctora.CedulaDeEspecialidad)),
            ["address"] = new JsonObject
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = $"{consultorio.Lugar}, {consultorio.Direccion}",
                ["addressLocality"] = consultorio.Ciudad,
                ["addressCountry"] = "MX",
            },
            ["knowsAbout"] = new JsonArray([.. doctora.Padecimientos.Select(p => (JsonNode?)JsonValue.Create(p.Nombre))]),
        };

        if (!string.IsNullOrWhiteSpace(whatsApp))
        {
            ficha["telephone"] = whatsApp;
        }

        var horario = dias
            .SelectMany(dia => dia.Tramos.Select(tramo => (JsonNode?)new JsonObject
            {
                ["@type"] = "OpeningHoursSpecification",
                ["dayOfWeek"] = $"https://schema.org/{dia.Dia}",
                ["opens"] = Hora(tramo.Inicio),
                ["closes"] = Hora(tramo.Fin),
            }))
            .ToArray();
        if (horario.Length > 0)
        {
            ficha["openingHoursSpecification"] = new JsonArray(horario);
        }

        return ficha.ToJsonString(Opciones);
    }

    private static JsonObject Identificador(string nombre, string valor) => new()
    {
        ["@type"] = "PropertyValue",
        ["propertyID"] = nombre,
        ["value"] = valor,
    };

    private static string Hora(TimeOnly hora) => hora.ToString("HH:mm", CultureInfo.InvariantCulture);
}
