using System.Security.Cryptography;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// El Enlace de gestión en la confirmación (FR-5, AD-19, RN-22): el token derivado de la clave del host y del id, del
/// que la Cita guarda solo el hash, y que solo arma <see cref="IAgendaApi"/>. Nunca en registros, bitácora ni outbox
/// (AD-23).
/// </summary>
public sealed class EnlaceDeGestionTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Una_reserva_valida_confirma_con_el_enlace_y_la_Cita_guarda_solo_el_hash_del_token()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);

        var resultado = await escenario.AgendarAsync(ReservaEnLineaTests.Comando());

        Assert.True(resultado.EsExito);
        var enlace = resultado.Valor.EnlaceDeGestion;
        Assert.Matches($"^{Arnes.UrlBaseDeEnlace}/c/[A-Za-z0-9_-]{{43}}$", enlace);
        var token = Token(enlace);

        var hash = await escenario.EscalarAsync<byte[]>($"select token_hash from agenda.cita where id = '{resultado.Valor.CitaId}'");
        Assert.Equal(SHA256.HashData(Bytes(token)), hash);

        // El token no aparece en ninguna columna, ni en texto ni en bytes.
        var fila = await escenario.EscalarAsync<string>("select row_to_json(c)::text from agenda.cita c");
        Assert.DoesNotContain(token, fila, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexStringLower(Bytes(token)), fila, StringComparison.Ordinal);
        Assert.Equal(0, await escenario.EscalarAsync<long>(
            $"select count(*) from agenda.cita where token_hash = '\\x{Convert.ToHexStringLower(Bytes(token))}'::bytea"));
    }

    [Fact]
    public async Task ObtenerEnlaceDeGestion_da_el_mismo_enlace_cada_vez_y_el_de_la_confirmacion()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var resultado = await escenario.AgendarAsync(ReservaEnLineaTests.Comando());
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var api = ambito.ServiceProvider.GetRequiredService<IAgendaApi>();

        var primero = api.ObtenerEnlaceDeGestion(resultado.Valor.CitaId);
        var segundo = api.ObtenerEnlaceDeGestion(resultado.Valor.CitaId);

        Assert.Equal(primero, segundo);
        Assert.Equal(resultado.Valor.EnlaceDeGestion, primero);
    }

    [Fact]
    public async Task Dos_Citas_tienen_enlaces_y_hashes_distintos()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);

        var una = await escenario.AgendarAsync(ReservaEnLineaTests.Comando(hora: new TimeOnly(16, 0)));
        var otra = await escenario.AgendarAsync(ReservaEnLineaTests.Comando(hora: new TimeOnly(16, 30)));

        Assert.True(una.EsExito && otra.EsExito);
        Assert.NotEqual(una.Valor.EnlaceDeGestion, otra.Valor.EnlaceDeGestion);
        Assert.Equal(2, await escenario.EscalarAsync<long>("select count(distinct token_hash) from agenda.cita"));
    }

    [Fact]
    public void La_misma_Cita_con_otra_clave_da_otro_enlace()
    {
        var cita = CitaId.Nuevo();

        var conLaClave = Api(Arnes.ClaveDeEnlace).ObtenerEnlaceDeGestion(cita);
        var conOtra = Api("otra-clave-del-host-de-al-menos-32-bytes").ObtenerEnlaceDeGestion(cita);

        Assert.NotEqual(conLaClave, conOtra);
        Assert.Equal(conLaClave, Api(Arnes.ClaveDeEnlace).ObtenerEnlaceDeGestion(cita));
    }

    [Fact]
    public async Task Confirmar_dos_veces_la_misma_solicitud_da_el_mismo_enlace()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var comando = ReservaEnLineaTests.Comando();

        var primera = await escenario.AgendarAsync(comando);
        var segunda = await escenario.AgendarAsync(ReservaEnLineaTests.Comando(solicitud: comando.SolicitudId));

        Assert.True(primera.EsExito && segunda.EsExito);
        Assert.Equal(primera.Valor, segunda.Valor);
        Assert.Equal(primera.Valor.EnlaceDeGestion, segunda.Valor.EnlaceDeGestion);
    }

    [Fact]
    public async Task La_confirmacion_lleva_el_umbral_de_reprogramacion_de_las_reglas()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        Assert.Equal(120, (await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).Valor.UmbralDeReprogramacionMin);

        await escenario.EjecutarAsync("update agenda.reglas_de_reserva set umbral_cancelacion_tardia_min = 60");

        Assert.Equal(60, (await escenario.AgendarAsync(ReservaEnLineaTests.Comando(hora: new TimeOnly(17, 0)))).Valor.UmbralDeReprogramacionMin);
    }

    [Fact]
    public async Task Ni_los_registros_ni_la_bitacora_ni_el_outbox_llevan_el_token()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        var comando = ReservaEnLineaTests.Comando();
        var resultado = await escenario.AgendarAsync(comando);
        await escenario.AgendarAsync(ReservaEnLineaTests.Comando(solicitud: comando.SolicitudId));
        await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion);
        var token = Token(resultado.Valor.EnlaceDeGestion);

        Assert.DoesNotContain(Arnes.Registros(escenario.Web), r => r.Mensaje.Contains(token, StringComparison.Ordinal));
        Assert.DoesNotContain(
            token,
            await escenario.EscalarAsync<string>("select coalesce(string_agg(row_to_json(b)::text, ''), '') from agenda.bitacora b"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            token,
            await escenario.EscalarAsync<string>("select coalesce(string_agg(row_to_json(o)::text, ''), '') from agenda.outbox o"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(token, resultado.Valor.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_la_clave_la_web_no_arranca_y_dice_que_configuracion_falta()
    {
        using var web = arnes.ArrancarWebSinClaveDeEnlace();

        var error = Record.Exception(() => web.CreateClient());

        Assert.NotNull(error);
        Assert.Contains("EnlaceDeGestion:Clave", Mensajes(error), StringComparison.Ordinal);
    }

    // Solo para armar enlaces: el archivo de calendario y la Agenda del día, que leen la base, no se piden aquí.
    private static AgendaApi Api(string clave) =>
        new(Options.Create(new OpcionesDeEnlaceDeGestion { Clave = clave, UrlBase = Arnes.UrlBaseDeEnlace }), null!, null!, null!, null!, null!);

    private static string Token(string enlace) => enlace[(enlace.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];

    private static byte[] Bytes(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }

    private static string Mensajes(Exception error) =>
        error is AggregateException agregada
            ? string.Join(" | ", agregada.InnerExceptions.Select(Mensajes))
            : error.Message + (error.InnerException is { } interna ? " | " + Mensajes(interna) : "");
}
