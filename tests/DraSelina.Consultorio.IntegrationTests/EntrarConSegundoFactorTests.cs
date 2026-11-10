using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DraSelina.Consultorio.Web;
using DraSelina.Consultorio.Web.Identidad;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La administración tras Identity con segundo factor (CAP-9, AD-22, entrada 5.1), por HTTP contra la web real del
/// arnés: redirecciones, registro inexistente, encabezados de /admin, y el recorrido completo contraseña → enrolamiento
/// con el código TOTP calculado aquí → Agenda; después contraseña → código, y contraseña → código de recuperación. Las
/// pruebas que cambian la cuenta (activan el segundo factor, la bloquean) usan su propia base: la cuenta sembrada es
/// una por base.
/// </summary>
public sealed partial class EntrarConSegundoFactorTests(Arnes arnes)
{
    private const string Robots = """<meta name="robots" content="noindex, nofollow">""";

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sin_sesion_admin_lleva_a_entrar_con_la_direccion_de_regreso_y_sin_indice()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = Cliente(web);

        using var respuesta = await cliente.GetAsync("/admin", Cancelacion);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/admin/entrar?ReturnUrl=%2Fadmin", Ruta(respuesta.Headers.Location));
        SinIndiceNiCache(respuesta);
    }

    [Theory]
    [InlineData("/Account/Register")]
    [InlineData("/Account/Login")]
    [InlineData("/admin/registro")]
    [InlineData("/Identity/Account/Register")]
    public async Task No_hay_registro_publico_ni_paginas_de_Identity_de_plantilla(string ruta)
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = Cliente(web);

        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);

        // /admin/registro no existe; sin sesión, cualquier ruta /admin/... sin página sigue siendo 404.
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Entrar_es_SSR_sin_indice_ni_cache_y_el_sitio_publico_no_lleva_blazor_web_js()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = Cliente(web);

        using var entrar = await cliente.GetAsync(Registro.RutaDeEntrar, Cancelacion);
        var html = await entrar.Content.ReadAsStringAsync(Cancelacion);
        Assert.Equal(HttpStatusCode.OK, entrar.StatusCode);
        SinIndiceNiCache(entrar);
        Assert.Contains(Robots, html, StringComparison.Ordinal);
        Assert.Contains("type=\"password\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__RequestVerificationToken", html, StringComparison.Ordinal);

        using var inicio = await cliente.GetAsync("/", Cancelacion);
        var publico = await inicio.Content.ReadAsStringAsync(Cancelacion);
        Assert.Equal(HttpStatusCode.OK, inicio.StatusCode);
        Assert.DoesNotContain("blazor.web.js", publico, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", publico, StringComparison.OrdinalIgnoreCase);
        Assert.False(inicio.Headers.Contains("X-Robots-Tag"));
    }

    [Fact]
    public async Task La_primera_vez_la_contrasena_lleva_al_enrolamiento_y_con_el_codigo_entra_a_la_Agenda_con_sus_codigos()
    {
        await using var escenario = await BaseYWebAsync();
        using var cliente = Cliente(escenario.Web);

        // Contraseña correcta, sin segundo factor: no hay sesión, solo el enrolamiento.
        var activar = await EntrarConContrasenaAsync(cliente);
        Assert.Equal($"{Registro.RutaDeActivar}?ReturnUrl=%2Fadmin", activar);
        using (var sinSesion = await cliente.GetAsync("/admin", Cancelacion))
        {
            Assert.Equal(HttpStatusCode.Redirect, sinSesion.StatusCode);
            Assert.StartsWith(Registro.RutaDeEntrar, Ruta(sinSesion.Headers.Location)!, StringComparison.Ordinal);
        }

        // El QR (SVG del servidor, sin scripts) y la clave en texto.
        var (pagina, clave) = await AbrirEnrolamientoAsync(cliente, activar);
        Assert.Contains("<svg", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", pagina, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Robots, pagina, StringComparison.Ordinal);

        // Un código mal escrito no activa nada.
        var mal = await EnviarAsync(cliente, activar, pagina, "activar", ("Datos.Codigo", CodigoQueNoEs(clave)));
        Assert.Equal(HttpStatusCode.OK, mal.Estado);
        Assert.True(Dice(mal.Html, ActivarSegundoFactor.Incorrecto));
        Assert.False(await SegundoFactorActivoAsync(escenario.Cadena));

        // El código de ahora lo activa, muestra los códigos de recuperación e inicia la sesión.
        var bien = await EnviarAsync(cliente, activar, mal.Html, "activar", ("Datos.Codigo", Totp(clave)));
        Assert.Equal(HttpStatusCode.OK, bien.Estado);
        Assert.Equal(10, CodigosDeRecuperacion(bien.Html).Count);
        Assert.True(await SegundoFactorActivoAsync(escenario.Cadena));

        using var agenda = await cliente.GetAsync("/admin", Cancelacion);
        var html = WebUtility.HtmlDecode(await agenda.Content.ReadAsStringAsync(Cancelacion));
        Assert.Equal(HttpStatusCode.OK, agenda.StatusCode);
        SinIndiceNiCache(agenda);
        Assert.Contains(Robots, html, StringComparison.Ordinal);
        Assert.Contains(">Agenda</h1>", html, StringComparison.Ordinal);
        Assert.Contains("No hay citas este día.", html, StringComparison.Ordinal);
        Assert.Matches("""<script src="_framework/blazor\.web(\.[a-z0-9]+)?\.js"></script>""", html);
        Assert.Contains("action=\"/admin/salir\"", html, StringComparison.Ordinal);

        // El enrolamiento ya no se vuelve a mostrar.
        using var otraVez = await cliente.GetAsync(Registro.RutaDeActivar, Cancelacion);
        Assert.Equal(HttpStatusCode.Redirect, otraVez.StatusCode);
    }

    [Fact]
    public async Task Con_el_segundo_factor_activo_la_contrasena_pide_el_codigo_no_el_QR_y_con_el_codigo_entra()
    {
        await using var escenario = await BaseYWebAsync();
        var clave = await ActivarAsync(escenario.Web);

        // Otro navegador, otra web (como tras reiniciarla): contraseña y código.
        await using var otraWeb = arnes.ArrancarWeb(escenario.Cadena);
        using var cliente = Cliente(otraWeb);
        var segundoFactor = await EntrarConContrasenaAsync(cliente);
        Assert.Equal($"{Registro.RutaDelSegundoFactor}?ReturnUrl=%2Fadmin", segundoFactor);

        using (var sinCodigo = await cliente.GetAsync("/admin", Cancelacion))
        {
            Assert.Equal(HttpStatusCode.Redirect, sinCodigo.StatusCode);
        }

        // El enrolamiento no se abre: el QR no se vuelve a mostrar.
        using (var activar = await cliente.GetAsync(Registro.RutaDeActivar, Cancelacion))
        {
            Assert.Equal(HttpStatusCode.Redirect, activar.StatusCode);
            Assert.StartsWith(Registro.RutaDelSegundoFactor, Ruta(activar.Headers.Location)!, StringComparison.Ordinal);
        }

        var pagina = await LeerAsync(cliente, segundoFactor);
        Assert.DoesNotContain("<svg", pagina, StringComparison.Ordinal);

        var mal = await EnviarAsync(cliente, segundoFactor, pagina, "segundo-factor", ("Datos.Codigo", CodigoQueNoEs(clave)));
        Assert.True(Dice(mal.Html, SegundoFactor.Incorrecto));

        var bien = await EnviarAsync(cliente, segundoFactor, mal.Html, "segundo-factor", ("Datos.Codigo", Totp(clave, pasos: 1)));
        Assert.Equal(HttpStatusCode.Redirect, bien.Estado);
        Assert.Equal("/admin", bien.Destino);

        using var agenda = await cliente.GetAsync("/admin", Cancelacion);
        Assert.Equal(HttpStatusCode.OK, agenda.StatusCode);

        // Salir cierra la sesión.
        var html = await agenda.Content.ReadAsStringAsync(Cancelacion);
        var salir = await EnviarAFormularioAsync(cliente, Registro.RutaDeSalir, html, accion: Registro.RutaDeSalir);
        Assert.Equal(HttpStatusCode.Redirect, salir.Estado);
        using var tras = await cliente.GetAsync("/admin", Cancelacion);
        Assert.Equal(HttpStatusCode.Redirect, tras.StatusCode);
    }

    [Fact]
    public async Task Un_codigo_de_recuperacion_entra_una_sola_vez()
    {
        await using var escenario = await BaseYWebAsync();
        using var primero = Cliente(escenario.Web);
        var activar = await EntrarConContrasenaAsync(primero);
        var (pagina, clave) = await AbrirEnrolamientoAsync(primero, activar);
        var activada = await EnviarAsync(primero, activar, pagina, "activar", ("Datos.Codigo", Totp(clave)));
        var codigo = CodigosDeRecuperacion(activada.Html)[0];

        using var segundo = Cliente(escenario.Web);
        Assert.True(await EntrarConRecuperacionAsync(segundo, codigo));
        using (var agenda = await segundo.GetAsync("/admin", Cancelacion))
        {
            Assert.Equal(HttpStatusCode.OK, agenda.StatusCode);
        }

        using var tercero = Cliente(escenario.Web);
        Assert.False(await EntrarConRecuperacionAsync(tercero, codigo));
        using var sinSesion = await tercero.GetAsync("/admin", Cancelacion);
        Assert.Equal(HttpStatusCode.Redirect, sinSesion.StatusCode);
    }

    [Fact]
    public async Task Una_contrasena_mal_dice_lo_mismo_que_un_correo_que_no_existe_y_tras_el_limite_bloquea()
    {
        await using var escenario = await BaseYWebAsync();
        using var cliente = Cliente(escenario.Web);

        var otroCorreo = await EnviarEntrarAsync(cliente, "nadie@consultorio.prueba", Arnes.ContrasenaDelAdministrador);
        Assert.Equal(HttpStatusCode.OK, otroCorreo.Estado);
        Assert.True(Dice(otroCorreo.Html, Entrar.Incorrecto));

        // Los valores de Identity por omisión: 5 intentos.
        for (var intento = 1; intento <= 4; intento++)
        {
            var mal = await EnviarEntrarAsync(cliente, Arnes.CorreoDelAdministrador, "No-es-1");
            Assert.True(Dice(mal.Html, Entrar.Incorrecto));
        }

        var quinto = await EnviarEntrarAsync(cliente, Arnes.CorreoDelAdministrador, "No-es-1");
        Assert.True(Dice(quinto.Html, Entrar.Bloqueada));

        // Bloqueada, ni la contraseña correcta pasa.
        var correcta = await EnviarEntrarAsync(cliente, Arnes.CorreoDelAdministrador, Arnes.ContrasenaDelAdministrador);
        Assert.Equal(HttpStatusCode.OK, correcta.Estado);
        Assert.True(Dice(correcta.Html, Entrar.Bloqueada));
    }

    [Fact]
    public async Task Sin_cookie_parcial_las_paginas_del_segundo_factor_llevan_a_entrar()
    {
        await using var web = arnes.ArrancarWeb();
        using var cliente = Cliente(web);

        foreach (var ruta in new[] { Registro.RutaDelSegundoFactor, Registro.RutaDeActivar, Registro.RutaDeRecuperacion })
        {
            using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
            Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            Assert.Equal(Registro.RutaDeEntrar, Ruta(respuesta.Headers.Location)!);
            SinIndiceNiCache(respuesta);
        }
    }

    [Fact]
    public async Task Sin_segundo_factor_activo_la_cookie_parcial_no_abre_el_codigo_ni_la_recuperacion()
    {
        await using var escenario = await BaseYWebAsync();
        using var cliente = Cliente(escenario.Web);
        await EntrarConContrasenaAsync(cliente);

        using (var codigo = await cliente.GetAsync(Registro.RutaDelSegundoFactor, Cancelacion))
        {
            Assert.Equal(HttpStatusCode.Redirect, codigo.StatusCode);
            Assert.StartsWith(Registro.RutaDeActivar, Ruta(codigo.Headers.Location)!, StringComparison.Ordinal);
        }

        using var recuperacion = await cliente.GetAsync(Registro.RutaDeRecuperacion, Cancelacion);
        Assert.Equal(HttpStatusCode.Redirect, recuperacion.StatusCode);
        Assert.Equal(Registro.RutaDeEntrar, Ruta(recuperacion.Headers.Location)!);
    }

    [Fact]
    public async Task La_siembra_es_idempotente_y_no_toca_la_cuenta_que_ya_existe()
    {
        var cadena = await arnes.CrearBaseMigradaAsync(Cancelacion);
        var antes = await CuentasAsync(cadena);

        await MigrationService.EjecutarAsync(cadena, "Otra-contraseña-2", Cancelacion);

        var despues = await CuentasAsync(cadena);
        var cuenta = Assert.Single(despues);
        Assert.Equal(Assert.Single(antes), cuenta);
        Assert.Equal(Arnes.CorreoDelAdministrador, cuenta.Correo);
        NpgsqlConnection.ClearPool(new NpgsqlConnection(cadena));
    }

    [Fact]
    public async Task Con_otro_correo_la_siembra_no_crea_una_segunda_cuenta()
    {
        var cadena = await arnes.CrearBaseMigradaAsync(Cancelacion);
        var antes = await CuentasAsync(cadena);

        await MigrationService.EjecutarAsync(cadena, "Otra-contraseña-2", "otro@consultorio.prueba", Cancelacion);

        Assert.Equal(Assert.Single(antes), Assert.Single(await CuentasAsync(cadena)));
        NpgsqlConnection.ClearPool(new NpgsqlConnection(cadena));
    }

    [Fact]
    public async Task Sin_sesion_la_semana_pedida_se_conserva_al_entrar_con_contrasena_y_codigo()
    {
        await using var escenario = await BaseYWebAsync();
        var clave = await ActivarAsync(escenario.Web);
        using var cliente = Cliente(escenario.Web);
        const string Semana = "/admin?fecha=2026-11-10&vista=semana";

        using var sinSesion = await cliente.GetAsync(Semana, Cancelacion);
        Assert.Equal(HttpStatusCode.Redirect, sinSesion.StatusCode);
        var entrar = Ruta(sinSesion.Headers.Location)!;
        Assert.Equal($"{Registro.RutaDeEntrar}?ReturnUrl={Uri.EscapeDataString(Semana)}", entrar);

        var pagina = await LeerAsync(cliente, entrar);
        var conContrasena = await EnviarAsync(
            cliente, entrar, pagina, "entrar",
            ("Datos.Correo", Arnes.CorreoDelAdministrador), ("Datos.Contrasena", Arnes.ContrasenaDelAdministrador));
        Assert.Equal(HttpStatusCode.Redirect, conContrasena.Estado);
        var segundoFactor = conContrasena.Destino!;
        Assert.StartsWith(Registro.RutaDelSegundoFactor, segundoFactor, StringComparison.Ordinal);

        var conCodigo = await EnviarAsync(
            cliente, segundoFactor, await LeerAsync(cliente, segundoFactor), "segundo-factor", ("Datos.Codigo", Totp(clave, pasos: 1)));

        Assert.Equal(HttpStatusCode.Redirect, conCodigo.Estado);
        Assert.Equal(Semana, conCodigo.Destino);
    }

    [Theory]
    [InlineData("/admin", "/admin")]
    [InlineData("/admin?fecha=2026-11-10", "/admin?fecha=2026-11-10")]
    [InlineData("/admin?fecha=2026-11-10&vista=semana", "/admin?fecha=2026-11-10&vista=semana")]
    [InlineData("/admin/pacientes", "/admin/pacientes")]
    [InlineData("/admin/entrar", "/admin")]
    [InlineData("/administrador", "/admin")]
    [InlineData("//evil.example/admin", "/admin")]
    [InlineData("/admin?x=//evil.example", "/admin")]
    [InlineData("https://evil.example/admin", "/admin")]
    [InlineData(null, "/admin")]
    public void El_regreso_solo_lleva_a_la_administracion_del_propio_sitio(string? regreso, string esperado)
    {
        Assert.Equal(esperado, Registro.RegresoSeguro(regreso));
    }

    [Fact]
    public async Task Sin_contrasena_inicial_la_siembra_falla_con_un_mensaje_claro()
    {
        var cadena = await arnes.CrearBaseVaciaAsync(Cancelacion);

        var error = await Assert.ThrowsAsync<MigracionFallidaException>(
            () => MigrationService.EjecutarAsync(cadena, "", Cancelacion));

        Assert.Equal(1, error.CodigoDeSalida);
        Assert.Contains("Falta la contraseña inicial del Administrador", error.Salida, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_contrasena_inicial_que_no_cumple_la_politica_falla_sin_mostrarla()
    {
        var cadena = await arnes.CrearBaseVaciaAsync(Cancelacion);

        var error = await Assert.ThrowsAsync<MigracionFallidaException>(
            () => MigrationService.EjecutarAsync(cadena, "corta", Cancelacion));

        Assert.Equal(1, error.CodigoDeSalida);
        Assert.Contains("no cumplen la política de Identity", error.Salida, StringComparison.Ordinal);
        Assert.DoesNotContain("corta", error.Salida, StringComparison.Ordinal);
        Assert.DoesNotContain(Arnes.CorreoDelAdministrador, error.Salida, StringComparison.Ordinal);
    }

    // ---- Recorridos ----

    /// <summary>Una base migrada propia, con su cuenta sin segundo factor, y la web sobre ella.</summary>
    private async Task<BaseYWeb> BaseYWebAsync()
    {
        var cadena = await arnes.CrearBaseMigradaAsync(Cancelacion);
        return new BaseYWeb(cadena, arnes.ArrancarWeb(cadena));
    }

    private sealed class BaseYWeb(string cadena, WebApplicationFactory<Program> web) : IAsyncDisposable
    {
        public string Cadena { get; } = cadena;

        public WebApplicationFactory<Program> Web { get; } = web;

        public async ValueTask DisposeAsync()
        {
            await Web.DisposeAsync();
            NpgsqlConnection.ClearPool(new NpgsqlConnection(Cadena));
        }
    }

    /// <summary>Un navegador: guarda cookies y no sigue redirecciones, para verlas.</summary>
    internal static HttpClient Cliente(WebApplicationFactory<Program> web) =>
        web.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>Activa el segundo factor de la cuenta con un navegador propio y devuelve su clave.</summary>
    internal static async Task<string> ActivarAsync(WebApplicationFactory<Program> web)
    {
        using var cliente = Cliente(web);
        var activar = await EntrarConContrasenaAsync(cliente);
        var (pagina, clave) = await AbrirEnrolamientoAsync(cliente, activar);
        var activada = await EnviarAsync(cliente, activar, pagina, "activar", ("Datos.Codigo", Totp(clave)));
        Assert.Equal(10, CodigosDeRecuperacion(activada.Html).Count);
        return clave;
    }

    /// <summary>Entrar con el correo y la contraseña del arnés. Devuelve adónde lleva (activar o el código).</summary>
    internal static async Task<string> EntrarConContrasenaAsync(HttpClient cliente)
    {
        var enviado = await EnviarEntrarAsync(cliente, Arnes.CorreoDelAdministrador, Arnes.ContrasenaDelAdministrador);
        Assert.True(enviado.Estado == HttpStatusCode.Redirect, $"Entrar no redirigió: {enviado.Estado}");
        return enviado.Destino!;
    }

    private static async Task<Enviado> EnviarEntrarAsync(HttpClient cliente, string correo, string contrasena)
    {
        var ruta = $"{Registro.RutaDeEntrar}?ReturnUrl=%2Fadmin";
        var pagina = await LeerAsync(cliente, ruta);
        return await EnviarAsync(cliente, ruta, pagina, "entrar", ("Datos.Correo", correo), ("Datos.Contrasena", contrasena));
    }

    private static async Task<(string Pagina, string Clave)> AbrirEnrolamientoAsync(HttpClient cliente, string activar)
    {
        var pagina = await LeerAsync(cliente, activar);
        var clave = ClaveDeLaPagina().Match(pagina);
        Assert.True(clave.Success, "La página de enrolamiento no muestra la clave.");
        return (pagina, clave.Groups["clave"].Value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant());
    }

    private static async Task<bool> EntrarConRecuperacionAsync(HttpClient cliente, string codigo)
    {
        var segundoFactor = await EntrarConContrasenaAsync(cliente);
        Assert.StartsWith(Registro.RutaDelSegundoFactor, segundoFactor, StringComparison.Ordinal);
        var ruta = $"{Registro.RutaDeRecuperacion}?ReturnUrl=%2Fadmin";
        var pagina = await LeerAsync(cliente, ruta);
        var enviado = await EnviarAsync(cliente, ruta, pagina, "recuperacion", ("Datos.Codigo", codigo));
        return enviado.Estado == HttpStatusCode.Redirect && enviado.Destino == "/admin";
    }

    private static async Task<string> LeerAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {ruta}: {respuesta.StatusCode}");
        return await respuesta.Content.ReadAsStringAsync(Cancelacion);
    }

    private sealed record Enviado(HttpStatusCode Estado, string? Destino, string Html);

    /// <summary>Envía el formulario <paramref name="nombre"/> de la página, con su antiforgery y los campos dados.</summary>
    private static async Task<Enviado> EnviarAsync(
        HttpClient cliente, string ruta, string pagina, string nombre, params (string Campo, string Valor)[] campos)
    {
        var formulario = Formularios().Matches(pagina)
            .Select(m => m.Value)
            .SingleOrDefault(f => f.Contains($"name=\"_handler\" value=\"{nombre}\"", StringComparison.Ordinal));
        Assert.True(formulario is not null, $"La página no tiene el formulario '{nombre}'.");

        var valores = Ocultos().Matches(formulario).ToDictionary(m => m.Groups["nombre"].Value, m => WebUtility.HtmlDecode(m.Groups["valor"].Value));
        foreach (var (campo, valor) in campos)
        {
            valores[campo] = valor;
        }

        using var respuesta = await cliente.PostAsync(ruta, new FormUrlEncodedContent(valores), Cancelacion);
        return new Enviado(
            respuesta.StatusCode,
            Ruta(respuesta.Headers.Location),
            await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    // El texto de la página, sin entidades HTML: los mensajes se comparan tal cual.
    private static bool Dice(string html, string mensaje) =>
        WebUtility.HtmlDecode(html).Contains(mensaje, StringComparison.Ordinal);

    /// <summary>Envía el formulario de la página cuyo <c>action</c> es <paramref name="accion"/>.</summary>
    private static async Task<Enviado> EnviarAFormularioAsync(HttpClient cliente, string ruta, string pagina, string accion)
    {
        var formulario = Formularios().Matches(pagina)
            .Select(m => m.Value)
            .Single(f => f.Contains($"action=\"{accion}\"", StringComparison.Ordinal));
        var valores = Ocultos().Matches(formulario).ToDictionary(m => m.Groups["nombre"].Value, m => WebUtility.HtmlDecode(m.Groups["valor"].Value));

        using var respuesta = await cliente.PostAsync(ruta, new FormUrlEncodedContent(valores), Cancelacion);
        return new Enviado(respuesta.StatusCode, Ruta(respuesta.Headers.Location), await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    // La ruta de una redirección, absoluta o relativa.
    private static string? Ruta(Uri? ubicacion) =>
        ubicacion is null ? null : ubicacion.IsAbsoluteUri ? ubicacion.PathAndQuery : ubicacion.OriginalString;

    private static List<string> CodigosDeRecuperacion(string html) =>
        [.. CodigoDeRecuperacion().Matches(html).Select(m => WebUtility.HtmlDecode(m.Groups["codigo"].Value))];

    private static void SinIndiceNiCache(HttpResponseMessage respuesta)
    {
        Assert.Equal("noindex, nofollow", string.Join(", ", respuesta.Headers.GetValues("X-Robots-Tag")));
        Assert.True(respuesta.Headers.CacheControl?.NoStore, "Falta Cache-Control: no-store.");
    }

    private static async Task<bool> SegundoFactorActivoAsync(string cadena)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var comando = new NpgsqlCommand("select \"TwoFactorEnabled\" from identidad.\"AspNetUsers\"", conexion);
        return (bool)(await comando.ExecuteScalarAsync(Cancelacion))!;
    }

    private sealed record Cuenta(string Id, string Correo, string Hash, string Sello);

    private static async Task<List<Cuenta>> CuentasAsync(string cadena)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var comando = new NpgsqlCommand(
            "select \"Id\", \"Email\", \"PasswordHash\", \"SecurityStamp\" from identidad.\"AspNetUsers\"", conexion);
        await using var lector = await comando.ExecuteReaderAsync(Cancelacion);
        var cuentas = new List<Cuenta>();
        while (await lector.ReadAsync(Cancelacion))
        {
            cuentas.Add(new Cuenta(lector.GetString(0), lector.GetString(1), lector.GetString(2), lector.GetString(3)));
        }

        return cuentas;
    }

    // ---- TOTP (RFC 6238), como lo calcula la app autenticadora ----

    /// <summary>
    /// El código de 6 dígitos de la clave en Base32 para el paso de 30 segundos de ahora, desplazado
    /// <paramref name="pasos"/> pasos (Identity acepta dos antes y dos después).
    /// </summary>
    internal static string Totp(string claveBase32, int pasos = 0)
    {
        var paso = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + pasos;
        var contador = BitConverter.GetBytes(paso);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(contador);
        }

        var hash = HMACSHA1.HashData(DesdeBase32(claveBase32), contador);
        var desplazamiento = hash[^1] & 0x0F;
        var binario = ((hash[desplazamiento] & 0x7F) << 24)
                      | (hash[desplazamiento + 1] << 16)
                      | (hash[desplazamiento + 2] << 8)
                      | hash[desplazamiento + 3];
        return (binario % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    // Un código que no vale en ningún paso de la ventana que Identity acepta.
    private static string CodigoQueNoEs(string clave)
    {
        var validos = Enumerable.Range(-3, 7).Select(p => Totp(clave, p)).ToHashSet();
        return Enumerable.Range(0, 1_000_000).Select(n => n.ToString("D6", System.Globalization.CultureInfo.InvariantCulture))
            .First(c => !validos.Contains(c));
    }

    private static byte[] DesdeBase32(string texto)
    {
        const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int bufer = 0, bits = 0;
        foreach (var letra in texto.TrimEnd('='))
        {
            bufer = (bufer << 5) | Alfabeto.IndexOf(letra, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)(bufer >> bits));
                bufer &= (1 << bits) - 1;
            }
        }

        return [.. bytes];
    }

    [GeneratedRegex("""<form[^>]*method="post"[^>]*>.*?</form>""", RegexOptions.Singleline)]
    private static partial Regex Formularios();

    [GeneratedRegex("""<input(?=[^>]*type="hidden")(?=[^>]*name="(?<nombre>[^"]+)")(?=[^>]*value="(?<valor>[^"]*)")[^>]*>""")]
    private static partial Regex Ocultos();

    [GeneratedRegex("""<p class="dsm-entrar__clave"><code>(?<clave>[^<]+)</code></p>""")]
    private static partial Regex ClaveDeLaPagina();

    [GeneratedRegex("""<li><code>(?<codigo>[^<]+)</code></li>""")]
    private static partial Regex CodigoDeRecuperacion();
}
