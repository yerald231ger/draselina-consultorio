using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// "Agregar a mi calendario" (FR-5): <c>GET /c/&lt;token&gt;/calendario.ics</c> por HTTP contra la web real. Se llega
/// con el token del Enlace de gestión, nunca con el id (AD-19); solo una Cita Agendada futura entrega el archivo, todo
/// lo demás es 404 sin detalle (RN-22). El archivo no lleva datos del Paciente ni motivo, y ni las trazas ni los
/// registros llevan el token (AD-23). Es una lectura: no escribe bitácora ni outbox.
/// </summary>
public sealed class CalendarioTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Una_Cita_Agendada_futura_descarga_su_evento_con_inicio_y_fin_en_UTC_lugar_y_Enlace_de_gestion()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        using var cliente = escenario.Web.CreateClient();

        using var respuesta = await cliente.GetAsync(Ruta(cita.EnlaceDeGestion), Cancelacion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("text/calendar", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", respuesta.Content.Headers.ContentType?.CharSet);
        Assert.Equal(ArchivoDeCalendario.NombreDeArchivo, respuesta.Content.Headers.ContentDisposition?.FileNameStar
                                                          ?? respuesta.Content.Headers.ContentDisposition?.FileName);
        Assert.True(respuesta.Headers.CacheControl?.NoStore, "El archivo lleva el Enlace de gestión: no se guarda en caché.");

        var archivo = await respuesta.Content.ReadAsStringAsync(Cancelacion);
        Assert.StartsWith("BEGIN:VCALENDAR\r\n", archivo, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", archivo, StringComparison.Ordinal);
        var lineas = Desplegar(archivo);
        Assert.Single(lineas, l => l == "BEGIN:VEVENT");
        Assert.Single(lineas, l => l == "END:VEVENT");

        // Martes 3 de noviembre, 16:00 a 16:30 en San Luis Potosí (UTC-6): 22:00 a 22:30 UTC.
        Assert.Contains("DTSTART:20261103T220000Z", lineas);
        Assert.Contains("DTEND:20261103T223000Z", lineas);
        Assert.Contains("SUMMARY:Primera consulta con la Dra. Selina Medina Castillo", lineas);
        Assert.Contains(
            "LOCATION:Hospital Star Médica\\, consultorio 101\\, Mariano Arista No. 735\\, Col. Barrio de Tequisquiapan\\, San Luis Potosí",
            lineas);
        Assert.Contains(lineas, l => l.StartsWith("DESCRIPTION:", StringComparison.Ordinal) && l.Contains(cita.EnlaceDeGestion, StringComparison.Ordinal));
        Assert.Contains($"UID:cita-{cita.CitaId.Valor:D}@citas.prueba.mx", lineas);
    }

    [Fact]
    public async Task El_archivo_no_lleva_motivo_nombre_telefono_ni_correo()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var comando = ReservaEnLineaTests.Comando();
        var cita = (await escenario.AgendarAsync(comando)).Valor;
        using var cliente = escenario.Web.CreateClient();

        var archivo = Desplegado(await cliente.GetStringAsync(Ruta(cita.EnlaceDeGestion), Cancelacion));

        Assert.DoesNotContain(comando.Motivo!, archivo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rodilla", archivo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ana", archivo, StringComparison.Ordinal);
        Assert.DoesNotContain("López", archivo, StringComparison.Ordinal);
        Assert.DoesNotContain("4441234567", archivo, StringComparison.Ordinal);
        Assert.DoesNotContain("444 123 4567", archivo, StringComparison.Ordinal);
        Assert.DoesNotContain("1234567", archivo, StringComparison.Ordinal);
        Assert.DoesNotContain(comando.Correo!, archivo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@correo", archivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dos_descargas_de_la_misma_Cita_llevan_el_mismo_UID()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        using var cliente = escenario.Web.CreateClient();

        var primera = Desplegar(await cliente.GetStringAsync(Ruta(cita.EnlaceDeGestion), Cancelacion));
        var segunda = Desplegar(await cliente.GetStringAsync(Ruta(cita.EnlaceDeGestion), Cancelacion));

        var uid = Assert.Single(primera, l => l.StartsWith("UID:", StringComparison.Ordinal));
        Assert.Equal(uid, Assert.Single(segunda, l => l.StartsWith("UID:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Dos_Citas_llevan_UID_distintos()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var una = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando(hora: new TimeOnly(16, 0)))).Valor;
        var otra = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando(hora: new TimeOnly(16, 30)))).Valor;
        using var cliente = escenario.Web.CreateClient();

        var uidDeUna = Desplegar(await cliente.GetStringAsync(Ruta(una.EnlaceDeGestion), Cancelacion)).Single(l => l.StartsWith("UID:", StringComparison.Ordinal));
        var uidDeOtra = Desplegar(await cliente.GetStringAsync(Ruta(otra.EnlaceDeGestion), Cancelacion)).Single(l => l.StartsWith("UID:", StringComparison.Ordinal));

        Assert.NotEqual(uidDeUna, uidDeOtra);
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("no-es-un-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB")]
    public async Task Un_token_que_no_es_de_ninguna_Cita_responde_404_sin_detalle(string token)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await escenario.AgendarAsync(ReservaEnLineaTests.Comando());
        using var cliente = escenario.Web.CreateClient();

        await NoEncontradoAsync(cliente, $"/c/{token}/calendario.ics");
    }

    [Fact]
    public async Task El_id_de_la_Cita_en_lugar_del_token_responde_404()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        using var cliente = escenario.Web.CreateClient();

        await NoEncontradoAsync(cliente, $"/c/{cita.CitaId.Valor}/calendario.ics");
        await NoEncontradoAsync(cliente, $"/c/{cita.CitaId.Valor:N}/calendario.ics");
    }

    [Fact]
    public async Task Una_Cita_cuyo_inicio_ya_paso_responde_404_sin_detalle()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;

        // El reloj de la web está en el lunes 2 a las 18:00 UTC: la Cita empieza justo entonces, ya pasó (RN-31).
        await escenario.EjecutarAsync(
            $"update agenda.cita set inicio = '{ReservaEnLineaTests.Ahora:O}', fin = '{ReservaEnLineaTests.Ahora.AddMinutes(30):O}' where id = '{cita.CitaId.Valor}'");
        using var cliente = escenario.Web.CreateClient();

        await NoEncontradoAsync(cliente, Ruta(cita.EnlaceDeGestion));
    }

    [Theory]
    [InlineData("estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now()")]
    [InlineData("estado = 'NoSePresento'")]
    public async Task Una_Cita_que_no_esta_Agendada_responde_404_sin_detalle(string cambio)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        await escenario.EjecutarAsync($"update agenda.cita set {cambio} where id = '{cita.CitaId.Valor}'");
        using var cliente = escenario.Web.CreateClient();

        await NoEncontradoAsync(cliente, Ruta(cita.EnlaceDeGestion));
    }

    [Fact]
    public async Task Descargar_no_escribe_bitacora_ni_outbox()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        var bitacora = await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora");
        var outbox = await escenario.EscalarAsync<long>("select count(*) from agenda.outbox");
        using var cliente = escenario.Web.CreateClient();

        await cliente.GetStringAsync(Ruta(cita.EnlaceDeGestion), Cancelacion);

        Assert.Equal(bitacora, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"));
        Assert.Equal(outbox, await escenario.EscalarAsync<long>("select count(*) from agenda.outbox"));
    }

    [Fact]
    public async Task Ninguna_traza_ni_registro_lleva_el_token_y_la_ruta_va_enmascarada()
    {
        var trazas = new ConcurrentQueue<Activity>();
        using var oyente = new ActivityListener
        {
            ShouldListenTo = fuente => fuente.Name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = trazas.Enqueue,
        };
        ActivitySource.AddActivityListener(oyente);

        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        var token = Token(cita.EnlaceDeGestion);
        using var cliente = escenario.Web.CreateClient();

        using (var respuesta = await cliente.GetAsync(Ruta(cita.EnlaceDeGestion), Cancelacion))
        {
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        using (var respuesta = await cliente.GetAsync($"/c/{new string('A', 43)}/calendario.ics", Cancelacion))
        {
            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }

        var delCalendario = trazas.Where(t => t.GetTagItem("url.path") is "/c/{token}/calendario.ics").ToList();
        Assert.NotEmpty(delCalendario);
        Assert.All(trazas, traza =>
        {
            Assert.DoesNotContain(token[..20], traza.DisplayName, StringComparison.Ordinal);
            Assert.DoesNotContain(token[..20], traza.OperationName, StringComparison.Ordinal);
            Assert.All(traza.TagObjects, tag => Assert.DoesNotContain(token[..20], tag.Value?.ToString() ?? "", StringComparison.Ordinal));
        });
        Assert.DoesNotContain(Arnes.Registros(escenario.Web), r => r.Mensaje.Contains(token[..20], StringComparison.Ordinal));
    }

    // AD-23: ASP.NET Core abre por petición un ámbito de registro con RequestPath (/c/<token>/calendario.ics) y escribe
    // "Request starting/finished" con la ruta. Con la categoría apagada en appsettings.json no se crea ni lo uno ni lo
    // otro, así que ningún registro de la descarga lleva el token. Las trazas siguen correlacionadas por su Activity.
    [Fact]
    public async Task Los_registros_de_hosting_estan_apagados_y_no_abren_el_ambito_con_la_ruta()
    {
        await using var web = arnes.ArrancarWeb();

        var registrador = web.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics");

        Assert.False(registrador.IsEnabled(LogLevel.Critical));
    }

    [Fact]
    public void La_consulta_no_muestra_el_token()
    {
        var token = new string('Q', 43);

        Assert.DoesNotContain(token, new ArchivoDeCalendarioPorEnlace(token).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Por_id_IAgendaApi_da_el_mismo_archivo_que_la_descarga_y_nada_si_la_Cita_esta_cancelada()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var cita = (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor;
        using var cliente = escenario.Web.CreateClient();
        var descargado = await cliente.GetStringAsync(Ruta(cita.EnlaceDeGestion), Cancelacion);

        await using (var ambito = escenario.Web.Services.CreateAsyncScope())
        {
            var archivo = await ambito.ServiceProvider.GetRequiredService<IAgendaApi>().ObtenerArchivoDeCalendarioAsync(cita.CitaId, Cancelacion);
            Assert.Equal(descargado, archivo?.Contenido);
            Assert.Null(await ambito.ServiceProvider.GetRequiredService<IAgendaApi>().ObtenerArchivoDeCalendarioAsync(CitaId.Nuevo(), Cancelacion));
        }

        await escenario.EjecutarAsync(
            $"update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'consultorio', cancelada_en = now() where id = '{cita.CitaId.Valor}'");

        await using (var ambito = escenario.Web.Services.CreateAsyncScope())
        {
            Assert.Null(await ambito.ServiceProvider.GetRequiredService<IAgendaApi>().ObtenerArchivoDeCalendarioAsync(cita.CitaId, Cancelacion));
        }
    }

    // La ruta del archivo, sin el sitio: la del Enlace de gestión más /calendario.ics.
    private static string Ruta(string enlace) => $"{new Uri(enlace).AbsolutePath}/calendario.ics";

    private static string Token(string enlace) => enlace[(enlace.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];

    private static async Task NoEncontradoAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("", await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    // Las líneas lógicas: una continuación (CRLF y un espacio) se une a la anterior (RFC 5545, 3.1).
    private static string Desplegado(string archivo) => archivo.Replace("\r\n ", "", StringComparison.Ordinal);

    private static List<string> Desplegar(string archivo) =>
        [.. Desplegado(archivo).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)];
}
