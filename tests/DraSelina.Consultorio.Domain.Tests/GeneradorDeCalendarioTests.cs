using System.Text;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Contenido.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.Domain.Tests;

/// <summary>
/// <see cref="GeneradorDeCalendario"/>: un evento iCalendar (RFC 5545) con inicio y fin en UTC, el lugar y el Enlace
/// de gestión (FR-5), sin datos del Paciente (AD-23).
/// </summary>
public class GeneradorDeCalendarioTests
{
    private static readonly CitaId Cita = new(Guid.Parse("0199a3b2-0000-7000-8000-000000000001"));
    private static readonly DateTimeOffset Inicio = new(2026, 11, 3, 22, 0, 0, TimeSpan.Zero);
    private static readonly DatosDelConsultorio Consultorio = new(
        "Hospital Star Médica, consultorio 101", "Mariano Arista No. 735, Col. Barrio de Tequisquiapan", "San Luis Potosí");

    private const string Enlace = "https://citas.prueba.mx/c/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void Es_un_VCALENDAR_con_un_VEVENT_en_UTC_y_lineas_CRLF()
    {
        var archivo = Generar().Contenido;

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", archivo, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", archivo, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", archivo.Replace("\r\n", "", StringComparison.Ordinal), StringComparison.Ordinal);

        var lineas = Desplegar(archivo);
        Assert.Contains("VERSION:2.0", lineas);
        Assert.Single(lineas, l => l == "BEGIN:VEVENT");
        Assert.Single(lineas, l => l == "END:VEVENT");
        Assert.Contains("DTSTART:20261103T220000Z", lineas);
        Assert.Contains("DTEND:20261103T223000Z", lineas);
        Assert.Contains("DTSTAMP:20261102T180000Z", lineas);
        Assert.Contains($"UID:cita-{Cita.Valor:D}@citas.prueba.mx", lineas);
        Assert.Contains("SUMMARY:Primera consulta con la Dra. Selina Medina Castillo", lineas);
        Assert.Contains(
            "LOCATION:Hospital Star Médica\\, consultorio 101\\, Mariano Arista No. 735\\, Col. Barrio de Tequisquiapan\\, San Luis Potosí",
            lineas);
        Assert.Contains($"DESCRIPTION:Para ver\\, reprogramar o cancelar tu cita: {Enlace}", lineas);
    }

    [Fact]
    public void Ninguna_linea_pasa_de_75_octetos_y_las_continuaciones_empiezan_con_espacio()
    {
        var archivo = Generar(tipo: new string('é', 60)).Contenido;

        var fisicas = archivo.Split("\r\n")[..^1];
        Assert.All(fisicas, l => Assert.InRange(Encoding.UTF8.GetByteCount(l), 1, 75));
        Assert.Contains(fisicas, l => l.StartsWith(' '));
        Assert.Contains($"SUMMARY:{new string('é', 60)} con la Dra. Selina Medina Castillo", Desplegar(archivo));
    }

    [Fact]
    public void Escapa_punto_y_coma_coma_diagonal_invertida_y_saltos_de_linea()
    {
        var lineas = Desplegar(Generar(tipo: "a;b,c\\d\ne").Contenido);

        Assert.Contains("SUMMARY:a\\;b\\,c\\\\d\\ne con la Dra. Selina Medina Castillo", lineas);
    }

    [Fact]
    public void La_misma_Cita_da_el_mismo_archivo_y_ToString_no_lo_muestra()
    {
        var archivo = Generar();

        Assert.Equal(archivo, Generar());
        Assert.DoesNotContain(Enlace, archivo.ToString(), StringComparison.Ordinal);
    }

    private static Agenda.Contracts.ArchivoDeCalendario Generar(string tipo = "Primera consulta") =>
        GeneradorDeCalendario.Generar(
            Cita, Inicio, Inicio.AddMinutes(30), tipo, Consultorio, Enlace, "citas.prueba.mx",
            new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.FromHours(-6)));

    // Las líneas lógicas: una continuación (CRLF y un espacio) se une a la anterior (RFC 5545, 3.1).
    internal static List<string> Desplegar(string archivo) =>
        [.. archivo.Replace("\r\n ", "", StringComparison.Ordinal).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)];
}
