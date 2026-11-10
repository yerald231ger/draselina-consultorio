using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;
using PaginaDelHistorial = DraSelina.Consultorio.UI.Pages.Administracion.HistorialDelPaciente;
using PaginaDePacientes = DraSelina.Consultorio.UI.Pages.Administracion.Pacientes;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Pacientes: búsqueda e historial (entrada 5.7) contra PostgreSQL real: la consulta <see cref="BuscarPacientes"/> en cada
/// fila de búsqueda de la matriz del plan (sin acentos ni mayúsculas, con acento, espacios, parte del nombre, sin texto,
/// más de 50, sin coincidencias, sin correo), la consulta <see cref="HistorialDelPaciente"/> (todos los estados en orden,
/// el resumen, sin Citas, inexistente), las dos sin sesión, y por HTTP: la redirección sin sesión, la navegación y el
/// camino de la lista al historial y al Detalle. Con el escenario y el reloj de <see cref="ReservaEnLineaTests"/>: lunes 2
/// de noviembre de 2026 a las 12:00 local.
/// </summary>
public sealed class PacientesBusquedaEHistorialTests(Arnes arnes)
{
    private const string IdDelAdministrador = "cuenta-del-administrador";

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("mariana")]
    [InlineData("  mariana  ")]
    [InlineData("MARIANA")]
    [InlineData("riana")]
    public async Task Busca_contiene_sin_distinguir_mayusculas_ni_espacios_en_orden_alfabetico(string texto)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await TresPacientesAsync(escenario);

        var encontrados = await BuscarAsync(escenario, texto);

        Assert.Equal(["Mariana López", "MARIANA Pérez"], encontrados.Pacientes.Select(p => p.Nombre));
        Assert.False(encontrados.HayMas);
    }

    [Theory]
    [InlineData("lópez")]
    [InlineData("lopez")]
    [InlineData("LÓPEZ")]
    public async Task Con_o_sin_acento_encuentra_a_Mariana_Lopez(string texto)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await TresPacientesAsync(escenario);

        var encontrados = await BuscarAsync(escenario, texto);

        Assert.Equal(["Mariana López"], encontrados.Pacientes.Select(p => p.Nombre));
    }

    [Fact]
    public async Task Lop_encuentra_a_Mariana_Lopez_y_a_Rosa_Lopera()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await TresPacientesAsync(escenario);

        var encontrados = await BuscarAsync(escenario, "lop");

        Assert.Equal(["Mariana López", "Rosa Lopera"], encontrados.Pacientes.Select(p => p.Nombre));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Sin_texto_da_todos_en_orden_alfabetico_con_telefono_y_si_tienen_correo(string? texto)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var (mariana, perez, rosa) = await TresPacientesAsync(escenario);

        var encontrados = await BuscarAsync(escenario, texto);

        Assert.Equal(
            [
                new PacienteEncontrado(mariana, "Mariana López", "+524445551001", TieneCorreo: true),
                new PacienteEncontrado(perez, "MARIANA Pérez", "+524445551002", TieneCorreo: false),
                new PacienteEncontrado(rosa, "Rosa Lopera", "+524445551003", TieneCorreo: true),
            ],
            encontrados.Pacientes);
        Assert.False(encontrados.HayMas);
    }

    [Fact]
    public async Task Con_mas_de_50_da_los_primeros_50_y_dice_que_hay_mas()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using (var ambito = escenario.Web.Services.CreateAsyncScope())
        {
            var api = ambito.ServiceProvider.GetRequiredService<IPacientesApi>();
            foreach (var i in Enumerable.Range(0, 60).Reverse())
            {
                Assert.True((await api.BuscarOCrearAsync($"Paciente Número {i:00}", $"44455510{i:00}", null, Cancelacion)).EsExito);
            }
        }

        var encontrados = await BuscarAsync(escenario, null);

        Assert.True(encontrados.HayMas);
        Assert.Equal(
            Enumerable.Range(0, BuscarPacientes.Maximo).Select(i => $"Paciente Número {i:00}"),
            encontrados.Pacientes.Select(p => p.Nombre));

        var conTexto = await BuscarAsync(escenario, "numero 5");
        Assert.Equal(Enumerable.Range(50, 10).Select(i => $"Paciente Número {i:00}"), conTexto.Pacientes.Select(p => p.Nombre));
        Assert.False(conTexto.HayMas);
    }

    [Theory]
    [InlineData("zzz")]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("123")]
    public async Task Sin_coincidencias_no_da_ninguno(string texto)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await TresPacientesAsync(escenario);

        var encontrados = await BuscarAsync(escenario, texto);

        Assert.Empty(encontrados.Pacientes);
        Assert.False(encontrados.HayMas);
    }

    [Fact]
    public async Task Sin_sesion_las_dos_consultas_vuelven_no_autorizado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var (mariana, _, _) = await TresPacientesAsync(escenario);

        var busqueda = await ConsultarAsync<BuscarPacientes, PacientesEncontrados>(escenario, new BuscarPacientes("mariana"), SinSesion);
        var historial = await ConsultarAsync<HistorialDelPaciente, PacienteConHistorial>(escenario, new HistorialDelPaciente(mariana), SinSesion);

        Assert.Equal(FormaDeResultado.NoAutorizado, busqueda.Forma);
        Assert.Equal(FormaDeResultado.NoAutorizado, historial.Forma);
    }

    [Fact]
    public async Task El_historial_trae_todas_sus_Citas_de_la_mas_reciente_a_la_mas_antigua_con_su_estado_y_su_resumen()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var (mariana, enLinea) = await MarianaConSeisCitasAsync(escenario);

        var historial = await HistorialAsync(escenario, mariana);

        Assert.Equal(mariana, historial.PatientId);
        Assert.Equal(new ContactoDelPaciente("Mariana López", "+524445551001", "mariana@correo.com"), historial.Paciente);
        Assert.Equal(
            [
                (new DateOnly(2026, 11, 3), new TimeOnly(16, 0), OrigenEnLaAgenda.EnLinea, EstadoEnLaAgenda.Agendada, (CancelacionEnLaAgenda?)null),
                (new DateOnly(2026, 10, 26), new TimeOnly(10, 0), OrigenEnLaAgenda.DelConsultorio, EstadoEnLaAgenda.Atendida, null),
                (new DateOnly(2026, 10, 19), new TimeOnly(10, 0), OrigenEnLaAgenda.DelConsultorio, EstadoEnLaAgenda.NoSePresento, null),
                (new DateOnly(2026, 10, 12), new TimeOnly(10, 0), OrigenEnLaAgenda.DelConsultorio, EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElPaciente),
                (new DateOnly(2026, 10, 5), new TimeOnly(10, 0), OrigenEnLaAgenda.DelConsultorio, EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.Tardia),
                (new DateOnly(2026, 9, 28), new TimeOnly(10, 0), OrigenEnLaAgenda.DelConsultorio, EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElConsultorio),
            ],
            historial.Citas.Select(c => (c.Fecha, c.Hora, c.Origen, c.Estado, c.Cancelacion)));
        Assert.Equal(enLinea, historial.Citas[0].CitaId);
        Assert.All(historial.Citas, c => Assert.Equal("Primera consulta", c.TipoDeVisita));
        Assert.Equal(new ResumenDelHistorial(6, 1, 1), historial.Resumen);
        Assert.Equal("6 citas · 1 no se presentó · 1 cancelación tardía", PaginaDelHistorial.Resumen(historial.Resumen));
    }

    [Fact]
    public async Task Cuando_llega_su_inicio_la_Cita_Agendada_del_historial_se_ve_Atendida()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var (mariana, _) = await MarianaConSeisCitasAsync(escenario);
        escenario.Reloj.Avanzar(HoraLocal.AUtc(new DateOnly(2026, 11, 3), new TimeOnly(16, 0)) - Ahora);

        var historial = await HistorialAsync(escenario, mariana);

        Assert.Equal(EstadoEnLaAgenda.Atendida, historial.Citas[0].Estado);
    }

    [Fact]
    public async Task Un_Paciente_sin_Citas_trae_su_contacto_sin_Citas_y_sin_resumen()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var (_, perez, _) = await TresPacientesAsync(escenario);

        var historial = await HistorialAsync(escenario, perez);

        Assert.Equal(new ContactoDelPaciente("MARIANA Pérez", "+524445551002", null), historial.Paciente);
        Assert.Empty(historial.Citas);
        Assert.Equal(new ResumenDelHistorial(0, 0, 0), historial.Resumen);
        Assert.Null(PaginaDelHistorial.Resumen(historial.Resumen));
    }

    [Fact]
    public async Task Un_Paciente_que_no_existe_vuelve_paciente_no_encontrado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(9, 0), new TimeOnly(9, 30));

        var resultado = await ConsultarAsync<HistorialDelPaciente, PacienteConHistorial>(
            escenario, new HistorialDelPaciente(PatientId.Nuevo()), ComoAdministrador);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.PacienteNoEncontrado, resultado.Codigo);
    }

    [Fact]
    public async Task Los_resultados_no_muestran_nombre_telefono_ni_correo_al_convertirse_en_texto()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var (mariana, _) = await MarianaConSeisCitasAsync(escenario);

        var encontrados = await BuscarAsync(escenario, "mariana");
        var historial = await HistorialAsync(escenario, mariana);
        var texto = string.Join(
            " ",
            new BuscarPacientes("mariana").ToString(),
            encontrados.ToString(),
            string.Join(" ", encontrados.Pacientes.Select(p => p.ToString())),
            historial.ToString(),
            historial.Paciente.ToString(),
            string.Join(" ", historial.Citas.Select(c => c.ToString())));

        foreach (var dato in new[] { "mariana", "lópez", "4445551001", "correo.com", "rodilla" })
        {
            Assert.DoesNotContain(dato, texto, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(mariana.ToString(), texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/admin/pacientes", "%2Fadmin%2Fpacientes")]
    [InlineData("/admin/pacientes/0193a7a2-0000-7000-8000-000000000001", "%2Fadmin%2Fpacientes%2F0193a7a2-0000-7000-8000-000000000001")]
    public async Task Sin_sesion_las_paginas_llevan_a_entrar_con_su_direccion_de_regreso(string ruta, string regreso)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        using var cliente = EntrarConSegundoFactorTests.Cliente(escenario.Web);

        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal(
            $"/admin/entrar?ReturnUrl={regreso}",
            respuesta.Headers.Location!.IsAbsoluteUri ? respuesta.Headers.Location.PathAndQuery : respuesta.Headers.Location.OriginalString);
    }

    [Fact]
    public async Task La_navegacion_dice_Agenda_Pacientes_Salir_con_Pacientes_activo_en_sus_paginas()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var (mariana, _, _) = await TresPacientesAsync(escenario);
        using var cliente = await EntrarConSegundoFactorTests.ClienteConSesionAsync(escenario.Web);

        foreach (var (ruta, activo) in new[]
                 {
                     ("/admin", "Agenda"),
                     ("/admin/pacientes", "Pacientes"),
                     ($"/admin/pacientes/{mariana.Valor}", "Pacientes"),
                 })
        {
            var navegacion = Navegacion(await LeerAsync(cliente, ruta));

            Assert.Equal(["Agenda", "Pacientes"], navegacion.Select(e => e.Texto));
            Assert.Equal(["/admin", "/admin/pacientes"], navegacion.Select(e => e.Href));
            Assert.Equal([activo], navegacion.Where(e => e.Activo).Select(e => e.Texto));
        }
    }

    [Fact]
    public async Task Con_sesion_desde_la_lista_se_abre_el_historial_de_Mariana_que_distingue_cada_estado_y_cada_Cita_abre_su_Detalle()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var (mariana, enLinea) = await MarianaConSeisCitasAsync(escenario);
        await TresPacientesAsync(escenario);
        using var cliente = await EntrarConSegundoFactorTests.ClienteConSesionAsync(escenario.Web);

        // La búsqueda corre en el circuito (bUnit); por HTTP se pinta la lista sin texto, que lleva al historial.
        var lista = await LeerAsync(cliente, "/admin/pacientes");
        var historialDeMariana = PaginaDelHistorial.Direccion(mariana);
        Assert.Contains($"href=\"{historialDeMariana}\">Mariana López</a>", lista, StringComparison.Ordinal);
        Assert.Contains("MARIANA Pérez", lista, StringComparison.Ordinal);
        Assert.Contains("444 555 1001", lista, StringComparison.Ordinal);
        Assert.DoesNotMatch("href=\"[^\"]*\\?q=", lista);

        var historial = await LeerAsync(cliente, historialDeMariana);
        Assert.Contains("Mariana López", historial, StringComparison.Ordinal);
        Assert.Contains("6 citas · 1 no se presentó · 1 cancelación tardía", historial, StringComparison.Ordinal);
        foreach (var estado in new[]
                 {
                     "Agendada", "Atendida", "No se presentó", "Cancelada (por el paciente)", "Cancelada (tardía)",
                     "Cancelada (por el consultorio)",
                 })
        {
            Assert.Contains($">{estado}</span>", historial, StringComparison.Ordinal);
        }

        var detalle = $"/admin/citas/{enLinea.Valor}";
        Assert.Contains($"href=\"{detalle}\"", historial, StringComparison.Ordinal);
        Assert.Equal(6, Regex.Matches(historial, "href=\"/admin/citas/[0-9a-f-]+\"").Count);
        Assert.Contains("Mariana López", await LeerAsync(cliente, detalle), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_sesion_la_direccion_no_trae_busqueda_y_un_Paciente_sin_Citas_y_uno_que_no_existe_lo_dicen()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var (_, perez, _) = await TresPacientesAsync(escenario);
        using var cliente = await EntrarConSegundoFactorTests.ClienteConSesionAsync(escenario.Web);

        // El texto nunca viene de la dirección (AD-23): un ?q= se ignora y la lista sale completa.
        var conQ = await LeerAsync(cliente, "/admin/pacientes?q=zzz");
        Assert.DoesNotContain(PaginaDePacientes.NoEncontramos, conQ, StringComparison.Ordinal);
        Assert.Contains("Rosa Lopera", conQ, StringComparison.Ordinal);

        var sinCitas = await LeerAsync(cliente, PaginaDelHistorial.Direccion(perez));
        Assert.Contains(PaginaDelHistorial.SinCitas, sinCitas, StringComparison.Ordinal);
        Assert.DoesNotContain("dsm-historial__resumen", sinCitas, StringComparison.Ordinal);

        var noExiste = await LeerAsync(cliente, PaginaDelHistorial.Direccion(PatientId.Nuevo()));
        Assert.Contains(PaginaDelHistorial.NoExiste, noExiste, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/admin/pacientes\" class=\"dsm-volver\">", noExiste, StringComparison.Ordinal);
    }

    /// <summary>Mariana López (con correo), MARIANA Pérez (sin correo) y Rosa Lopera (con correo).</summary>
    private static async Task<(PatientId Mariana, PatientId Perez, PatientId Rosa)> TresPacientesAsync(Escenario escenario)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var api = ambito.ServiceProvider.GetRequiredService<IPacientesApi>();
        return (
            (await api.BuscarOCrearAsync("Mariana López", "444 555 1001", "mariana@correo.com", Cancelacion)).Valor,
            (await api.BuscarOCrearAsync("MARIANA Pérez", "444 555 1002", null, Cancelacion)).Valor,
            (await api.BuscarOCrearAsync("Rosa Lopera", "444 555 1003", "rosa@correo.com", Cancelacion)).Valor);
    }

    /// <summary>
    /// Mariana López con seis Citas: una en línea Agendada mañana a las 16:00 (por el caso de uso) y cinco del consultorio
    /// ya pasadas, escritas en la base, a las 10:00 de cada lunes anterior: Atendida (26 oct), No se presentó (19 oct),
    /// Cancelada por el Paciente (12 oct), Cancelada tarde (5 oct) y Cancelada por el Consultorio (28 sep). Más una Cita
    /// de otro Paciente, que no aparece.
    /// </summary>
    private static async Task<(PatientId Mariana, CitaId EnLinea)> MarianaConSeisCitasAsync(Escenario escenario)
    {
        var comando = Comando(Manana, new TimeOnly(16, 0));
        comando.Nombre = "Mariana López";
        comando.Telefono = "444 555 1001";
        comando.Correo = "mariana@correo.com";
        var agendada = await escenario.AgendarAsync(comando);
        Assert.True(agendada.EsExito, $"{agendada.Forma} {agendada.Codigo}");

        var mariana = new PatientId(Assert.Single(await escenario.PacientesAsync()).Id);
        var pasadas = new (DateOnly Fecha, string Estado, string? Motivo)[]
        {
            (new DateOnly(2026, 10, 26), "Agendada", null),
            (new DateOnly(2026, 10, 19), "NoSePresento", null),
            (new DateOnly(2026, 10, 12), "Cancelada", "paciente"),
            (new DateOnly(2026, 10, 5), "Cancelada", "tarde"),
            (new DateOnly(2026, 9, 28), "Cancelada", "consultorio"),
        };
        foreach (var (fecha, estado, motivo) in pasadas)
        {
            await escenario.InsertarCitaDelConsultorioAsync(fecha, new TimeOnly(10, 0), new TimeOnly(10, 30), mariana.Valor);
            var inicio = HoraLocal.AUtc(fecha, new TimeOnly(10, 0));
            await escenario.EjecutarAsync(motivo is null
                ? $"update agenda.cita set estado = '{estado}' where inicio = '{inicio:O}'"
                : $"update agenda.cita set estado = '{estado}', motivo_de_cancelacion = '{motivo}', cancelada_en = '{inicio.AddDays(-1):O}' where inicio = '{inicio:O}'");
        }

        await escenario.InsertarCitaDelConsultorioAsync(new DateOnly(2026, 10, 27), new TimeOnly(9, 0), new TimeOnly(9, 30));
        return (mariana, agendada.Valor.CitaId);
    }

    private static async Task<PacientesEncontrados> BuscarAsync(Escenario escenario, string? texto)
    {
        var resultado = await ConsultarAsync<BuscarPacientes, PacientesEncontrados>(escenario, new BuscarPacientes(texto), ComoAdministrador);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static async Task<PacienteConHistorial> HistorialAsync(Escenario escenario, PatientId paciente)
    {
        var resultado = await ConsultarAsync<HistorialDelPaciente, PacienteConHistorial>(
            escenario, new HistorialDelPaciente(paciente), ComoAdministrador);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    /// <summary>La consulta por su interfaz, en un ámbito propio con la sesión que se pida, como la pide la página.</summary>
    private static async Task<Resultado<T>> ConsultarAsync<TConsulta, T>(
        Escenario escenario, TConsulta consulta, Action<AsyncServiceScope> sesion)
        where TConsulta : IQuery<T>
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        sesion(ambito);
        return await ambito.ServiceProvider.GetRequiredService<IQueryHandler<TConsulta, T>>().HandleAsync(consulta, Cancelacion);
    }

    private static void ComoAdministrador(AsyncServiceScope ambito) =>
        ((IHostEnvironmentAuthenticationStateProvider)ambito.ServiceProvider.GetRequiredService<AuthenticationStateProvider>())
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, IdDelAdministrador), new Claim(ClaimTypes.Name, "administradora")],
                authenticationType: "Prueba")))));

    // Los enlaces de la navegación de administración, en orden: su texto, su dirección y si está activo.
    private static List<(string Texto, string Href, bool Activo)> Navegacion(string pagina)
    {
        var navegacion = Regex.Match(pagina, "<nav class=\"dsm-administracion__navegacion\"[^>]*>(.*?)</nav>", RegexOptions.Singleline);
        Assert.True(navegacion.Success, "La página no tiene la navegación de administración.");
        Assert.Contains("Salir", navegacion.Groups[1].Value, StringComparison.Ordinal);
        return
        [
            .. Regex.Matches(navegacion.Groups[1].Value, "<a ([^>]*)>([^<]*)</a>")
                .Select(enlace => (
                    enlace.Groups[2].Value.Trim(),
                    Regex.Match(enlace.Groups[1].Value, "href=\"([^\"]*)\"").Groups[1].Value,
                    enlace.Groups[1].Value.Contains("dsm-administracion__enlace--activo", StringComparison.Ordinal))),
        ];
    }

    private static async Task<string> LeerAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"GET {ruta}: {respuesta.StatusCode}");
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }
}
