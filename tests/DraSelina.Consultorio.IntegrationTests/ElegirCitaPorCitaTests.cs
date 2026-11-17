using System.Security.Claims;
using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Guardar y borrar una Agenda custom eligiendo Cita por Cita (CAP-11, RN-9, RN-10, AD-6; entrada 6.2) contra PostgreSQL
/// real: la lista vista que debe coincidir, el evento interno en el outbox con la custom (T-30), y la política P-1 que,
/// al entregarse, cancela cada elegida a nombre del Administrador (T-31) con la disculpa, de forma idempotente (T-29).
/// Con el escenario y el reloj de <see cref="ReservaEnLineaTests"/>: lunes 2 de noviembre de 2026 a las 12:00 local. El
/// sábado 7 abre de 10:00 a 1:00 por una custom sembrada, con cuatro Citas de Ana: 10:00, 10:30, 11:00 y 12:30.
/// </summary>
public sealed class ElegirCitaPorCitaTests(Arnes arnes)
{
    private const string IdDelAdministrador = "cuenta-del-administrador";

    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Cerrar_el_sabado_cancelando_tres_y_conservando_la_de_las_12_30()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var citas = await SabadoConCuatroCitasAsync(escenario);

        var resultado = await GuardarAsync(escenario, Cerrar(citas, 3));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.False(resultado.Valor.Registrada);
        // RN-10: guardar la custom no toca ninguna Cita; las cancela después P-1.
        Assert.All(await escenario.CitasAsync(), c => Assert.Equal("Agendada", c.Estado));
        var evento = Assert.Single(await EventosDeCustomAsync(escenario));
        Assert.Equal(nameof(AgendaCustomFueCambiada), evento.Tipo);

        await EntregarTodoAsync(escenario);

        var despues = await escenario.CitasAsync();
        Assert.Equal(["Cancelada", "Cancelada", "Cancelada", "Agendada"], despues.Select(c => c.Estado));
        Assert.Equal(3L, await escenario.EscalarAsync<long>(
            "select count(*) from agenda.cita where motivo_de_cancelacion = 'consultorio'"));

        var bitacora = await BitacoraDeCitasAsync(escenario);
        Assert.Equal(3, bitacora.Count);
        Assert.All(bitacora, b => Assert.Equal(("Administrador", IdDelAdministrador, "Cancelada"), (b.ActorTipo, b.ActorId, b.Accion)));
        Assert.DoesNotContain(bitacora, b => b.EntidadId == citas[3].ToString());

        var canceladas = await CitasCanceladasAsync(escenario);
        Assert.Equal(3, canceladas.Count);
        Assert.All(canceladas, c => Assert.Equal((Actor.Administrador, CitaCancelada.MotivoConsultorio), (c.Actor, c.Motivo)));

        // La disculpa a Ana, una por Cita cancelada; ninguna por la de las 12:30.
        Assert.Equal(3, Arnes.Canal(escenario.Web).Enviados.Count);
        Assert.All(Arnes.Canal(escenario.Web).Enviados, m => Assert.Equal("ana@correo.com", m.Destinatario));
    }

    [Fact]
    public async Task La_custom_deja_su_bitacora_con_las_elegidas_y_el_evento_lleva_al_Administrador()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var citas = await SabadoConCuatroCitasAsync(escenario);

        Assert.True((await GuardarAsync(escenario, Cerrar(citas, 3))).EsExito);

        var entrada = Assert.Single(await escenario.FilasAsync(
            "select accion, detalle::text from agenda.bitacora where entidad = 'AgendaCustom'",
            l => (Accion: l.GetString(0), Detalle: l.GetString(1))));
        Assert.Equal("Cambiada", entrada.Accion);
        using (var detalle = JsonDocument.Parse(entrada.Detalle))
        {
            Assert.Equal(
                citas.Take(3).Select(c => c.Valor),
                detalle.RootElement.GetProperty("citasACancelar").EnumerateArray().Select(e => e.GetGuid()));
        }

        var fila = Assert.Single(await EventosDeCustomAsync(escenario));
        var evento = JsonSerializer.Deserialize<AgendaCustomFueCambiada>(fila.Contenido)!;
        Assert.Equal(Sabado, evento.Fecha);
        Assert.Equal(citas.Take(3), evento.CitasACancelar);
        Assert.Equal(Actor.Administrador, evento.Actor);
        Assert.Equal(IdDelAdministrador, evento.ActorId);
        Assert.DoesNotContain("Ana", fila.Contenido, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Guardar_el_mismo_horario_cancelando_una_conservada_antes_la_cancela()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var citas = await SabadoConCuatroCitasAsync(escenario);
        Assert.True((await GuardarAsync(escenario, Cerrar(citas, 3))).EsExito);
        await EntregarTodoAsync(escenario);

        // El sábado sigue cerrado; ahora la de las 12:30 también se cancela.
        var otraVez = new GuardarAgendaCustom
        {
            Fecha = Sabado,
            Motivo = "asunto personal",
            Citas = [new DecisionSobreCita { CitaId = citas[3], Cancelar = true }],
        };
        var resultado = await GuardarAsync(escenario, otraVez);
        await EntregarTodoAsync(escenario);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.False(resultado.Valor.SinCambios);
        Assert.All(await escenario.CitasAsync(), c => Assert.Equal("Cancelada", c.Estado));

        // Conservándola otra vez, es un guardado sin cambios.
        var conservar = await GuardarAsync(escenario, new GuardarAgendaCustom { Fecha = Sabado, Motivo = "asunto personal" });
        Assert.True(conservar.Valor.SinCambios);
    }

    [Fact]
    public async Task Conservar_todas_guarda_la_custom_sin_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var citas = await SabadoConCuatroCitasAsync(escenario);

        var resultado = await GuardarAsync(escenario, Cerrar(citas, 0, 1, 2, 3));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal("asunto personal", await escenario.EscalarAsync<string>("select motivo from agenda.agenda_custom"));
        Assert.Empty(await EventosDeCustomAsync(escenario));
        await EntregarTodoAsync(escenario);
        Assert.All(await escenario.CitasAsync(), c => Assert.Equal("Agendada", c.Estado));
    }

    [Theory]
    [InlineData("falta")]
    [InlineData("sobra")]
    [InlineData("vacia")]
    public async Task Una_lista_que_no_coincide_devuelve_conflicto_y_no_cambia_nada(string caso)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var citas = await SabadoConCuatroCitasAsync(escenario);
        var comando = Cerrar(citas, 3);
        switch (caso)
        {
            case "falta":
                comando.Citas.RemoveAt(0);
                break;
            case "sobra":
                comando.Citas.Add(new DecisionSobreCita { CitaId = CitaId.Nuevo(), Cancelar = true });
                break;
            default:
                comando.Citas.Clear();
                break;
        }

        var resultado = await GuardarAsync(escenario, comando);

        Assert.Equal(CodigosDeAgenda.CitasFuera, resultado.Codigo);
        Assert.Equal("sábado abierto", await escenario.EscalarAsync<string>("select motivo from agenda.agenda_custom"));
        Assert.Empty(await EventosDeCustomAsync(escenario));
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"));
    }

    [Fact]
    public async Task Una_Cita_que_llega_despues_de_ver_la_lista_devuelve_conflicto()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var citas = await SabadoConCuatroCitasAsync(escenario);
        var comando = Cerrar(citas, 3);
        await escenario.InsertarCitaDelConsultorioAsync(Sabado, new TimeOnly(11, 30), new TimeOnly(12, 0));

        Assert.Equal(CodigosDeAgenda.CitasFuera, (await GuardarAsync(escenario, comando)).Codigo);
    }

    [Fact]
    public async Task Entregar_el_evento_dos_veces_no_cancela_ni_avisa_dos_veces()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var citas = await SabadoConCuatroCitasAsync(escenario);
        Assert.True((await GuardarAsync(escenario, Cerrar(citas, 3))).EsExito);
        await EntregarTodoAsync(escenario);
        var fila = Assert.Single(await EventosDeCustomAsync(escenario));

        await escenario.EjecutarAsync($"update agenda.outbox set estado = 'Pendiente', entregado_en = null where id = '{fila.Id}'");
        await EntregarTodoAsync(escenario);

        Assert.Equal(3, (await BitacoraDeCitasAsync(escenario)).Count);
        Assert.Equal(3, (await CitasCanceladasAsync(escenario)).Count);
        Assert.Equal(3, Arnes.Canal(escenario.Web).Enviados.Count);
    }

    [Fact]
    public async Task La_que_ya_no_esta_Agendada_o_ya_empezo_se_omite_y_las_demas_se_cancelan()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var citas = await SabadoConCuatroCitasAsync(escenario);
        Assert.True((await GuardarAsync(escenario, Cerrar(citas, 3))).EsExito);

        // Antes de la entrega: la de las 10:30 la cancela el Paciente y la de las 11:00 el Administrador por su comando.
        await escenario.EjecutarAsync(
            $"update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now() where id = '{citas[1].Valor}'");
        Assert.True((await CancelarPorConsultorioAsync(escenario, citas[2])).EsExito);
        // Y llega el sábado a las 10:15: la de las 10:00 ya empezó.
        escenario.Reloj.Avanzar(HoraLocal.AUtc(Sabado, new TimeOnly(10, 15)) - escenario.Reloj.UtcNow);

        await EntregarTodoAsync(escenario);

        var despues = await escenario.CitasAsync();
        Assert.Equal(["Agendada", "Cancelada", "Cancelada", "Agendada"], despues.Select(c => c.Estado));
        Assert.Equal("paciente", await escenario.EscalarAsync<string>(
            $"select motivo_de_cancelacion from agenda.cita where id = '{citas[1].Valor}'"));
        // Solo la cancelación del comando: P-1 no dejó ninguna.
        Assert.Equal([citas[2].ToString()], (await BitacoraDeCitasAsync(escenario)).Select(b => b.EntidadId));
    }

    [Fact]
    public async Task Borrar_la_custom_que_abria_el_sabado_pide_la_misma_eleccion()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var citas = await SabadoConCuatroCitasAsync(escenario);

        var sinLista = await BorrarAsync(escenario, new BorrarAgendaCustom { Fecha = Sabado });
        Assert.Equal(CodigosDeAgenda.CitasFuera, sinLista.Codigo);
        Assert.Equal(1L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom"));

        var comando = new BorrarAgendaCustom { Fecha = Sabado, Citas = Decisiones(citas, 1, 2, 3) };
        var resultado = await BorrarAsync(escenario, comando);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom"));
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom_tramo"));
        Assert.Equal(nameof(AgendaCustomFueBorrada), Assert.Single(await EventosDeCustomAsync(escenario)).Tipo);
        var entrada = Assert.Single(await escenario.FilasAsync(
            "select accion, actor_id, detalle::text from agenda.bitacora where entidad = 'AgendaCustom'",
            l => (Accion: l.GetString(0), ActorId: l.GetString(1), Detalle: l.GetString(2))));
        Assert.Equal(("Borrada", IdDelAdministrador), (entrada.Accion, entrada.ActorId));
        Assert.Contains("sábado abierto", entrada.Detalle, StringComparison.Ordinal);

        await EntregarTodoAsync(escenario);

        Assert.Equal(["Cancelada", "Agendada", "Agendada", "Agendada"], (await escenario.CitasAsync()).Select(c => c.Estado));
    }

    [Fact]
    public async Task Borrar_sin_custom_o_una_fecha_pasada_no_cambia_nada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var sinCustom = await BorrarAsync(escenario, new BorrarAgendaCustom { Fecha = Sabado });
        var pasada = await BorrarAsync(escenario, new BorrarAgendaCustom { Fecha = new DateOnly(2026, 11, 1) });

        Assert.Equal(CodigosDeAgenda.FechaSinCustom, sinCustom.Codigo);
        Assert.Equal(CodigosDeAgenda.FechaPasada, pasada.Codigo);
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"));
    }

    [Fact]
    public async Task Sin_sesion_borrar_vuelve_no_autorizado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCustomAsync(Sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(13, 0)));
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);

        var resultado = await ambito.ServiceProvider.GetRequiredService<ICommandHandler<BorrarAgendaCustom, AgendaCustomBorrada>>()
            .HandleAsync(new BorrarAgendaCustom { Fecha = Sabado }, Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.Equal(1L, await escenario.EscalarAsync<long>("select count(*) from agenda.agenda_custom"));
    }

    [Fact]
    public async Task Una_Cita_repetida_o_sin_id_en_la_lista_es_invalida()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var citas = await SabadoConCuatroCitasAsync(escenario);
        var repetida = Cerrar(citas, 3);
        repetida.Citas.Add(new DecisionSobreCita { CitaId = citas[0], Cancelar = false });
        var sinId = new BorrarAgendaCustom { Fecha = Sabado, Citas = [new DecisionSobreCita()] };

        var guardar = await GuardarAsync(escenario, repetida);
        var borrar = await BorrarAsync(escenario, sinId);

        Assert.Equal([DecisionSobreCita.MensajeRepetida], guardar.Errores["Citas[4]"]);
        Assert.Equal([DecisionSobreCita.MensajeSinCita], borrar.Errores["Citas[0]"]);
    }

    // La custom del sábado, 10:00 a 1:00, y cuatro Citas de Ana: 10:00, 10:30, 11:00 y 12:30; sus ids en ese orden.
    private static async Task<IReadOnlyList<CitaId>> SabadoConCuatroCitasAsync(Escenario escenario)
    {
        await escenario.InsertarCustomAsync(Sabado, "sábado abierto", (new TimeOnly(10, 0), new TimeOnly(13, 0)));
        var ana = await escenario.PacienteDeAnaAsync();
        foreach (var (hora, minuto) in new[] { (10, 0), (10, 30), (11, 0), (12, 30) })
        {
            var inicio = new TimeOnly(hora, minuto);
            await escenario.InsertarCitaDelConsultorioAsync(Sabado, inicio, inicio.AddMinutes(30), ana);
        }

        return [.. (await escenario.CitasAsync()).Select(c => new CitaId(c.Id))];
    }

    private static List<DecisionSobreCita> Decisiones(IReadOnlyList<CitaId> citas, params int[] conservar) =>
        [.. citas.Select((id, i) => new DecisionSobreCita { CitaId = id, Cancelar = !conservar.Contains(i) })];

    // Cerrar el sábado (cero Tramos, "asunto personal") con la lista completa; conserva las de esas posiciones.
    private static GuardarAgendaCustom Cerrar(IReadOnlyList<CitaId> citas, params int[] conservar) => new()
    {
        Fecha = Sabado,
        Motivo = "asunto personal",
        Citas = Decisiones(citas, conservar),
    };

    private static void ComoAdministrador(AsyncServiceScope ambito) =>
        Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, IdDelAdministrador), new Claim(ClaimTypes.Name, "administradora")],
            authenticationType: "Prueba")));

    private static async Task<Resultado<T>> ComoAdministradorAsync<TComando, T>(Escenario escenario, TComando comando)
        where TComando : ICommand<T>
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        return await ambito.ServiceProvider.GetRequiredService<ICommandHandler<TComando, T>>().HandleAsync(comando, Cancelacion);
    }

    private static Task<Resultado<AgendaCustomGuardada>> GuardarAsync(Escenario escenario, GuardarAgendaCustom comando) =>
        ComoAdministradorAsync<GuardarAgendaCustom, AgendaCustomGuardada>(escenario, comando);

    private static Task<Resultado<AgendaCustomBorrada>> BorrarAsync(Escenario escenario, BorrarAgendaCustom comando) =>
        ComoAdministradorAsync<BorrarAgendaCustom, AgendaCustomBorrada>(escenario, comando);

    private static Task<Resultado<CitaCanceladaPorConsultorio>> CancelarPorConsultorioAsync(Escenario escenario, CitaId cita) =>
        ComoAdministradorAsync<CancelarCitaPorConsultorio, CitaCanceladaPorConsultorio>(
            escenario, new CancelarCitaPorConsultorio { CitaId = cita });

    // Hasta que no quede nada pendiente: P-1 publica CitaCancelada, que se entrega en la vuelta siguiente.
    private static async Task EntregarTodoAsync(Escenario escenario)
    {
        for (var vuelta = 0; vuelta < 5 && await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion) > 0; vuelta++)
        {
        }
    }

    private static Task<List<(Guid Id, string Tipo, string Contenido)>> EventosDeCustomAsync(Escenario escenario) =>
        escenario.FilasAsync(
            $"select id, tipo, contenido::text from {AgendaDbContext.Esquema}.outbox where tipo like 'AgendaCustomFue%'",
            l => (l.GetGuid(0), l.GetString(1), l.GetString(2)));

    private static async Task<List<CitaCancelada>> CitasCanceladasAsync(Escenario escenario) =>
        [
            .. (await escenario.FilasAsync(
                $"select contenido::text from {AgendaDbContext.Esquema}.outbox where tipo = '{nameof(CitaCancelada)}'",
                l => l.GetString(0)))
            .Select(c => JsonSerializer.Deserialize<CitaCancelada>(c)!),
        ];

    private static Task<List<(string ActorTipo, string? ActorId, string EntidadId, string Accion)>> BitacoraDeCitasAsync(Escenario escenario) =>
        escenario.FilasAsync(
            "select actor_tipo, actor_id, entidad_id, accion from agenda.bitacora where entidad = 'Cita' order by ocurrido_en, id",
            l => (l.GetString(0), l.IsDBNull(1) ? null : l.GetString(1), l.GetString(2), l.GetString(3)));
}
