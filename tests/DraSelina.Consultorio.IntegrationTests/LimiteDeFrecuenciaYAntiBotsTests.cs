using System.Net;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.UI.Pages.Reserva;
using DraSelina.Consultorio.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La protección de la reserva (FR-7, CAP-5; entrada 11) contra la web en memoria: la verificación anti-bots de Tus
/// datos (campo trampa y tiempo mínimo protegido) y el límite de frecuencia por IP de las páginas de reserva.
/// </summary>
public sealed partial class TusDatosTests
{
    private const string AvisoGenerico = "No pudimos agendar tu cita. Inténtalo de nuevo.";

    private const string DemasiadosIntentos = "Demasiados intentos. Espera unos minutos y vuelve a intentarlo.";

    /// <summary>El encabezado con el que una prueba dice desde qué IP llega la petición.</summary>
    private const string EncabezadoDeIp = "X-Ip-De-Prueba";

    [Fact]
    public async Task El_formulario_lleva_el_instante_protegido_y_un_campo_trampa_que_una_persona_no_ve()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.ClienteDeUnaPersona();

        var html = WebUtility.HtmlDecode(await cliente.GetStringAsync(Ruta(), Cancelacion));
        var formulario = Formulario().Match(html).Value;

        Assert.False(string.IsNullOrEmpty(CamposOcultos(formulario)[VerificacionAntiBots.CampoDePintado]));
        Assert.Matches(
            $"""<div class="dsm-fuera-de-pantalla" aria-hidden="true">\s*<label[^>]*>[^<]*</label>\s*<input(?=[^>]*name="{VerificacionAntiBots.CampoTrampa}")(?=[^>]*tabindex="-1")(?=[^>]*autocomplete="off")(?![^>]*value="[^"])[^>]*>""",
            formulario);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Un_envio_humano_pasa_la_verificacion_y_reserva()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();
        var (accion, campos) = await LlenarAsync(cliente, Ruta(), Datos());

        // Justo el tiempo mínimo después de pintar el formulario.
        escenario.Reloj.Avanzar(VerificacionAntiBots.TiempoMinimo);
        var html = await PostAsync(cliente, accion, campos);

        Assert.Contains("Tu cita está confirmada", html, StringComparison.Ordinal);
        Assert.Single(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Con_el_campo_trampa_lleno_no_reserva_y_vuelve_a_Tus_datos_con_el_aviso_generico()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.ClienteDeUnaPersona();
        var datos = Datos();
        datos[VerificacionAntiBots.CampoTrampa] = "https://bots.example";

        var (_, html) = await EnviarAsync(cliente, Ruta(), datos);

        SinReservarConElAvisoGenerico(html);
        Assert.Empty(await escenario.CitasAsync());
        Assert.Empty(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task Un_envio_antes_del_tiempo_minimo_no_reserva_y_al_reintentar_despues_si()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();
        var (accion, campos) = await LlenarAsync(cliente, Ruta(), Datos());

        escenario.Reloj.Avanzar(VerificacionAntiBots.TiempoMinimo - TimeSpan.FromMilliseconds(1));
        var rapido = await PostAsync(cliente, accion, campos);

        SinReservarConElAvisoGenerico(rapido);
        Assert.Empty(await escenario.CitasAsync());
        Assert.Empty(await escenario.PacientesAsync());

        // El formulario que vuelve conserva el instante del primer pintado: una persona que reintenta sí reserva.
        var formulario = Formulario().Match(rapido).Value;
        Assert.Equal(campos[VerificacionAntiBots.CampoDePintado], CamposOcultos(formulario)[VerificacionAntiBots.CampoDePintado]);
        escenario.Reloj.Avanzar(TimeSpan.FromSeconds(1));
        var reintento = await PostAsync(cliente, accion, CamposDelFormulario(formulario));

        Assert.Contains("Tu cita está confirmada", reintento, StringComparison.Ordinal);
        Assert.Single(await escenario.CitasAsync());
    }

    [Theory]
    [InlineData("ausente")]
    [InlineData("vacio")]
    [InlineData("alterado")]
    [InlineData("inventado")]
    public async Task Con_el_instante_ausente_o_alterado_no_reserva(string caso)
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.ClienteDeUnaPersona();
        var (accion, campos) = await LlenarAsync(cliente, Ruta(), Datos());
        var original = campos[VerificacionAntiBots.CampoDePintado];
        switch (caso)
        {
            case "ausente":
                campos.Remove(VerificacionAntiBots.CampoDePintado);
                break;
            case "vacio":
                campos[VerificacionAntiBots.CampoDePintado] = "";
                break;
            case "alterado":
                var medio = original.Length / 2;
                campos[VerificacionAntiBots.CampoDePintado] =
                    original[..medio] + (original[medio] == 'A' ? 'B' : 'A') + original[(medio + 1)..];
                break;
            default:
                campos[VerificacionAntiBots.CampoDePintado] = "638000000000000000";
                break;
        }

        var html = await PostAsync(cliente, accion, campos);

        SinReservarConElAvisoGenerico(html);
        Assert.Empty(await escenario.CitasAsync());
        Assert.Empty(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task Tras_horario_tomado_elegir_otra_hora_y_confirmar_pasa_con_el_instante_original()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).EsExito);

        // Sin mover el reloj entre envíos: solo pasa si el instante es el del primer pintado.
        using var cliente = escenario.Web.CreateClient();
        var (accion, campos) = await LlenarAsync(cliente, Ruta(), Datos());
        escenario.Reloj.Avanzar(TimeSpan.FromSeconds(20));
        var tomado = await PostAsync(cliente, accion, campos);
        Assert.Contains("Ese horario se acaba de ocupar", tomado, StringComparison.Ordinal);
        Assert.Equal(campos[VerificacionAntiBots.CampoDePintado], CamposOcultos(tomado)[VerificacionAntiBots.CampoDePintado]);

        var otraHora = Horas().Matches(tomado).Select(m => m.Groups["accion"].Value).Single(a => a == Ruta("16:30"));
        var datosConHora = await PostAsync(cliente, otraHora, CamposOcultos(tomado));
        var formulario = Formulario().Match(datosConHora).Value;
        Assert.Equal(campos[VerificacionAntiBots.CampoDePintado], CamposOcultos(formulario)[VerificacionAntiBots.CampoDePintado]);

        var confirmada = await PostAsync(cliente, otraHora, CamposDelFormulario(formulario));

        Assert.Contains("Tu cita está confirmada", confirmada, StringComparison.Ordinal);
        Assert.Equal(2, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Un_instante_de_mas_de_2_horas_no_reserva_y_el_formulario_vuelve_con_uno_nuevo()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        using var cliente = escenario.Web.CreateClient();
        var (accion, campos) = await LlenarAsync(cliente, Ruta(), Datos());

        escenario.Reloj.Avanzar(VerificacionAntiBots.TiempoMaximo + TimeSpan.FromSeconds(1));
        var vencido = await PostAsync(cliente, accion, campos);

        SinReservarConElAvisoGenerico(vencido);
        Assert.Empty(await escenario.CitasAsync());
        Assert.Empty(await escenario.PacientesAsync());

        // El formulario que vuelve trae un instante nuevo: una persona que reintenta después del mínimo sí reserva.
        var formulario = Formulario().Match(vencido).Value;
        Assert.NotEqual(campos[VerificacionAntiBots.CampoDePintado], CamposOcultos(formulario)[VerificacionAntiBots.CampoDePintado]);
        escenario.Reloj.Avanzar(VerificacionAntiBots.TiempoMinimo);
        var reintento = await PostAsync(cliente, accion, CamposDelFormulario(formulario));

        Assert.Contains("Tu cita está confirmada", reintento, StringComparison.Ordinal);
        Assert.Single(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Tras_horario_tomado_confirmar_dentro_de_las_2_horas_reserva_con_el_instante_original()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(ReservaEnLineaTests.Comando())).EsExito);
        using var cliente = escenario.Web.CreateClient();
        var (accion, campos) = await LlenarAsync(cliente, Ruta(), Datos());

        escenario.Reloj.Avanzar(TimeSpan.FromHours(1));
        var tomado = await PostAsync(cliente, accion, campos);
        Assert.Contains("Ese horario se acaba de ocupar", tomado, StringComparison.Ordinal);

        var otraHora = Horas().Matches(tomado).Select(m => m.Groups["accion"].Value).Single(a => a == Ruta("16:30"));
        var datosConHora = await PostAsync(cliente, otraHora, CamposOcultos(tomado));
        var formulario = Formulario().Match(datosConHora).Value;
        Assert.Equal(campos[VerificacionAntiBots.CampoDePintado], CamposOcultos(formulario)[VerificacionAntiBots.CampoDePintado]);

        // Justo en el límite de 2 horas desde el primer pintado.
        escenario.Reloj.Avanzar(VerificacionAntiBots.TiempoMaximo - TimeSpan.FromHours(1));
        var confirmada = await PostAsync(cliente, otraHora, CamposDelFormulario(formulario));

        Assert.Contains("Tu cita está confirmada", confirmada, StringComparison.Ordinal);
        Assert.Equal(2, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Los_limites_de_fabrica_son_10_envios_cada_10_minutos_y_60_visitas_por_minuto()
    {
        await using var web = arnes.ArrancarWeb();

        var opciones = web.Services.GetRequiredService<IOptions<OpcionesDeLimiteDeFrecuencia>>().Value;

        Assert.Equal(10, opciones.Envios.Permitidas);
        Assert.Equal(TimeSpan.FromMinutes(10), opciones.Envios.Duracion);
        Assert.Equal(60, opciones.Visitas.Permitidas);
        Assert.Equal(TimeSpan.FromMinutes(1), opciones.Visitas.Duracion);
    }

    [Fact]
    public async Task Pasado_el_limite_de_envios_responde_429_amable_sin_Cita_y_otra_IP_se_atiende()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await using var web = ConIpDePrueba(escenario.Web, envios: 1, visitas: 60);
        using var cliente = escenario.ClienteDeUnaPersona(web);
        cliente.DefaultRequestHeaders.Add(EncabezadoDeIp, "203.0.113.10");

        var (_, primera) = await EnviarAsync(cliente, Ruta(), Datos());
        Assert.Contains("Tu cita está confirmada", primera, StringComparison.Ordinal);

        var (accion, campos) = await LlenarAsync(cliente, Ruta("16:30"), Datos());
        using var segunda = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);

        await PaginaAmableAsync(segunda);
        Assert.Single(await escenario.CitasAsync());

        // Otra IP no gasta el límite de la primera.
        using var otro = escenario.ClienteDeUnaPersona(web);
        otro.DefaultRequestHeaders.Add(EncabezadoDeIp, "203.0.113.20");
        var (_, deOtraIp) = await EnviarAsync(otro, Ruta("16:30"), Datos());
        Assert.Contains("Tu cita está confirmada", deOtraIp, StringComparison.Ordinal);
        Assert.Equal(2, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Pasado_el_limite_de_visitas_responde_429_amable_en_las_paginas_de_reserva_y_otra_IP_se_atiende()
    {
        await using var escenario = await ReservaEnLineaTests.Escenario.CrearAsync(arnes);
        await using var web = ConIpDePrueba(escenario.Web, envios: 10, visitas: 3);
        using var cliente = web.CreateClient();
        cliente.DefaultRequestHeaders.Add(EncabezadoDeIp, "203.0.113.30");

        foreach (var ruta in new[] { "/reservar", $"/reservar/horario?tipo={TiposDeVisitaSembrados.PrimeraConsulta}", Ruta() })
        {
            using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        foreach (var ruta in new[] { "/reservar", $"/reservar/horario?tipo={TiposDeVisitaSembrados.PrimeraConsulta}", Ruta() })
        {
            using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
            await PaginaAmableAsync(respuesta);
        }

        // Lo que no es reserva no lleva el límite: el inicio, el aviso, la salud y las hojas de estilo.
        foreach (var ruta in new[] { "/", "/aviso-de-privacidad", "/health", "/css/consultorio.css" })
        {
            using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        using var otro = web.CreateClient();
        otro.DefaultRequestHeaders.Add(EncabezadoDeIp, "203.0.113.40");
        using var deOtraIp = await otro.GetAsync("/reservar", Cancelacion);
        Assert.Equal(HttpStatusCode.OK, deOtraIp.StatusCode);
    }

    private static void SinReservarConElAvisoGenerico(string html)
    {
        Assert.Contains("Tus datos", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Tu cita está confirmada", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);

        // Un solo aviso, el genérico: no dice por qué.
        var aviso = Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, """<p class="dsm-aviso__mensaje">\s*(?<texto>.*?)\s*</p>""",
            System.Text.RegularExpressions.RegexOptions.Singleline));
        Assert.Equal(AvisoGenerico, aviso.Groups["texto"].Value);

        // Lo escrito se conserva.
        Assert.Contains("value=\"Ana Sofía López\"", html, StringComparison.Ordinal);
    }

    private static async Task PaginaAmableAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
        Assert.Contains(DemasiadosIntentos, html, StringComparison.Ordinal);
        Assert.Contains("Dra. Selina Medina Castillo", html, StringComparison.Ordinal);
        Assert.Contains("css/consultorio", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// La web del escenario con límites bajos y la IP de cada petición tomada de <see cref="EncabezadoDeIp"/>: en
    /// memoria no hay conexión real.
    /// </summary>
    private static WebApplicationFactory<Program> ConIpDePrueba(WebApplicationFactory<Program> web, int envios, int visitas) =>
        web.WithWebHostBuilder(b =>
        {
            b.UseSetting("LimiteDeFrecuencia:Envios:Permitidas", envios.ToString(System.Globalization.CultureInfo.InvariantCulture));
            b.UseSetting("LimiteDeFrecuencia:Visitas:Permitidas", visitas.ToString(System.Globalization.CultureInfo.InvariantCulture));
            b.ConfigureServices(s => s.AddSingleton<IStartupFilter, IpDePrueba>());
        });

    private sealed class IpDePrueba : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
        {
            app.Use((contexto, resto) =>
            {
                if (contexto.Request.Headers.TryGetValue(EncabezadoDeIp, out var ip))
                {
                    contexto.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                }

                return resto(contexto);
            });
            siguiente(app);
        };
    }
}
