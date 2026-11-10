using System.Security.Claims;
using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// <see cref="AgendarCitaPorConsultorio"/> por su handler contra PostgreSQL real (CAP-13, FR-15, AD-7): la sesión del
/// Administrador, la nota fuera de horario (INV-C6), el traslape de la base (AD-8), la bitácora con el id del
/// Administrador (RN-24), <see cref="CitaAgendada"/> con origen consultorio, la confirmación y el Recordatorio de
/// Notificaciones, <see cref="HorariosLibres"/> y <see cref="DiasDeLaAgenda"/>. Con el escenario y el reloj de
/// <see cref="ReservaEnLineaTests"/>: lunes 2 de noviembre de 2026 a las 12:00 local; base sembrada de lunes a viernes,
/// 3:30 a 6:00 pm.
/// </summary>
public sealed class AgendarCitaPorConsultorioTests(Arnes arnes)
{
    private const string IdDelAdministrador = "cuenta-del-administrador";

    private static readonly DateOnly Hoy = new(2026, 11, 2);

    private static readonly DateOnly Miercoles = new(2026, 11, 4);

    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Dentro_de_horario_y_sin_nota_guarda_la_Cita_del_consultorio_con_su_Consentimiento_externo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await AgendarAsync(escenario, Comando(Manana, new TimeOnly(16, 0)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(Manana, resultado.Valor.Fecha);
        Assert.Equal(new TimeOnly(16, 0), resultado.Valor.Hora);
        Assert.False(resultado.Valor.FueraDeHorario);

        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(resultado.Valor.CitaId.Valor, cita.Id);
        Assert.Equal("Agendada", cita.Estado);
        Assert.Equal("consultorio", cita.Origen);
        Assert.Equal(HoraLocal.AUtc(Manana, new TimeOnly(16, 30)), cita.Fin);
        Assert.Null(cita.MotivoDeConsulta);
        Assert.Equal(Ahora, cita.ConsentimientoEn);
        Assert.Equal("2026-11-borrador", cita.ConsentimientoVersion);
        Assert.Equal("externo", cita.ConsentimientoCanal);
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.cita where solicitud_id is not null or nota is not null"));
        Assert.Equal(32, await escenario.EscalarAsync<int>("select octet_length(token_hash) from agenda.cita"));
    }

    [Fact]
    public async Task Fuera_de_la_rejilla_se_guarda()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await AgendarAsync(escenario, Comando(Manana, new TimeOnly(16, 10)));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(HoraLocal.AUtc(Manana, new TimeOnly(16, 10)), Assert.Single(await escenario.CitasAsync()).Inicio);
    }

    [Fact]
    public async Task Un_sabado_cerrado_sin_nota_pide_la_nota_y_no_guarda_nada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await AgendarAsync(escenario, Comando(Sabado, new TimeOnly(10, 15), nota: "  "));

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.NotaObligatoria, resultado.Codigo);
        Assert.Empty(await escenario.CitasAsync());
        Assert.Equal(0L, await BitacoraYOutboxAsync(escenario));
    }

    [Fact]
    public async Task Un_sabado_cerrado_con_nota_se_guarda_y_la_Agenda_lo_marca_fuera_de_horario_con_su_nota()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await AgendarAsync(escenario, Comando(Sabado, new TimeOnly(10, 15), nota: "lo pidió Selina"));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.True(resultado.Valor.FueraDeHorario);
        Assert.Equal("lo pidió Selina", await escenario.EscalarAsync<string>("select nota from agenda.cita"));

        // RN-14: una Cita del consultorio no abre disponibilidad.
        Assert.DoesNotContain(await HorariosAsync(escenario), d => d.Fecha == Sabado);

        var dia = Assert.Single(await DiasAsync(escenario, Sabado));
        var cita = Assert.Single(dia.Citas);
        Assert.Equal(resultado.Valor.CitaId, cita.CitaId);
        Assert.True(cita.FueraDeHorario);
        Assert.Equal("lo pidió Selina", cita.Nota);
    }

    [Fact]
    public async Task Una_Cita_que_cabe_a_medias_en_el_Tramo_pide_la_nota()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await AgendarAsync(escenario, Comando(Manana, new TimeOnly(17, 45)));

        Assert.Equal(CodigosDeAgenda.NotaObligatoria, resultado.Codigo);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Hoy_mas_tarde_fuera_de_la_ventana_con_nota_se_guarda()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await AgendarAsync(escenario, Comando(Hoy, new TimeOnly(19, 0), nota: "Urgencia"));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.True(resultado.Valor.FueraDeHorario);
    }

    [Fact]
    public async Task Una_hora_antes_de_ahora_esta_en_el_pasado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await AgendarAsync(escenario, Comando(Hoy, new TimeOnly(11, 0), nota: "nota"));

        Assert.Equal(CodigosDeAgenda.EnElPasado, resultado.Codigo);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Un_traslape_vuelve_horario_ocupado_sin_Cita_bitacora_ni_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(16, 0), new TimeOnly(16, 30));

        var resultado = await AgendarAsync(escenario, Comando(Manana, new TimeOnly(16, 15)));

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAgenda.HorarioOcupado, resultado.Codigo);
        Assert.Single(await escenario.CitasAsync());
        Assert.Equal(0L, await BitacoraYOutboxAsync(escenario));
    }

    [Fact]
    public async Task Sin_consentimiento_es_invalido_por_su_campo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando(Manana, new TimeOnly(16, 0));
        comando.Consentimiento = false;

        var resultado = await AgendarAsync(escenario, comando);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.True(resultado.Errores.ContainsKey(nameof(AgendarCitaPorConsultorio.Consentimiento)));
        Assert.Empty(await escenario.CitasAsync());
        Assert.Empty(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task Un_tipo_activo_que_no_se_reserva_en_linea_se_agenda_con_su_duracion()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var tipo = await InsertarTipoAsync(escenario, activo: true);
        var comando = Comando(Manana, new TimeOnly(16, 0), nota: "Solo por teléfono");
        comando.TipoDeVisitaId = tipo;

        var resultado = await AgendarAsync(escenario, comando);

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(tipo.Valor, cita.TipoDeVisitaId);
        Assert.Equal(TimeSpan.FromMinutes(60), cita.Fin - cita.Inicio);
    }

    [Fact]
    public async Task Un_tipo_inactivo_o_inexistente_no_se_agenda()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var inactivo = Comando(Manana, new TimeOnly(16, 0));
        inactivo.TipoDeVisitaId = await InsertarTipoAsync(escenario, activo: false);
        var inexistente = Comando(Manana, new TimeOnly(16, 0));
        inexistente.TipoDeVisitaId = TipoDeVisitaId.Nuevo();

        Assert.Equal(CodigosDeAgenda.TipoNoReservable, (await AgendarAsync(escenario, inactivo)).Codigo);
        Assert.Equal(CodigosDeAgenda.TipoNoReservable, (await AgendarAsync(escenario, inexistente)).Codigo);
        Assert.Empty(await escenario.CitasAsync());
    }

    [Fact]
    public async Task Sin_sesion_vuelve_no_autorizado_y_el_handler_no_corre()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);

        var resultado = await Handler(ambito).HandleAsync(Comando(Manana, new TimeOnly(16, 0)), Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, resultado.Forma);
        Assert.Empty(await escenario.CitasAsync());
        Assert.Empty(await escenario.PacientesAsync());
    }

    [Fact]
    public async Task El_mismo_telefono_y_nombre_es_el_Paciente_guardado_y_su_correo_nuevo_reemplaza_al_anterior()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteDeAnaAsync();
        var comando = Comando(Manana, new TimeOnly(16, 0));
        comando.Correo = "ana.nueva@correo.com";

        Assert.True((await AgendarAsync(escenario, comando)).EsExito);

        var paciente = Assert.Single(await escenario.PacientesAsync());
        Assert.Equal(ana, paciente.Id);
        Assert.Equal(ana, Assert.Single(await escenario.CitasAsync()).PacienteId);
        Assert.Equal("ana.nueva@correo.com", paciente.Correo);
    }

    [Fact]
    public async Task Deja_la_bitacora_a_nombre_del_Administrador_y_CitaAgendada_con_origen_consultorio()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando(Sabado, new TimeOnly(10, 15), nota: "Pidió por WhatsApp");
        comando.Motivo = "Dolor de rodilla derecha";

        Assert.True((await AgendarAsync(escenario, comando)).EsExito);

        var cita = Assert.Single(await escenario.CitasAsync());
        var entrada = Assert.Single(await escenario.FilasAsync(
            "select actor_tipo, actor_id, entidad, entidad_id, accion, detalle::text from agenda.bitacora",
            l => (ActorTipo: l.GetString(0), ActorId: l.IsDBNull(1) ? null : l.GetString(1), Entidad: l.GetString(2),
                EntidadId: l.GetString(3), Accion: l.GetString(4), Detalle: l.GetString(5))));
        Assert.Equal("Administrador", entrada.ActorTipo);
        Assert.Equal(IdDelAdministrador, entrada.ActorId);
        Assert.Equal("Cita", entrada.Entidad);
        Assert.Equal(cita.Id.ToString(), entrada.EntidadId);
        Assert.Equal("Agendada", entrada.Accion);

        using (var detalle = JsonDocument.Parse(entrada.Detalle))
        {
            var raiz = detalle.RootElement;
            Assert.Equal(cita.Inicio, raiz.GetProperty("inicio").GetDateTimeOffset());
            Assert.Equal(cita.Fin, raiz.GetProperty("fin").GetDateTimeOffset());
            Assert.Equal(TiposDeVisitaSembrados.PrimeraConsulta.Valor, raiz.GetProperty("tipoDeVisitaId").GetGuid());
            Assert.Equal("consultorio", raiz.GetProperty("origen").GetString());
            Assert.True(raiz.GetProperty("fueraDeHorario").GetBoolean());
        }

        var fila = Assert.Single(await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, cita.Id, Cancelacion));
        Assert.Equal(nameof(CitaAgendada), fila.Tipo);
        var evento = JsonSerializer.Deserialize<CitaAgendada>(fila.Contenido)!;
        Assert.Equal(
            new CitaAgendada(fila.Id, Ahora, new CitaId(cita.Id), new PatientId(cita.PacienteId), Actor.Administrador, cita.Inicio, cita.Fin, "consultorio"),
            evento);

        // AD-23: ni motivo, ni nota, ni contacto.
        foreach (var texto in new[] { entrada.Detalle, fila.Contenido })
        {
            foreach (var dato in new[] { "rodilla", "WhatsApp", "Ana", "4441234567", "ana@correo.com" })
            {
                Assert.DoesNotContain(dato, texto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task Con_correo_sale_la_confirmacion_y_se_programa_el_Recordatorio()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await AgendarAsync(escenario, Comando(Miercoles, new TimeOnly(16, 0)))).EsExito);
        var cita = Assert.Single(await escenario.CitasAsync());

        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var correo = Assert.Single(Arnes.Canal(escenario.Web).Enviados);
        Assert.Equal("ana@correo.com", correo.Destinatario);
        Assert.Equal(
            cita.Id,
            await escenario.EscalarAsync<Guid>("select cita_id from notificaciones.recordatorio_programado"));
    }

    [Fact]
    public async Task Ocupa_su_horario_publico_sin_ofrecer_nada_nuevo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var antes = Assert.Single(await HorariosAsync(escenario), d => d.Fecha == Manana).Horas;

        Assert.True((await AgendarAsync(escenario, Comando(Manana, new TimeOnly(16, 0)))).EsExito);

        var martes = Assert.Single(await HorariosAsync(escenario), d => d.Fecha == Manana).Horas;
        Assert.Equal([new(15, 30), new(16, 30), new(17, 0), new(17, 30)], martes);
        Assert.All(martes, hora => Assert.Contains(hora, antes));
    }

    private static AgendarCitaPorConsultorio Comando(DateOnly fecha, TimeOnly hora, string? nota = null) => new()
    {
        TipoDeVisitaId = TiposDeVisitaSembrados.PrimeraConsulta,
        Fecha = fecha,
        Hora = hora,
        Nombre = "Ana Sofía López",
        Telefono = "444 123 4567",
        Correo = "ana@correo.com",
        Nota = nota,
        Consentimiento = true,
    };

    private static ICommandHandler<AgendarCitaPorConsultorio, CitaAgendadaPorConsultorio> Handler(AsyncServiceScope ambito) =>
        ambito.ServiceProvider.GetRequiredService<ICommandHandler<AgendarCitaPorConsultorio, CitaAgendadaPorConsultorio>>();

    private static void ComoAdministrador(AsyncServiceScope ambito) =>
        ((IHostEnvironmentAuthenticationStateProvider)ambito.ServiceProvider.GetRequiredService<AuthenticationStateProvider>())
            .SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, IdDelAdministrador), new Claim(ClaimTypes.Name, "administradora")],
                authenticationType: "Prueba")))));

    /// <summary>El handler en un ámbito propio con la sesión del Administrador, como lo pedirá la página.</summary>
    private static async Task<Resultado<CitaAgendadaPorConsultorio>> AgendarAsync(Escenario escenario, AgendarCitaPorConsultorio comando)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        return await Handler(ambito).HandleAsync(comando, Cancelacion);
    }

    private static async Task<IReadOnlyList<DiaConHorarios>> HorariosAsync(Escenario escenario)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>>()
            .HandleAsync(new HorariosLibres(TiposDeVisitaSembrados.PrimeraConsulta), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
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

    /// <summary>Un tipo de visita de 60 minutos que no se reserva en línea, escrito directo en la base.</summary>
    private static async Task<TipoDeVisitaId> InsertarTipoAsync(Escenario escenario, bool activo)
    {
        var id = TipoDeVisitaId.Nuevo();
        await escenario.EjecutarAsync(
            $"""
            insert into agenda.tipo_de_visita (id, nombre, descripcion, duracion_min, reservable_en_linea, orden, activo)
            values ('{id.Valor}', 'Cirugía menor', null, 60, false, 3, {(activo ? "true" : "false")})
            """);
        return id;
    }

    private static Task<long> BitacoraYOutboxAsync(Escenario escenario) =>
        escenario.EscalarAsync<long>("select (select count(*) from agenda.bitacora) + (select count(*) from agenda.outbox)");
}
