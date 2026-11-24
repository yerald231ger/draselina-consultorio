using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Contenido;
using DraSelina.Consultorio.UI.Pages.Sitio;

namespace DraSelina.Consultorio.UI.Tests;

/// <summary>El JSON-LD Physician del sitio (entrada 7.4): horario por Tramo, lo nulo omitido y el texto escapado.</summary>
public sealed class FichaDelConsultorioTests
{
    private static readonly TramoDeLaAgenda Tarde = new(new(15, 30), new(18, 0));

    [Fact]
    public void Un_elemento_por_Tramo_de_cada_dia_con_atencion()
    {
        var raiz = Ficha(
        [
            new DiaDeLaBase(DayOfWeek.Monday, [Tarde]),
            new DiaDeLaBase(DayOfWeek.Tuesday, [new(new(10, 0), new(13, 0)), new(new(16, 0), new(18, 0))]),
            new DiaDeLaBase(DayOfWeek.Sunday, []),
        ]);

        Assert.Equal(
            ["https://schema.org/Monday 15:30-18:00", "https://schema.org/Tuesday 10:00-13:00", "https://schema.org/Tuesday 16:00-18:00"],
            raiz.GetProperty("openingHoursSpecification").EnumerateArray().Select(h =>
                $"{h.GetProperty("dayOfWeek").GetString()} {h.GetProperty("opens").GetString()}-{h.GetProperty("closes").GetString()}"));
        Assert.All(raiz.GetProperty("openingHoursSpecification").EnumerateArray(), h =>
            Assert.Equal("OpeningHoursSpecification", h.GetProperty("@type").GetString()));
    }

    [Fact]
    public void Semana_vacia_sin_horario()
    {
        var raiz = Ficha([new DiaDeLaBase(DayOfWeek.Monday, [])]);

        Assert.False(raiz.TryGetProperty("openingHoursSpecification", out _));
    }

    [Fact]
    public void Sin_WhatsApp_sin_telephone_y_con_el_si()
    {
        Assert.False(Ficha([]).TryGetProperty("telephone", out _));
        Assert.Equal("4441234567", Ficha([], whatsApp: "4441234567").GetProperty("telephone").GetString());
    }

    [Fact]
    public void Datos_de_la_doctora_y_del_consultorio()
    {
        var raiz = Ficha([]);

        Assert.Equal("Physician", raiz.GetProperty("@type").GetString());
        Assert.Equal("Dra. Selina Medina Castillo", raiz.GetProperty("name").GetString());
        Assert.Equal("https://consultorio.example/", raiz.GetProperty("url").GetString());
        Assert.Equal(
            ["Cédula profesional 12093135", "Cédula de especialidad 14899218"],
            raiz.GetProperty("identifier").EnumerateArray().Select(i =>
                $"{i.GetProperty("propertyID").GetString()} {i.GetProperty("value").GetString()}"));
        var direccion = raiz.GetProperty("address");
        Assert.Equal("Hospital Star Médica, consultorio 101, Mariano Arista No. 735, Col. Barrio de Tequisquiapan", direccion.GetProperty("streetAddress").GetString());
        Assert.Equal("San Luis Potosí", direccion.GetProperty("addressLocality").GetString());
        Assert.Equal("MX", direccion.GetProperty("addressCountry").GetString());
        Assert.Equal(ContenidoApi.Doctora.Padecimientos.Count, raiz.GetProperty("knowsAbout").GetArrayLength());
    }

    [Fact]
    public void Un_dato_con_cierre_de_script_no_cierra_el_bloque()
    {
        var doctora = ContenidoApi.Doctora with { Nombre = "</script><script>alert(1)</script>" };

        var json = FichaDelConsultorio.JsonLd(doctora, ContenidoApi.Consultorio, null, [], "https://consultorio.example/");

        Assert.DoesNotContain("<", json, StringComparison.Ordinal);
        Assert.DoesNotContain(">", json, StringComparison.Ordinal);
        using var documento = JsonDocument.Parse(json);
        Assert.Equal("</script><script>alert(1)</script>", documento.RootElement.GetProperty("name").GetString());
    }

    private static JsonElement Ficha(IReadOnlyList<DiaDeLaBase> dias, string? whatsApp = null) =>
        JsonDocument.Parse(FichaDelConsultorio.JsonLd(ContenidoApi.Doctora, ContenidoApi.Consultorio, whatsApp, dias, "https://consultorio.example/"))
            .RootElement.Clone();
}
