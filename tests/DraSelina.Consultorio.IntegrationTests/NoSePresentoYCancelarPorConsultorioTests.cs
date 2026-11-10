using System.Net;
using System.Security.Claims;
using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// <see cref="MarcarNoSePresento"/> y <see cref="CancelarCitaPorConsultorio"/> por sus handlers contra PostgreSQL real
/// (CAP-14, FR-16, FR-17, AD-9): la sesión del Administrador, la fila bloqueada, la bitácora con su id (RN-24),
/// <see cref="CitaCancelada"/> con motivo consultorio solo al cancelar (T-23), la disculpa y el Recordatorio de
/// Notificaciones (4.4, 4.2), <see cref="HorariosLibres"/> y <see cref="DiasDeLaAgenda"/>. Con el escenario y el reloj
/// de <see cref="ReservaEnLineaTests"/>: lunes 2 de noviembre de 2026 a las 12:00 local; la Cita, en línea, Agendada el
/// martes 3 a las 16:00 local (22:00 UTC), de Ana con correo.
/// </summary>
public sealed class NoSePresentoYCancelarPorConsultorioTests(Arnes arnes)
{
    private const string IdDelAdministrador = "cuenta-del-administrador";

    private static readonly DateTimeOffset Inicio = HoraLocal.AUtc(Manana, new TimeOnly(16, 0));

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    // Marcar No se presentó.

    [Fact]
    public async Task Marcar_despues_del_inicio_deja_No_se_presento_con_la_bitacora_del_Administrador_y_sin_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        var outboxAntes = await OutboxAsync(escenario);
        var ahora = Inicio.AddMinutes(10);
        escenario.Reloj.Avanzar(ahora - Ahora);

        var resultado = await MarcarAsync(escenario, cita);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(new CitaMarcadaNoSePresento(cita, EstadoEnLaAgenda.NoSePresento), resultado.Valor);

        var (estado, motivo, canceladaEn) = await EstadoGuardadoAsync(escenario);
        Assert.Equal("NoSePresento", estado);
        Assert.Null(motivo);
        Assert.Null(canceladaEn);

        var entrada = Assert.Single(await BitacoraAsync(escenario, "NoSePresento"));
        Assert.Equal(ahora, entrada.OcurridoEn);
        Assert.Equal("Administrador", entrada.ActorTipo);
        Assert.Equal(IdDelAdministrador, entrada.ActorId);
        Assert.Equal("Cita", entrada.Entidad);
        Assert.Equal(cita.ToString(), entrada.EntidadId);
        using (var detalle = JsonDocument.Parse(entrada.Detalle))
        {
            Assert.Equal("Agendada", detalle.RootElement.GetProperty("estadoAnterior").GetString());
            Assert.Equal("NoSePresento", detalle.RootElement.GetProperty("estadoNuevo").GetString());
            Assert.Equal(Inicio, detalle.RootElement.GetProperty("inicio").GetDateTimeOffset());
            Assert.Equal(Inicio.AddMinutes(30), detalle.RootElement.GetProperty("fin").GetDateTimeOffset());
            Assert.False(detalle.RootElement.TryGetProperty("motivo", out _));
        }

        // T-23: marcar No se presentó no publica nada.
        Assert.Equal(outboxAntes, await OutboxAsync(escenario));
    }

    [Fact]
    public async Task Marcar_en_el_inicio_mismo_se_puede()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio - Ahora);

        var resultado = await MarcarAsync(escenario, cita);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal("NoSePresento", (await EstadoGuardadoAsync(escenario)).Estado);
    }

    [Fact]
    public async Task Marcar_antes_del_inicio_vuelve_aun_no_empieza_sin_cambios()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(-10) - Ahora);
        var antes = await ConteosAsync(escenario);

        var resultado = await MarcarAsync(escenario, cita);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.AunNoEmpieza, resultado.Codigo);
        await SinCambiosAsync(escenario, antes);
    }

    [Fact]
    public async Task Marcar_dos_veces_devuelve_lo_guardado_sin_otra_bitacora()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(10) - Ahora);

        var primero = await MarcarAsync(escenario, cita);
        var antes = await ConteosAsync(escenario);
        escenario.Reloj.Avanzar(TimeSpan.FromHours(1));
        var segundo = await MarcarAsync(escenario, cita);

        Assert.True(primero.EsExito, $"{primero.Forma} {primero.Codigo}");
        Assert.True(segundo.EsExito, $"{segundo.Forma} {segundo.Codigo}");
        Assert.Equal(primero.Valor, segundo.Valor);
        Assert.Equal(antes, await ConteosAsync(escenario));
        Assert.Single(await BitacoraAsync(escenario, "NoSePresento"));
    }

    [Fact]
    public async Task Marcar_una_Cita_cancelada_por_el_Paciente_vuelve_cita_no_agendada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        await escenario.EjecutarAsync(
            "update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now()");
        escenario.Reloj.Avanzar(Inicio.AddMinutes(10) - Ahora);
        var antes = await ConteosAsync(escenario);

        var resultado = await MarcarAsync(escenario, cita);

        Assert.Equal(CodigosDeAgenda.CitaNoAgendada, resultado.Codigo);
        Assert.Equal(("Cancelada", "paciente"), await EstadoYMotivoAsync(escenario));
        Assert.Equal(antes, await ConteosAsync(escenario));
    }

    // Cancelar por el Consultorio.

    [Fact]
    public async Task Cancelar_antes_del_inicio_deja_la_Cita_cancelada_por_el_Consultorio_con_su_bitacora_y_su_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        var ahora = Inicio.AddMinutes(-10);
        escenario.Reloj.Avanzar(ahora - Ahora);

        var resultado = await CancelarAsync(escenario, cita);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(
            new CitaCanceladaPorConsultorio(cita, EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElConsultorio),
            resultado.Valor);

        // Sin umbral de tardía: a diez minutos sigue siendo del Consultorio.
        var (estado, motivo, canceladaEn) = await EstadoGuardadoAsync(escenario);
        Assert.Equal("Cancelada", estado);
        Assert.Equal("consultorio", motivo);
        Assert.Equal(ahora, canceladaEn);
        var guardada = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(Inicio, guardada.Inicio);
        Assert.Equal(Inicio.AddMinutes(30), guardada.Fin);

        var entrada = Assert.Single(await BitacoraAsync(escenario, "Cancelada"));
        Assert.Equal(ahora, entrada.OcurridoEn);
        Assert.Equal("Administrador", entrada.ActorTipo);
        Assert.Equal(IdDelAdministrador, entrada.ActorId);
        Assert.Equal("Cita", entrada.Entidad);
        Assert.Equal(cita.ToString(), entrada.EntidadId);
        using (var detalle = JsonDocument.Parse(entrada.Detalle))
        {
            Assert.Equal("Agendada", detalle.RootElement.GetProperty("estadoAnterior").GetString());
            Assert.Equal("Cancelada", detalle.RootElement.GetProperty("estadoNuevo").GetString());
            Assert.Equal(Inicio, detalle.RootElement.GetProperty("inicio").GetDateTimeOffset());
            Assert.Equal(Inicio.AddMinutes(30), detalle.RootElement.GetProperty("fin").GetDateTimeOffset());
            Assert.Equal("consultorio", detalle.RootElement.GetProperty("motivo").GetString());
        }

        var fila = Assert.Single(await EventosCanceladaAsync(escenario, cita));
        Assert.Equal("Pendiente", fila.Estado);
        Assert.Equal(ahora, fila.OcurridoEn);
        Assert.Equal(cita.Valor, fila.ClaveDeOrden);
        using (var contenido = JsonDocument.Parse(fila.Contenido))
        {
            Assert.Equal("Administrador", contenido.RootElement.GetProperty(nameof(CitaCancelada.Actor)).GetString());
        }

        Assert.Equal(
            new CitaCancelada(
                fila.Id, ahora, cita, new PatientId(guardada.PacienteId), Actor.Administrador, Inicio, CitaCancelada.MotivoConsultorio),
            JsonSerializer.Deserialize<CitaCancelada>(fila.Contenido));

        // AD-23: ni motivo de consulta, ni contacto.
        foreach (var texto in new[] { entrada.Detalle, fila.Contenido })
        {
            foreach (var dato in new[] { "rodilla", "Ana", "4441234567", "ana@correo.com" })
            {
                Assert.DoesNotContain(dato, texto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public async Task Cancelar_en_el_inicio_o_despues_vuelve_ya_empezo_sin_cambios(int minutosDespues)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(minutosDespues) - Ahora);
        var antes = await ConteosAsync(escenario);

        var resultado = await CancelarAsync(escenario, cita);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.YaEmpezo, resultado.Codigo);
        await SinCambiosAsync(escenario, antes);
    }

    [Fact]
    public async Task Cancelar_dos_veces_devuelve_lo_guardado_sin_otra_bitacora_ni_otro_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        var primera = Inicio.AddHours(-3);
        escenario.Reloj.Avanzar(primera - Ahora);

        var primero = await CancelarAsync(escenario, cita);
        var antes = await ConteosAsync(escenario);

        // Ya pasado el inicio: el segundo envío no vuelve a decidir, devuelve lo guardado.
        escenario.Reloj.Avanzar(TimeSpan.FromHours(4));
        var segundo = await CancelarAsync(escenario, cita);

        Assert.True(primero.EsExito, $"{primero.Forma} {primero.Codigo}");
        Assert.True(segundo.EsExito, $"{segundo.Forma} {segundo.Codigo}");
        Assert.Equal(primero.Valor, segundo.Valor);
        Assert.Equal(antes, await ConteosAsync(escenario));
        Assert.Single(await BitacoraAsync(escenario, "Cancelada"));
        Assert.Single(await EventosCanceladaAsync(escenario, cita));
        Assert.Equal(primera, (await EstadoGuardadoAsync(escenario)).CanceladaEn);
    }

    [Theory]
    [InlineData("NoSePresento", null)]
    [InlineData("Cancelada", "paciente")]
    [InlineData("Cancelada", "tarde")]
    public async Task Cancelar_una_Cita_que_no_se_presento_o_que_cancelo_el_Paciente_vuelve_cita_no_agendada(
        string estado, string? motivo)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        await escenario.EjecutarAsync(motivo is null
            ? $"update agenda.cita set estado = '{estado}'"
            : $"update agenda.cita set estado = '{estado}', motivo_de_cancelacion = '{motivo}', cancelada_en = now()");
        var antes = await ConteosAsync(escenario);

        var resultado = await CancelarAsync(escenario, cita);

        Assert.Equal(CodigosDeAgenda.CitaNoAgendada, resultado.Codigo);
        Assert.Equal((estado, motivo), await EstadoYMotivoAsync(escenario));
        Assert.Equal(antes, await ConteosAsync(escenario));
    }

    [Fact]
    public async Task Tras_la_entrega_sale_la_disculpa_con_Elegir_otro_horario_y_el_Recordatorio_ya_no_existe()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // El miércoles: la de mañana ya no lleva Recordatorio del día anterior.
        var agendada = await escenario.AgendarAsync(Comando(new DateOnly(2026, 11, 4), new TimeOnly(16, 0)));
        Assert.True(agendada.EsExito, $"{agendada.Forma} {agendada.Codigo}");
        var cita = agendada.Valor.CitaId;
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        Assert.Equal(1L, await RecordatoriosAsync(escenario));
        var enviadosAntes = Arnes.Canal(escenario.Web).Enviados.Count;

        Assert.True((await CancelarAsync(escenario, cita)).EsExito);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var disculpa = Assert.Single(Arnes.Canal(escenario.Web).Enviados.Skip(enviadosAntes));
        Assert.Equal("ana@correo.com", disculpa.Destinatario);
        Assert.Equal("Tuvimos que cancelar tu cita", disculpa.Asunto);
        Assert.Contains("Elegir otro horario", WebUtility.HtmlDecode(disculpa.Html), StringComparison.Ordinal);
        Assert.Contains("Elegir otro horario", disculpa.Texto, StringComparison.Ordinal);
        Assert.Equal(0L, await RecordatoriosAsync(escenario));
    }

    [Fact]
    public async Task Un_Paciente_sin_correo_queda_cancelado_y_no_sale_correo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando();
        comando.Correo = null;
        var agendada = await escenario.AgendarAsync(comando);
        Assert.True(agendada.EsExito, $"{agendada.Forma} {agendada.Codigo}");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.True((await CancelarAsync(escenario, agendada.Valor.CitaId)).EsExito);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Equal(("Cancelada", "consultorio"), await EstadoYMotivoAsync(escenario));
        Assert.Empty(Arnes.Canal(escenario.Web).Enviados);
        Assert.All(
            await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, agendada.Valor.CitaId.Valor, Cancelacion),
            e => Assert.Equal("Entregado", e.Estado));
    }

    [Fact]
    public async Task El_horario_cancelado_por_el_Consultorio_vuelve_a_ofrecerse()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        Assert.DoesNotContain(new TimeOnly(16, 0), await HorasDelMartesAsync(escenario));

        Assert.True((await CancelarAsync(escenario, cita)).EsExito);

        Assert.Contains(new TimeOnly(16, 0), await HorasDelMartesAsync(escenario));
    }

    [Fact]
    public async Task La_Agenda_muestra_Cancelada_por_el_consultorio_y_No_se_presento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cancelada = await AgendarAsync(escenario);
        var otra = Comando(Manana, new TimeOnly(17, 0));
        otra.Telefono = "444 555 1000";
        var marcada = (await escenario.AgendarAsync(otra)).Valor.CitaId;

        Assert.True((await CancelarAsync(escenario, cancelada)).EsExito);
        escenario.Reloj.Avanzar(HoraLocal.AUtc(Manana, new TimeOnly(17, 10)) - Ahora);
        Assert.True((await MarcarAsync(escenario, marcada)).EsExito);

        var citas = Assert.Single(await DiasAsync(escenario, Manana)).Citas;
        var laCancelada = Assert.Single(citas, c => c.CitaId == cancelada);
        Assert.Equal(EstadoEnLaAgenda.Cancelada, laCancelada.Estado);
        Assert.Equal(CancelacionEnLaAgenda.PorElConsultorio, laCancelada.Cancelacion);
        var laMarcada = Assert.Single(citas, c => c.CitaId == marcada);
        Assert.Equal(EstadoEnLaAgenda.NoSePresento, laMarcada.Estado);
        Assert.Null(laMarcada.Cancelacion);
    }

    // Lo que valen para los dos.

    [Fact]
    public async Task Una_Cita_que_no_existe_vuelve_cita_no_encontrada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAsync(escenario);
        var antes = await ConteosAsync(escenario);

        Assert.Equal(CodigosDeAgenda.CitaNoEncontrada, (await CancelarAsync(escenario, CitaId.Nuevo())).Codigo);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(10) - Ahora);
        Assert.Equal(CodigosDeAgenda.CitaNoEncontrada, (await MarcarAsync(escenario, CitaId.Nuevo())).Codigo);
        await SinCambiosAsync(escenario, antes);
    }

    [Fact]
    public async Task Sin_CitaId_es_invalido_por_su_campo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var cancelar = await EnviarAsync<CancelarCitaPorConsultorio, CitaCanceladaPorConsultorio>(escenario, new(), ComoAdministrador);
        var marcar = await EnviarAsync<MarcarNoSePresento, CitaMarcadaNoSePresento>(escenario, new(), ComoAdministrador);

        Assert.Equal(FormaDeResultado.Invalido, cancelar.Forma);
        Assert.Contains(nameof(CancelarCitaPorConsultorio.CitaId), cancelar.Errores.Keys);
        Assert.Equal(FormaDeResultado.Invalido, marcar.Forma);
        Assert.Contains(nameof(MarcarNoSePresento.CitaId), marcar.Errores.Keys);
    }

    [Fact]
    public async Task Sin_sesion_vuelve_no_autorizado_y_el_handler_no_corre()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        var antes = await ConteosAsync(escenario);

        var cancelar = await EnviarAsync<CancelarCitaPorConsultorio, CitaCanceladaPorConsultorio>(
            escenario, new() { CitaId = cita }, SinSesion);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(10) - Ahora);
        var marcar = await EnviarAsync<MarcarNoSePresento, CitaMarcadaNoSePresento>(
            escenario, new() { CitaId = cita }, SinSesion);

        Assert.Equal(FormaDeResultado.NoAutorizado, cancelar.Forma);
        Assert.Equal(FormaDeResultado.NoAutorizado, marcar.Forma);
        await SinCambiosAsync(escenario, antes);
    }

    [Fact]
    public async Task Marcar_espera_a_quien_tiene_bloqueada_la_fila_y_ve_la_Cita_ya_cancelada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(10) - Ahora);

        // Otra transacción tiene la fila y la cancela, como lo haría una cancelación en curso.
        await using var conexion = new NpgsqlConnection(escenario.Cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var otra = await conexion.BeginTransactionAsync(Cancelacion);
        await EjecutarEnAsync(conexion, otra, $"select id from agenda.cita where id = '{cita.Valor}' for update");
        await EjecutarEnAsync(
            conexion, otra,
            "update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'consultorio', cancelada_en = now()");

        var marcar = MarcarAsync(escenario, cita);
        await Task.Delay(TimeSpan.FromMilliseconds(500), Cancelacion);
        Assert.False(marcar.IsCompleted);

        await otra.CommitAsync(Cancelacion);

        Assert.Equal(CodigosDeAgenda.CitaNoAgendada, (await marcar).Codigo);
        Assert.Equal(("Cancelada", "consultorio"), await EstadoYMotivoAsync(escenario));
        Assert.Empty(await BitacoraAsync(escenario, "NoSePresento"));
    }

    [Fact]
    public async Task Cancelar_espera_a_quien_tiene_bloqueada_la_fila_y_ve_la_Cita_ya_marcada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        var outboxAntes = await OutboxAsync(escenario);

        // Otra transacción tiene la fila y la marca No se presentó, como lo haría una marca en curso.
        await using var conexion = new NpgsqlConnection(escenario.Cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var otra = await conexion.BeginTransactionAsync(Cancelacion);
        await EjecutarEnAsync(conexion, otra, $"select id from agenda.cita where id = '{cita.Valor}' for update");
        await EjecutarEnAsync(conexion, otra, "update agenda.cita set estado = 'NoSePresento'");

        var cancelar = CancelarAsync(escenario, cita);
        await Task.Delay(TimeSpan.FromMilliseconds(500), Cancelacion);
        Assert.False(cancelar.IsCompleted);

        await otra.CommitAsync(Cancelacion);

        Assert.Equal(CodigosDeAgenda.CitaNoAgendada, (await cancelar).Codigo);
        Assert.Equal(("NoSePresento", (string?)null), await EstadoYMotivoAsync(escenario));
        Assert.Empty(await BitacoraAsync(escenario, "Cancelada"));
        Assert.Equal(outboxAntes, await OutboxAsync(escenario));
    }

    [Fact]
    public async Task Cancelar_por_el_Consultorio_y_por_el_Paciente_a_la_vez_dejan_una_sola_cancelacion()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await escenario.AgendarAsync(Comando(Manana, new TimeOnly(16, 0)));
        Assert.True(agendada.EsExito);
        escenario.Reloj.Avanzar(Inicio.AddHours(-3) - Ahora);

        var consultorio = CancelarAsync(escenario, agendada.Valor.CitaId);
        var paciente = CancelarPorElPacienteAsync(escenario, agendada.Valor);
        await Task.WhenAll(consultorio, paciente);

        // El Paciente siempre ve éxito con el motivo guardado; el Consultorio, éxito si ganó o cita no agendada si no.
        Assert.True((await paciente).EsExito);
        Assert.Single(await BitacoraAsync(escenario, "Cancelada"));
        Assert.Single(await EventosCanceladaAsync(escenario, agendada.Valor.CitaId));
        if ((await consultorio).EsExito)
        {
            Assert.True((await paciente).Valor.PorElConsultorio);
            Assert.Equal(("Cancelada", "consultorio"), await EstadoYMotivoAsync(escenario));
        }
        else
        {
            Assert.Equal(CodigosDeAgenda.CitaNoAgendada, (await consultorio).Codigo);
            Assert.Equal(("Cancelada", "paciente"), await EstadoYMotivoAsync(escenario));
        }
    }

    [Fact]
    public async Task Dos_marcas_a_la_vez_dejan_una_sola_bitacora()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddMinutes(10) - Ahora);

        var resultados = await Task.WhenAll(MarcarAsync(escenario, cita), MarcarAsync(escenario, cita));

        Assert.All(resultados, r => Assert.True(r.EsExito, $"{r.Forma} {r.Codigo}"));
        Assert.Single(await BitacoraAsync(escenario, "NoSePresento"));
    }

    [Fact]
    public async Task En_el_mismo_circuito_un_cambio_hecho_por_otro_ambito_se_ve_y_no_se_cancela_dos_veces()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // Un solo ámbito, como el circuito del Administrador: agenda la Cita (queda rastreada en su contexto).
        await using var circuito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(circuito);
        var agendada = await circuito.ServiceProvider
            .GetRequiredService<ICommandHandler<AgendarCitaPorConsultorio, CitaAgendadaPorConsultorio>>()
            .HandleAsync(
                new AgendarCitaPorConsultorio
                {
                    TipoDeVisitaId = TiposDeVisitaSembrados.PrimeraConsulta,
                    Fecha = Manana,
                    Hora = new TimeOnly(16, 0),
                    Nombre = "Ana Sofía López",
                    Telefono = "444 123 4567",
                    Correo = "ana@correo.com",
                    Consentimiento = true,
                },
                Cancelacion);
        Assert.True(agendada.EsExito, $"{agendada.Forma} {agendada.Codigo}");
        var cita = agendada.Valor.CitaId;

        // El Paciente la cancela desde otro ámbito, con su Enlace de gestión.
        var token = EnlaceDeGestion.Derivar(escenario.Web.Services.GetRequiredService<ClaveDeGestion>(), cita).Token;
        var delPaciente = await EnviarAsync<CancelarCitaPorEnlace, CitaCanceladaPorEnlace>(escenario, new() { Token = token }, SinSesion);
        Assert.True(delPaciente.EsExito, $"{delPaciente.Forma} {delPaciente.Codigo}");

        // El circuito cancela la misma Cita: lee lo guardado, no la que tenía rastreada.
        var delConsultorio = await circuito.ServiceProvider
            .GetRequiredService<ICommandHandler<CancelarCitaPorConsultorio, CitaCanceladaPorConsultorio>>()
            .HandleAsync(new CancelarCitaPorConsultorio { CitaId = cita }, Cancelacion);

        Assert.Equal(CodigosDeAgenda.CitaNoAgendada, delConsultorio.Codigo);
        Assert.Equal(("Cancelada", "paciente"), await EstadoYMotivoAsync(escenario));
        Assert.Single(await EventosCanceladaAsync(escenario, cita));
        Assert.Single(await BitacoraAsync(escenario, "Cancelada"));
    }

    private static async Task<CitaId> AgendarAsync(Escenario escenario)
    {
        var resultado = await escenario.AgendarAsync(Comando(Manana, new TimeOnly(16, 0)));
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor.CitaId;
    }

    private static Task<Resultado<CitaMarcadaNoSePresento>> MarcarAsync(Escenario escenario, CitaId cita) =>
        EnviarAsync<MarcarNoSePresento, CitaMarcadaNoSePresento>(escenario, new() { CitaId = cita }, ComoAdministrador);

    private static Task<Resultado<CitaCanceladaPorConsultorio>> CancelarAsync(Escenario escenario, CitaId cita) =>
        EnviarAsync<CancelarCitaPorConsultorio, CitaCanceladaPorConsultorio>(escenario, new() { CitaId = cita }, ComoAdministrador);

    /// <summary>El handler por su interfaz, en un ámbito propio con la sesión que se pida, como lo pedirá la página.</summary>
    private static async Task<Resultado<TResultado>> EnviarAsync<TComando, TResultado>(
        Escenario escenario, TComando comando, Action<AsyncServiceScope> sesion)
        where TComando : ICommand<TResultado>
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        sesion(ambito);
        return await ambito.ServiceProvider
            .GetRequiredService<ICommandHandler<TComando, TResultado>>()
            .HandleAsync(comando, Cancelacion);
    }

    private static async Task<Resultado<CitaCanceladaPorEnlace>> CancelarPorElPacienteAsync(Escenario escenario, CitaAgendadaEnLinea cita)
    {
        var token = cita.EnlaceDeGestion[(cita.EnlaceDeGestion.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];
        return await EnviarAsync<CancelarCitaPorEnlace, CitaCanceladaPorEnlace>(escenario, new() { Token = token }, SinSesion);
    }

    private static void ComoAdministrador(AsyncServiceScope ambito) =>
        ((IHostEnvironmentAuthenticationStateProvider)ambito.ServiceProvider.GetRequiredService<AuthenticationStateProvider>())
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, IdDelAdministrador), new Claim(ClaimTypes.Name, "administradora")],
                authenticationType: "Prueba")))));

    private static async Task EjecutarEnAsync(NpgsqlConnection conexion, NpgsqlTransaction transaccion, string sql)
    {
        await using var comando = new NpgsqlCommand(sql, conexion, transaccion);
        await comando.ExecuteNonQueryAsync(Cancelacion);
    }

    // Las horas libres de Primera consulta el martes 3.
    private static async Task<IReadOnlyList<TimeOnly>> HorasDelMartesAsync(Escenario escenario)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>>()
            .HandleAsync(new HorariosLibres(TiposDeVisitaSembrados.PrimeraConsulta), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return Assert.Single(resultado.Valor, d => d.Fecha == Manana).Horas;
    }

    private static async Task<IReadOnlyList<DiaDeLaAgenda>> DiasAsync(Escenario escenario, DateOnly fecha)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<IQueryHandler<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>>()
            .HandleAsync(DiasDeLaAgenda.DelDia(fecha), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static async Task<(string Estado, string? Motivo, DateTimeOffset? CanceladaEn)> EstadoGuardadoAsync(Escenario escenario) =>
        Assert.Single(await escenario.FilasAsync(
            "select estado, motivo_de_cancelacion, cancelada_en from agenda.cita",
            l => (l.GetString(0), l.IsDBNull(1) ? null : l.GetString(1),
                  l.IsDBNull(2) ? (DateTimeOffset?)null : l.GetFieldValue<DateTimeOffset>(2))));

    private static async Task<(string Estado, string? Motivo)> EstadoYMotivoAsync(Escenario escenario)
    {
        var (estado, motivo, _) = await EstadoGuardadoAsync(escenario);
        return (estado, motivo);
    }

    private static Task<long> OutboxAsync(Escenario escenario) =>
        escenario.EscalarAsync<long>("select count(*) from agenda.outbox");

    private static Task<long> RecordatoriosAsync(Escenario escenario) =>
        escenario.EscalarAsync<long>("select count(*) from notificaciones.recordatorio_programado");

    // Filas de la bitácora y del outbox, todas.
    private static async Task<(long Bitacora, long Outbox)> ConteosAsync(Escenario escenario) =>
        (await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"), await OutboxAsync(escenario));

    // La Cita sigue Agendada el martes a las 16:00 y la bitácora y el outbox tienen las mismas filas que antes.
    private static async Task SinCambiosAsync(Escenario escenario, (long Bitacora, long Outbox) antes)
    {
        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal("Agendada", cita.Estado);
        Assert.Equal(Inicio, cita.Inicio);
        Assert.Equal(antes, await ConteosAsync(escenario));
    }

    private static Task<List<EntradaLeida>> BitacoraAsync(Escenario escenario, string accion) => escenario.FilasAsync(
        "select ocurrido_en, actor_tipo, actor_id, entidad, entidad_id, detalle::text from agenda.bitacora "
        + $"where accion = '{accion}' order by id",
        l => new EntradaLeida(
            l.GetFieldValue<DateTimeOffset>(0), l.GetString(1), l.IsDBNull(2) ? null : l.GetString(2),
            l.GetString(3), l.GetString(4), l.GetString(5)));

    private static async Task<List<FilaLeidaDeOutbox>> EventosCanceladaAsync(Escenario escenario, CitaId cita) =>
        [.. (await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, cita.Valor, Cancelacion))
            .Where(f => f.Tipo == nameof(CitaCancelada))];

    private sealed record EntradaLeida(
        DateTimeOffset OcurridoEn, string ActorTipo, string? ActorId, string Entidad, string EntidadId, string Detalle);
}
