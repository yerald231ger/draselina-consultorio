using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.UI.Pages.Gestion;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;
using PaginaCancelar = DraSelina.Consultorio.UI.Pages.Gestion.Cancelar;
using PaginaReprogramar = DraSelina.Consultorio.UI.Pages.Gestion.Reprogramar;
using VistaCitaCancelada = DraSelina.Consultorio.UI.Pages.Gestion.CitaCancelada;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Cancelar desde Tu cita (CAP-8, FR-10, UJ-2 pasos 4 a 7; entrada 3.4): <see cref="CancelarCitaPorEnlace"/> por su
/// handler contra PostgreSQL real, y <c>/c/&lt;token&gt;/cancelar</c> por HTTP contra la web real. La Cita queda
/// Cancelada con su motivo, que decide la Cita (RN-16), con su bitácora y <see cref="Agenda.Contracts.CitaCancelada"/>
/// en la misma transacción (T-30). El reloj está en el lunes 2 de noviembre de 2026 a las 18:00 UTC y la Cita, Agendada
/// el martes 3 a las 16:00 local (22:00 UTC, 30 min), con umbral de 120.
/// </summary>
public sealed partial class CancelarCitaTests(Arnes arnes)
{
    private const string Robots = """<meta name="robots" content="noindex, nofollow">""";

    private static readonly DateOnly Jueves = new(2026, 11, 5);
    private static readonly DateTimeOffset Inicio = HoraLocal.AUtc(Manana, new TimeOnly(16, 0));

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    // Por el handler.

    [Fact]
    public async Task Cancelar_con_tiempo_deja_la_Cita_cancelada_por_el_Paciente_con_su_bitacora_y_su_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        var ahora = Inicio.AddHours(-3);
        escenario.Reloj.Avanzar(ahora - Ahora);

        var resultado = await CancelarAsync(escenario, agendada);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(
            new CitaCanceladaPorEnlace(Manana, new TimeOnly(16, 0), "Primera consulta", Tardia: false, PorElConsultorio: false),
            resultado.Valor);

        var (estado, motivo, canceladaEn) = await CancelacionGuardadaAsync(escenario);
        Assert.Equal("Cancelada", estado);
        Assert.Equal("paciente", motivo);
        Assert.Equal(ahora, canceladaEn);
        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(Inicio, cita.Inicio);
        Assert.Equal(Inicio.AddMinutes(30), cita.Fin);

        var entrada = Assert.Single(await CanceladasAsync(escenario));
        Assert.Equal(ahora, entrada.OcurridoEn);
        Assert.Equal("Paciente", entrada.ActorTipo);
        Assert.Null(entrada.ActorId);
        Assert.Equal("Cita", entrada.Entidad);
        Assert.Equal(cita.Id.ToString(), entrada.EntidadId);
        using (var detalle = JsonDocument.Parse(entrada.Detalle))
        {
            var raiz = detalle.RootElement;
            Assert.Equal(Inicio, raiz.GetProperty("inicio").GetDateTimeOffset());
            Assert.Equal(Inicio.AddMinutes(30), raiz.GetProperty("fin").GetDateTimeOffset());
            Assert.Equal("paciente", raiz.GetProperty("motivo").GetString());
        }

        var fila = Assert.Single(await EventosCanceladaAsync(escenario, cita.Id));
        Assert.Equal("Pendiente", fila.Estado);
        Assert.Equal(ahora, fila.OcurridoEn);
        Assert.Equal(cita.Id, fila.ClaveDeOrden);
        using (var contenido = JsonDocument.Parse(fila.Contenido))
        {
            Assert.Equal("Paciente", contenido.RootElement.GetProperty(nameof(Agenda.Contracts.CitaCancelada.Actor)).GetString());
            Assert.Equal("paciente", contenido.RootElement.GetProperty(nameof(Agenda.Contracts.CitaCancelada.Motivo)).GetString());
        }

        Assert.Equal(
            new Agenda.Contracts.CitaCancelada(
                fila.Id, ahora, new CitaId(cita.Id), new PatientId(cita.PacienteId), Actor.Paciente, Inicio,
                Agenda.Contracts.CitaCancelada.MotivoPaciente),
            JsonSerializer.Deserialize<Agenda.Contracts.CitaCancelada>(fila.Contenido));
    }

    [Fact]
    public async Task Justo_en_el_umbral_el_motivo_es_paciente()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(-120) - Ahora);

        var resultado = await CancelarAsync(escenario, agendada);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.False(resultado.Valor.Tardia);
        Assert.Equal("paciente", (await CancelacionGuardadaAsync(escenario)).Motivo);
    }

    [Fact]
    public async Task Una_hora_antes_es_cancelacion_tardia_en_la_Cita_la_bitacora_y_el_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddHours(-1) - Ahora);

        var resultado = await CancelarAsync(escenario, agendada);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.True(resultado.Valor.Tardia);
        Assert.Equal("tarde", (await CancelacionGuardadaAsync(escenario)).Motivo);
        using (var detalle = JsonDocument.Parse(Assert.Single(await CanceladasAsync(escenario)).Detalle))
        {
            Assert.Equal("tarde", detalle.RootElement.GetProperty("motivo").GetString());
        }

        var evento = JsonSerializer.Deserialize<Agenda.Contracts.CitaCancelada>(
            Assert.Single(await EventosCanceladaAsync(escenario, agendada.CitaId.Valor)).Contenido);
        Assert.Equal(Agenda.Contracts.CitaCancelada.MotivoTarde, evento!.Motivo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    public async Task Una_Cita_que_ya_empezo_vuelve_como_cita_no_encontrada_sin_cambios(int minutosDespues)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        var antes = await ConteosAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(minutosDespues) - Ahora);

        var resultado = await CancelarAsync(escenario, agendada);

        Assert.Equal(CodigosDeAgenda.CitaNoEncontrada, resultado.Codigo);
        await SinCambiosAsync(escenario, antes);
    }

    [Fact]
    public async Task Un_segundo_envio_devuelve_el_motivo_guardado_sin_otra_bitacora_ni_otro_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddHours(-3) - Ahora);

        var primero = await CancelarAsync(escenario, agendada);
        var antes = await ConteosAsync(escenario);

        // Ya dentro del umbral: el segundo envío no recalcula la tardía, devuelve la de la primera vez.
        escenario.Reloj.Avanzar(TimeSpan.FromHours(2.5));
        var segundo = await CancelarAsync(escenario, agendada);

        Assert.True(primero.EsExito, $"{primero.Forma} {primero.Codigo}");
        Assert.True(segundo.EsExito, $"{segundo.Forma} {segundo.Codigo}");
        Assert.Equal(primero.Valor, segundo.Valor);
        Assert.False(segundo.Valor.Tardia);
        Assert.Equal(antes, await ConteosAsync(escenario));
        Assert.Single(await CanceladasAsync(escenario));
        Assert.Single(await EventosCanceladaAsync(escenario, agendada.CitaId.Valor));
        Assert.Equal("paciente", (await CancelacionGuardadaAsync(escenario)).Motivo);
    }

    [Theory]
    [InlineData("paciente", false, false)]
    [InlineData("tarde", true, false)]
    [InlineData("consultorio", false, true)]
    public async Task Una_Cita_ya_cancelada_devuelve_su_motivo_guardado(string motivo, bool tardia, bool porElConsultorio)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        await escenario.EjecutarAsync(
            $"update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = '{motivo}', cancelada_en = now()");
        var antes = await ConteosAsync(escenario);

        var resultado = await CancelarAsync(escenario, agendada);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(tardia, resultado.Valor.Tardia);
        Assert.Equal(porElConsultorio, resultado.Valor.PorElConsultorio);
        Assert.Equal(motivo, (await CancelacionGuardadaAsync(escenario)).Motivo);
        Assert.Equal(antes, await ConteosAsync(escenario));
    }

    [Fact]
    public async Task Una_Cita_que_no_se_presento_vuelve_como_cita_no_agendada_sin_cambios()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        await escenario.EjecutarAsync("update agenda.cita set estado = 'NoSePresento'");
        var antes = await ConteosAsync(escenario);

        var resultado = await CancelarAsync(escenario, agendada);

        Assert.Equal(CodigosDeAgenda.CitaNoAgendada, resultado.Codigo);
        Assert.Equal("NoSePresento", (await CancelacionGuardadaAsync(escenario)).Estado);
        Assert.Equal(antes, await ConteosAsync(escenario));
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("no-es-un-token")]
    public async Task Un_token_inexistente_o_mal_formado_vuelve_como_cita_no_encontrada(string token)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAsync(escenario);
        var antes = await ConteosAsync(escenario);

        var resultado = await CancelarAsync(escenario, token);

        Assert.Equal(CodigosDeAgenda.CitaNoEncontrada, resultado.Codigo);
        await SinCambiosAsync(escenario, antes);
    }

    [Fact]
    public async Task Un_token_vacio_no_llega_al_handler()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await EnviarAsync(escenario, new CancelarCitaPorEnlace());

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Contains(nameof(CancelarCitaPorEnlace.Token), resultado.Errores.Keys);
    }

    [Fact]
    public async Task El_horario_cancelado_vuelve_a_ofrecerse()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        Assert.DoesNotContain(new TimeOnly(16, 0), await HorasDelMartesAsync(escenario));

        Assert.True((await CancelarAsync(escenario, agendada)).EsExito);

        Assert.Contains(new TimeOnly(16, 0), await HorasDelMartesAsync(escenario));

        // Y otra Cita puede tomarlo: la restricción de exclusión ignora la cancelada.
        var otra = Comando(Manana, new TimeOnly(16, 0));
        otra.Telefono = "444 555 1000";
        Assert.True((await escenario.AgendarAsync(otra)).EsExito);
    }

    [Fact]
    public async Task El_token_no_va_a_los_registros_ni_al_texto_del_comando_ni_al_rastro()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        var token = Token(agendada);
        var comando = new CancelarCitaPorEnlace { Token = token };

        Assert.DoesNotContain(token, comando.ToString(), StringComparison.Ordinal);
        Assert.True((await EnviarAsync(escenario, comando)).EsExito);
        Assert.True((await EnviarAsync(escenario, comando)).EsExito);

        Assert.DoesNotContain(Arnes.Registros(escenario.Web), r => r.Mensaje.Contains(token[..20], StringComparison.Ordinal));
        var detalle = Assert.Single(await CanceladasAsync(escenario)).Detalle;
        var contenido = Assert.Single(await EventosCanceladaAsync(escenario, agendada.CitaId.Valor)).Contenido;
        foreach (var texto in new[] { detalle, contenido })
        {
            foreach (var dato in new[] { token[..20], "Ana", "López", "4441234567", "ana@correo.com", "rodilla" })
            {
                Assert.DoesNotContain(dato, texto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task La_cancelacion_espera_a_quien_tiene_bloqueada_la_fila_y_reprogramar_despues_recibe_cita_no_agendada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        // Otra transacción bloquea la fila de la Cita, como lo haría una reprogramación en curso.
        await using var conexion = new NpgsqlConnection(escenario.Cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var otra = await conexion.BeginTransactionAsync(Cancelacion);
        await using (var bloqueo = new NpgsqlCommand(
            $"select id from agenda.cita where id = '{agendada.CitaId.Valor}' for update", conexion, otra))
        {
            await bloqueo.ExecuteNonQueryAsync(Cancelacion);
        }

        var cancelacion = CancelarAsync(escenario, agendada);
        await Task.Delay(TimeSpan.FromMilliseconds(500), Cancelacion);
        Assert.False(cancelacion.IsCompleted);

        await otra.CommitAsync(Cancelacion);
        Assert.True((await cancelacion).EsExito);

        var reprogramacion = await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));
        Assert.Equal(CodigosDeAgenda.CitaNoAgendada, reprogramacion.Codigo);
        Assert.Empty(await escenario.FilasAsync("select id from agenda.bitacora where accion = 'Reprogramada'", l => l.GetInt64(0)));
    }

    [Fact]
    public async Task Cancelar_y_reprogramar_a_la_vez_se_aplican_uno_tras_otro()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        var jueves = HoraLocal.AUtc(Jueves, new TimeOnly(17, 0));

        var cancelar = CancelarAsync(escenario, agendada);
        var reprogramar = ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));
        await Task.WhenAll(cancelar, reprogramar);

        Assert.True((await cancelar).EsExito, $"{(await cancelar).Forma} {(await cancelar).Codigo}");
        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal("Cancelada", cita.Estado);
        Assert.Single(await CanceladasAsync(escenario));
        using var detalle = JsonDocument.Parse(Assert.Single(await CanceladasAsync(escenario)).Detalle);
        var inicioCancelado = detalle.RootElement.GetProperty("inicio").GetDateTimeOffset();

        if ((await reprogramar).EsExito)
        {
            // Reprogramar llegó primero: la cancelación partió de la Cita ya movida.
            Assert.Equal(jueves, inicioCancelado);
            Assert.Equal(jueves, cita.Inicio);
        }
        else
        {
            // Cancelar llegó primero: reprogramar la encontró cancelada y no la movió.
            Assert.Equal(CodigosDeAgenda.CitaNoAgendada, (await reprogramar).Codigo);
            Assert.Equal(Inicio, inicioCancelado);
            Assert.Equal(Inicio, cita.Inicio);
        }
    }

    // Por HTTP.

    [Fact]
    public async Task Tu_cita_Agendada_ofrece_Cancelar_cita_fuera_y_dentro_del_umbral()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();

        var (_, fuera) = await AbrirAsync(cliente, TuCita(cita));
        escenario.Reloj.Avanzar(Inicio.AddHours(-1) - Ahora);
        var (_, dentro) = await AbrirAsync(cliente, TuCita(cita));

        foreach (var html in new[] { fuera, dentro })
        {
            Assert.Matches(
                $"""<a href="{Regex.Escape(RutaDeCancelar(cita))}" class="btn dsm-boton-peligro[^"]*">Cancelar cita</a>""", html);
        }
    }

    [Fact]
    public async Task Abrir_cancelar_con_tiempo_pregunta_sin_panel_ambar()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddHours(-3) - Ahora);
        using var cliente = escenario.Web.CreateClient();

        using var respuesta = await cliente.GetAsync(RutaDeCancelar(cita), Cancelacion);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        SinIndiceNiCache(respuesta);
        Assert.Contains(Robots, html, StringComparison.Ordinal);
        Assert.Contains("<title>Cancelar tu cita · Dra. Selina Medina Castillo</title>", html, StringComparison.Ordinal);
        Assert.Contains($">{PaginaCancelar.Titulo}</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Martes 3 de noviembre, 4:00 pm · Primera consulta", html, StringComparison.Ordinal);
        Assert.Matches($"""<button type="submit" class="btn dsm-boton-cancelar">{PaginaCancelar.Confirmar}</button>""", html);
        Assert.Matches($"""<a href="{Regex.Escape(TuCita(cita))}" class="btn dsm-boton-neutro">{PaginaCancelar.Conservar}</a>""", html);
        var (accion, campos) = Formulario(html);
        Assert.Equal(RutaDeCancelar(cita), accion);
        Assert.Contains("__RequestVerificationToken", campos.Keys);
        Assert.DoesNotContain(campos.Values, v => v.Contains(Token(cita), StringComparison.Ordinal));
        Assert.DoesNotContain("Faltan menos de", html, StringComparison.Ordinal);
        Assert.DoesNotContain(PaginaCancelar.Tardia, html, StringComparison.Ordinal);
        Assert.DoesNotContain("¿Buscabas otro horario?", html, StringComparison.Ordinal);
        SinIdNiScripts(html, cita);
    }

    [Fact]
    public async Task Abrir_cancelar_dentro_del_umbral_muestra_el_panel_ambar_con_el_texto_de_tardia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddHours(-1) - Ahora);
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, RutaDeCancelar(cita));

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Contains($">{PaginaCancelar.Titulo}</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Faltan menos de 2 horas", html, StringComparison.Ordinal);
        Assert.Contains(PaginaCancelar.Tardia, html, StringComparison.Ordinal);
        Assert.Contains("¿Buscabas otro horario?", html, StringComparison.Ordinal);
        Assert.Contains(PanelDentroDelUmbral.OtroHorario, html, StringComparison.Ordinal);
        Assert.Contains(PaginaCancelar.Confirmar, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirmar_con_tiempo_pinta_Cita_cancelada_sin_la_frase_de_tardia_y_Tu_cita_se_ve_Cancelada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddHours(-3) - Ahora);
        using var cliente = escenario.Web.CreateClient();
        var (_, pregunta) = await AbrirAsync(cliente, RutaDeCancelar(cita));
        var (accion, campos) = Formulario(pregunta);

        using var respuesta = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        SinIndiceNiCache(respuesta);
        Assert.Contains(Robots, html, StringComparison.Ordinal);
        Assert.Contains("<title>Cita cancelada · Dra. Selina Medina Castillo</title>", html, StringComparison.Ordinal);
        Assert.Contains($">{VistaCitaCancelada.Titulo}</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Martes 3 de noviembre, 4:00 pm", html, StringComparison.Ordinal);
        Assert.Contains("Primera consulta", html, StringComparison.Ordinal);
        Assert.DoesNotContain(VistaCitaCancelada.NotaTardia, html, StringComparison.Ordinal);
        Assert.Matches("""<a href="/reservar"[^>]*>Agendar otra cita</a>""", html);
        SinIdNiScripts(html, cita);

        Assert.Equal("paciente", (await CancelacionGuardadaAsync(escenario)).Motivo);
        var entrada = Assert.Single(await CanceladasAsync(escenario));
        Assert.Equal("Paciente", entrada.ActorTipo);

        // Tu cita, con el mismo enlace: Cancelada, con "Agendar otra cita" (3.1).
        var (estado, tuCita) = await AbrirAsync(cliente, TuCita(cita));
        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Matches("""<span class="dsm-chip-de-estado dsm-chip-de-estado--cancelada">\s*<svg[\s\S]*?</svg>\s*Cancelada\s*</span>""", tuCita);
        Assert.Contains(UI.Pages.Gestion.TuCita.CanceladaPorElPaciente, tuCita, StringComparison.Ordinal);
        Assert.Matches("""<a href="/reservar"[^>]*>Agendar otra cita</a>""", tuCita);
        Assert.DoesNotContain("Cancelar cita", tuCita, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirmar_dentro_del_umbral_pinta_Cita_cancelada_con_la_frase_de_tardia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddHours(-1) - Ahora);
        using var cliente = escenario.Web.CreateClient();
        var (_, pregunta) = await AbrirAsync(cliente, RutaDeCancelar(cita));
        var (accion, campos) = Formulario(pregunta);

        var html = await EnviarAsync(cliente, accion, campos);

        Assert.Contains($">{VistaCitaCancelada.Titulo}</h1>", html, StringComparison.Ordinal);
        Assert.Contains(VistaCitaCancelada.NotaTardia, html, StringComparison.Ordinal);
        Assert.Equal("tarde", (await CancelacionGuardadaAsync(escenario)).Motivo);
    }

    [Fact]
    public async Task Un_segundo_envio_pinta_Cita_cancelada_con_el_motivo_guardado_sin_otro_rastro()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddHours(-1) - Ahora);
        using var cliente = escenario.Web.CreateClient();
        var (_, pregunta) = await AbrirAsync(cliente, RutaDeCancelar(cita));
        var (accion, campos) = Formulario(pregunta);

        await EnviarAsync(cliente, accion, campos);
        var antes = await ConteosAsync(escenario);
        var segundo = await EnviarAsync(cliente, accion, campos);

        Assert.Contains($">{VistaCitaCancelada.Titulo}</h1>", segundo, StringComparison.Ordinal);
        Assert.Contains(VistaCitaCancelada.NotaTardia, segundo, StringComparison.Ordinal);
        Assert.Equal(antes, await ConteosAsync(escenario));
    }

    [Fact]
    public async Task Un_envio_sobre_una_Cita_cancelada_por_el_Consultorio_pinta_su_frase_y_Elegir_otro_horario()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, pregunta) = await AbrirAsync(cliente, RutaDeCancelar(cita));
        var (accion, campos) = Formulario(pregunta);
        await escenario.EjecutarAsync(
            "update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'consultorio', cancelada_en = now()");
        var antes = await ConteosAsync(escenario);

        var html = await EnviarAsync(cliente, accion, campos);

        Assert.Contains($">{VistaCitaCancelada.Titulo}</h1>", html, StringComparison.Ordinal);
        Assert.Contains(UI.Pages.Gestion.TuCita.CanceladaPorElConsultorio, html, StringComparison.Ordinal);
        Assert.Matches("""<a href="/reservar"[^>]*>Elegir otro horario</a>""", html);
        Assert.DoesNotContain("Agendar otra cita", html, StringComparison.Ordinal);
        Assert.DoesNotContain(VistaCitaCancelada.NotaTardia, html, StringComparison.Ordinal);
        Assert.Equal("consultorio", (await CancelacionGuardadaAsync(escenario)).Motivo);
        Assert.Equal(antes, await ConteosAsync(escenario));
    }

    [Theory]
    [InlineData(MotivoDeCancelacion.Paciente, Agenda.Contracts.CitaCancelada.MotivoPaciente)]
    [InlineData(MotivoDeCancelacion.Tarde, Agenda.Contracts.CitaCancelada.MotivoTarde)]
    [InlineData(MotivoDeCancelacion.Consultorio, Agenda.Contracts.CitaCancelada.MotivoConsultorio)]
    public void El_motivo_del_evento_es_el_texto_de_la_columna(MotivoDeCancelacion motivo, string delEvento)
    {
        // TextoDeEnumeraciones es interno de la web: se llama por reflexión.
        var texto = typeof(AgendaDbContext).Assembly
            .GetType("DraSelina.Consultorio.Agenda.Infrastructure.TextoDeEnumeraciones", throwOnError: true)!
            .GetMethod("Motivo", [typeof(MotivoDeCancelacion)])!;

        Assert.Equal(delEvento, (string)texto.Invoke(null, [motivo])!);
        // Los tres casos de arriba son todos los valores: uno nuevo exige su caso.
        Assert.Equal(3, Enum.GetValues<MotivoDeCancelacion>().Length);
    }

    [Fact]
    public async Task Abrir_cancelar_con_la_Cita_ya_cancelada_lo_dice_con_Ver_mi_cita()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        Assert.True((await CancelarAsync(escenario, cita)).EsExito);
        using var cliente = escenario.Web.CreateClient();

        var (estado, html) = await AbrirAsync(cliente, RutaDeCancelar(cita));

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Contains($">{PaginaCancelar.YaCancelada}</h1>", html, StringComparison.Ordinal);
        Assert.Equal(PaginaReprogramar.Cancelada, PaginaCancelar.YaCancelada);
        Assert.Matches($"""<a href="{Regex.Escape(TuCita(cita))}"[^>]*>Ver mi cita</a>""", html);
        Assert.DoesNotContain(PaginaCancelar.Confirmar, html, StringComparison.Ordinal);
        Assert.Contains(Robots, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_enlace_no_valido_o_una_Cita_que_ya_empezo_al_abrir_o_al_enviar_es_404_sin_indice_ni_cache()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, pregunta) = await AbrirAsync(cliente, RutaDeCancelar(cita));
        var (accion, campos) = Formulario(pregunta);
        var noValido = $"/c/{new string('A', 43)}/cancelar";

        using var alAbrir = await cliente.GetAsync(noValido, Cancelacion);
        using var alEnviar = await cliente.PostAsync(noValido, new FormUrlEncodedContent(campos), Cancelacion);
        using var conElId = await cliente.GetAsync($"/c/{cita.CitaId.Valor}/cancelar", Cancelacion);
        escenario.Reloj.Avanzar(Inicio - Ahora);
        using var yaEmpezoAlAbrir = await cliente.GetAsync(RutaDeCancelar(cita), Cancelacion);
        using var yaEmpezoAlEnviar = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);

        foreach (var respuesta in new[] { alAbrir, alEnviar, conElId, yaEmpezoAlAbrir, yaEmpezoAlEnviar })
        {
            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
            SinIndiceNiCache(respuesta);
            var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
            Assert.Contains(EnlaceNoValido.Titulo, html, StringComparison.Ordinal);
            Assert.Contains(Robots, html, StringComparison.Ordinal);
            Assert.DoesNotContain("Exception", html, StringComparison.Ordinal);
        }

        Assert.Equal("Agendada", (await CancelacionGuardadaAsync(escenario)).Estado);
        Assert.Empty(await CanceladasAsync(escenario));
    }

    [Fact]
    public async Task Un_envio_sin_antiforgery_se_rechaza_sin_cambios()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, pregunta) = await AbrirAsync(cliente, RutaDeCancelar(cita));
        var (accion, campos) = Formulario(pregunta);
        campos.Remove("__RequestVerificationToken");
        var antes = await ConteosAsync(escenario);

        using var respuesta = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);

        Assert.NotEqual(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Agendada", (await CancelacionGuardadaAsync(escenario)).Estado);
        Assert.Equal(antes, await ConteosAsync(escenario));
    }

    [Fact]
    public async Task El_token_no_va_a_los_registros_al_cancelar_por_HTTP()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        using var cliente = escenario.Web.CreateClient();
        var (_, pregunta) = await AbrirAsync(cliente, RutaDeCancelar(cita));
        var (accion, campos) = Formulario(pregunta);

        await EnviarAsync(cliente, accion, campos);

        var token = Token(cita);
        var registros = Arnes.Registros(escenario.Web);
        Assert.DoesNotContain(registros, r => r.Mensaje.Contains(token[..20], StringComparison.Ordinal));
        Assert.DoesNotContain(registros, r => r.Mensaje.Contains("Ana Sofía", StringComparison.Ordinal));
    }

    private static async Task<CitaAgendadaEnLinea> AgendarAsync(Escenario escenario)
    {
        var resultado = await escenario.AgendarAsync(Comando(Manana, new TimeOnly(16, 0)));
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static Task<Resultado<CitaCanceladaPorEnlace>> CancelarAsync(Escenario escenario, CitaAgendadaEnLinea cita) =>
        CancelarAsync(escenario, Token(cita));

    private static Task<Resultado<CitaCanceladaPorEnlace>> CancelarAsync(Escenario escenario, string token) =>
        EnviarAsync(escenario, new CancelarCitaPorEnlace { Token = token });

    // El handler por su interfaz, en un ámbito propio y sin sesión, como lo pide la página: el actor es el Paciente.
    private static async Task<Resultado<CitaCanceladaPorEnlace>> EnviarAsync(Escenario escenario, CancelarCitaPorEnlace comando)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        return await ambito.ServiceProvider
            .GetRequiredService<ICommandHandler<CancelarCitaPorEnlace, CitaCanceladaPorEnlace>>()
            .HandleAsync(comando, Cancelacion);
    }

    private static async Task<Resultado<CitaReprogramadaPorEnlace>> ReprogramarAsync(
        Escenario escenario, CitaAgendadaEnLinea cita, DateOnly fecha, TimeOnly hora)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        return await ambito.ServiceProvider
            .GetRequiredService<ICommandHandler<ReprogramarCitaPorEnlace, CitaReprogramadaPorEnlace>>()
            .HandleAsync(new ReprogramarCitaPorEnlace { Token = Token(cita), Fecha = fecha, Hora = hora }, Cancelacion);
    }

    // Las horas libres de Primera consulta el martes 3.
    private static async Task<IReadOnlyList<TimeOnly>> HorasDelMartesAsync(Escenario escenario)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>>()
            .HandleAsync(new HorariosLibres(TiposDeVisitaSembrados.PrimeraConsulta), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return Assert.Single(resultado.Valor, d => d.Fecha == Manana).Horas;
    }

    private static async Task<(string Estado, string? Motivo, DateTimeOffset? CanceladaEn)> CancelacionGuardadaAsync(Escenario escenario) =>
        Assert.Single(await escenario.FilasAsync(
            "select estado, motivo_de_cancelacion, cancelada_en from agenda.cita",
            l => (l.GetString(0), l.IsDBNull(1) ? null : l.GetString(1),
                  l.IsDBNull(2) ? (DateTimeOffset?)null : l.GetFieldValue<DateTimeOffset>(2))));

    // Filas de la bitácora y del outbox, todas.
    private static async Task<(long Bitacora, long Outbox)> ConteosAsync(Escenario escenario) =>
        (await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"),
         await escenario.EscalarAsync<long>("select count(*) from agenda.outbox"));

    // La Cita sigue Agendada el martes a las 16:00 y la bitácora y el outbox tienen las mismas filas que antes.
    private static async Task SinCambiosAsync(Escenario escenario, (long Bitacora, long Outbox) antes)
    {
        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal("Agendada", cita.Estado);
        Assert.Equal(Inicio, cita.Inicio);
        Assert.Equal(antes, await ConteosAsync(escenario));
    }

    private static Task<List<Cancelada>> CanceladasAsync(Escenario escenario) => escenario.FilasAsync(
        "select ocurrido_en, actor_tipo, actor_id, entidad, entidad_id, detalle::text from agenda.bitacora "
        + "where accion = 'Cancelada' order by id",
        l => new Cancelada(
            l.GetFieldValue<DateTimeOffset>(0), l.GetString(1), l.IsDBNull(2) ? null : l.GetString(2),
            l.GetString(3), l.GetString(4), l.GetString(5)));

    private static async Task<List<FilaLeidaDeOutbox>> EventosCanceladaAsync(Escenario escenario, Guid citaId) =>
        [.. (await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, citaId, Cancelacion))
            .Where(f => f.Tipo == nameof(Agenda.Contracts.CitaCancelada))];

    private static void SinIdNiScripts(string html, CitaAgendadaEnLinea cita)
    {
        Assert.DoesNotContain(cita.CitaId.Valor.ToString(), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(cita.CitaId.Valor.ToString("N"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    private static void SinIndiceNiCache(HttpResponseMessage respuesta)
    {
        Assert.Equal("noindex, nofollow", Assert.Single(respuesta.Headers.GetValues("X-Robots-Tag")));
        Assert.True(respuesta.Headers.CacheControl?.NoStore, "Lleva el Enlace de gestión: no se guarda en caché.");
    }

    private static async Task<(HttpStatusCode Estado, string Html)> AbrirAsync(HttpClient cliente, string ruta)
    {
        using var respuesta = await cliente.GetAsync(ruta, Cancelacion);
        return (respuesta.StatusCode, WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion)));
    }

    private static async Task<string> EnviarAsync(HttpClient cliente, string accion, Dictionary<string, string> campos)
    {
        using var respuesta = await cliente.PostAsync(accion, new FormUrlEncodedContent(campos), Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Cancelacion));
    }

    // El formulario de "Sí, cancelar mi cita": su destino y sus campos ocultos, con el token antifalsificación.
    private static (string Accion, Dictionary<string, string> Campos) Formulario(string html)
    {
        var formulario = FormularioDeEnvio().Match(html);
        Assert.True(formulario.Success, "La página no tiene el formulario de cancelar.");
        var campos = Ocultos().Matches(formulario.Value)
            .ToDictionary(m => m.Groups["nombre"].Value, m => m.Groups["valor"].Value);
        return (formulario.Groups["accion"].Value, campos);
    }

    private static string TuCita(CitaAgendadaEnLinea cita) => new Uri(cita.EnlaceDeGestion).AbsolutePath;

    private static string RutaDeCancelar(CitaAgendadaEnLinea cita) => $"{TuCita(cita)}/cancelar";

    private static string Token(CitaAgendadaEnLinea cita) =>
        cita.EnlaceDeGestion[(cita.EnlaceDeGestion.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];

    [GeneratedRegex("""<form method="post" action="(?<accion>[^"]*)"[^>]*>.*?</form>""", RegexOptions.Singleline)]
    private static partial Regex FormularioDeEnvio();

    [GeneratedRegex("""<input(?=[^>]*type="hidden")(?=[^>]*name="(?<nombre>[^"]+)")(?=[^>]*value="(?<valor>[^"]*)")[^>]*>""")]
    private static partial Regex Ocultos();

    private sealed record Cancelada(
        DateTimeOffset OcurridoEn,
        string ActorTipo,
        string? ActorId,
        string Entidad,
        string EntidadId,
        string Detalle);
}
