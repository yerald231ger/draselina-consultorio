using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Reprogramar por el Enlace de gestión (CAP-7, FR-9; entrada 3.2): <see cref="ReprogramarCitaPorEnlace"/> por su
/// handler contra PostgreSQL real. Es la misma Cita en otro horario (AD-7, RN-15), con su bitácora y
/// <see cref="CitaReprogramada"/> en la misma transacción (T-30). El reloj está en el lunes 2 de noviembre de 2026 a las
/// 12:00 locales y la Cita, Agendada el martes 3 a las 16:00 (30 min).
/// </summary>
public sealed class ReprogramarCitaTests(Arnes arnes)
{
    private static readonly DateOnly Jueves = new(2026, 11, 5);
    private static readonly DateOnly Viernes = new(2026, 11, 6);
    private static readonly DateTimeOffset InicioActual = HoraLocal.AUtc(Manana, new TimeOnly(16, 0));

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Mover_a_otro_horario_libre_es_la_misma_Cita_con_su_bitacora_y_su_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        var tokenHash = await TokenHashAsync(escenario, agendada.CitaId);

        var resultado = await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(new CitaReprogramadaPorEnlace(Jueves, new TimeOnly(17, 0)), resultado.Valor);

        var cita = Assert.Single(await escenario.CitasAsync());
        var inicioNuevo = HoraLocal.AUtc(Jueves, new TimeOnly(17, 0));
        Assert.Equal(agendada.CitaId.Valor, cita.Id);
        Assert.Equal(tokenHash, await TokenHashAsync(escenario, agendada.CitaId));
        Assert.Equal(inicioNuevo, cita.Inicio);
        Assert.Equal(inicioNuevo.AddMinutes(30), cita.Fin);
        Assert.Equal("Agendada", cita.Estado);
        Assert.Equal("en_linea", cita.Origen);
        Assert.Equal("Dolor de rodilla derecha", cita.MotivoDeConsulta);

        var entrada = Assert.Single(await ReprogramadasAsync(escenario));
        Assert.Equal(Ahora, entrada.OcurridoEn);
        Assert.Equal("Paciente", entrada.ActorTipo);
        Assert.Null(entrada.ActorId);
        Assert.Equal("Cita", entrada.Entidad);
        Assert.Equal(cita.Id.ToString(), entrada.EntidadId);
        using (var detalle = JsonDocument.Parse(entrada.Detalle))
        {
            var raiz = detalle.RootElement;
            Assert.Equal(InicioActual, raiz.GetProperty("inicioAnterior").GetDateTimeOffset());
            Assert.Equal(InicioActual.AddMinutes(30), raiz.GetProperty("finAnterior").GetDateTimeOffset());
            Assert.Equal(inicioNuevo, raiz.GetProperty("inicioNuevo").GetDateTimeOffset());
            Assert.Equal(inicioNuevo.AddMinutes(30), raiz.GetProperty("finNuevo").GetDateTimeOffset());
        }

        var fila = Assert.Single(await EventosReprogramadaAsync(escenario, cita.Id));
        Assert.Equal("Pendiente", fila.Estado);
        Assert.Equal(Ahora, fila.OcurridoEn);
        Assert.Equal(cita.Id, fila.ClaveDeOrden);
        using (var contenido = JsonDocument.Parse(fila.Contenido))
        {
            Assert.Equal("Paciente", contenido.RootElement.GetProperty(nameof(CitaReprogramada.Actor)).GetString());
        }

        Assert.Equal(
            new CitaReprogramada(
                fila.Id, Ahora, new CitaId(cita.Id), new PatientId(cita.PacienteId), Actor.Paciente,
                InicioActual, inicioNuevo, inicioNuevo.AddMinutes(30)),
            JsonSerializer.Deserialize<CitaReprogramada>(fila.Contenido));
    }

    [Fact]
    public async Task El_mismo_enlace_muestra_la_nueva_fecha_en_Tu_cita()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        Assert.True((await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0))).EsExito);

        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var tuCita = await ambito.ServiceProvider.GetRequiredService<IQueryHandler<CitaPorEnlace, CitaDelEnlace>>()
            .HandleAsync(new CitaPorEnlace(Token(agendada)), Cancelacion);
        Assert.True(tuCita.EsExito, $"{tuCita.Forma} {tuCita.Codigo}");
        Assert.Equal(Jueves, tuCita.Valor.Fecha);
        Assert.Equal(new TimeOnly(17, 0), tuCita.Valor.Hora);
        Assert.Equal(EstadoDelEnlace.Agendada, tuCita.Valor.Estado);
    }

    [Fact]
    public async Task Con_tres_Citas_del_mismo_telefono_en_la_semana_se_mueve_una_sin_tope_semanal()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.AgendarTresAsync();
        var martes = await EnlaceDeAsync(escenario, InicioActual);

        var resultado = await ReprogramarAsync(escenario, martes, Viernes, new TimeOnly(17, 0));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Contains(await escenario.CitasAsync(), c => c.Inicio == HoraLocal.AUtc(Viernes, new TimeOnly(17, 0)));
        Assert.Equal(3, (await escenario.CitasAsync()).Count);
    }

    [Fact]
    public async Task Dentro_del_umbral_la_reprogramacion_esta_cerrada_y_no_cambia_nada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        // A 119 minutos del inicio actual (martes 3, 16:00 local = 22:00 UTC).
        escenario.Reloj.Avanzar(InicioActual.AddMinutes(-119) - Ahora);
        var resultado = await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));

        Assert.Equal(CodigosDeAgenda.ReprogramacionCerrada, resultado.Codigo);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task A_120_minutos_del_inicio_actual_todavia_se_reprograma()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        escenario.Reloj.Avanzar(InicioActual.AddMinutes(-120) - Ahora);
        var resultado = await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
    }

    [Theory]
    [InlineData("2026-11-05", "16:15", CodigosDeAgenda.FueraDeHorario)]
    [InlineData("2026-11-07", "16:00", CodigosDeAgenda.FueraDeHorario)]
    [InlineData("2026-11-02", "16:00", CodigosDeAgenda.FueraDeVentana)]
    [InlineData("2026-11-02", "11:00", CodigosDeAgenda.EnElPasado)]
    public async Task Un_rechazo_de_las_reglas_deja_la_Cita_la_bitacora_y_el_outbox_como_antes(string fecha, string hora, string codigo)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        var resultado = await ReprogramarAsync(escenario, agendada, DateOnly.Parse(fecha), TimeOnly.Parse(hora));

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(codigo, resultado.Codigo);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Mas_alla_de_la_ventana_queda_fuera_de_ventana()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        // Hoy + 2 meses + 1 día: domingo 3 de enero de 2027, abierto por una custom para que solo falle la ventana.
        var domingo = new DateOnly(2027, 1, 3);
        await escenario.InsertarCustomAsync(domingo, "domingo abierto", (new TimeOnly(10, 0), new TimeOnly(11, 0)));
        var resultado = await ReprogramarAsync(escenario, agendada, domingo, new TimeOnly(10, 0));

        Assert.Equal(CodigosDeAgenda.FueraDeVentana, resultado.Codigo);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Un_horario_ocupado_por_otra_Cita_vuelve_como_horario_ocupado_sin_cambios_ni_bitacora_ni_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        await escenario.InsertarCitaDelConsultorioAsync(Jueves, new TimeOnly(17, 0), new TimeOnly(17, 30));
        var antes = await ConteosAsync(escenario);

        var resultado = await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));

        Assert.Equal(CodigosDeAgenda.HorarioOcupado, resultado.Codigo);
        var cita = Assert.Single(await escenario.CitasAsync(), c => c.Id == agendada.CitaId.Valor);
        Assert.Equal(InicioActual, cita.Inicio);
        Assert.Equal(InicioActual.AddMinutes(30), cita.Fin);
        Assert.Equal(antes, await ConteosAsync(escenario));
        Assert.Empty(await ReprogramadasAsync(escenario));
    }

    [Fact]
    public async Task Pedir_la_misma_hora_que_ya_tiene_es_exito_sin_bitacora_ni_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        var resultado = await ReprogramarAsync(escenario, agendada, Manana, new TimeOnly(16, 0));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(new CitaReprogramadaPorEnlace(Manana, new TimeOnly(16, 0)), resultado.Valor);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task La_misma_hora_otra_vez_dentro_del_umbral_es_exito_sin_cambios()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        // La comprobación de la misma hora va antes del umbral (Gerardo, 2026-11-06).
        escenario.Reloj.Avanzar(InicioActual.AddMinutes(-30) - Ahora);
        var resultado = await ReprogramarAsync(escenario, agendada, Manana, new TimeOnly(16, 0));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Un_segundo_envio_identico_no_deja_otra_bitacora_ni_otro_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        var primero = await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));
        var segundo = await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));

        Assert.True(primero.EsExito, $"{primero.Forma} {primero.Codigo}");
        Assert.True(segundo.EsExito, $"{segundo.Forma} {segundo.Codigo}");
        Assert.Equal(primero.Valor, segundo.Valor);
        Assert.Single(await ReprogramadasAsync(escenario));
        Assert.Single(await EventosReprogramadaAsync(escenario, agendada.CitaId.Valor));
    }

    [Theory]
    [InlineData("paciente")]
    [InlineData("consultorio")]
    public async Task Una_Cita_cancelada_vuelve_como_cita_no_agendada(string motivo)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        await escenario.EjecutarAsync(
            $"update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = '{motivo}', cancelada_en = now()");
        var antes = await ConteosAsync(escenario);

        var resultado = await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));

        Assert.Equal(CodigosDeAgenda.CitaNoAgendada, resultado.Codigo);
        await SinCambiosTrasCancelarAsync(escenario, antes);
    }

    [Fact]
    public async Task Una_Cita_cancelada_a_la_misma_hora_tampoco_se_reprograma()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        await escenario.EjecutarAsync(
            "update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now()");
        var antes = await ConteosAsync(escenario);

        var resultado = await ReprogramarAsync(escenario, agendada, Manana, new TimeOnly(16, 0));

        Assert.Equal(CodigosDeAgenda.CitaNoAgendada, resultado.Codigo);
        await SinCambiosTrasCancelarAsync(escenario, antes);
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("no-es-un-token")]
    public async Task Un_token_inexistente_o_mal_formado_vuelve_como_cita_no_encontrada(string token)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAsync(escenario);

        var resultado = await ReprogramarAsync(escenario, token, Jueves, new TimeOnly(17, 0));

        Assert.Equal(CodigosDeAgenda.CitaNoEncontrada, resultado.Codigo);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task Un_token_vacio_no_llega_al_handler()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await EnviarAsync(escenario, new ReprogramarCitaPorEnlace { Fecha = Jueves, Hora = new TimeOnly(17, 0) });

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Contains(nameof(ReprogramarCitaPorEnlace.Token), resultado.Errores.Keys);
    }

    [Fact]
    public async Task Una_Cita_que_ya_empezo_vuelve_como_cita_no_encontrada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        escenario.Reloj.Avanzar(InicioActual - Ahora);
        var resultado = await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));

        Assert.Equal(CodigosDeAgenda.CitaNoEncontrada, resultado.Codigo);
        await SinCambiosAsync(escenario);
    }

    [Fact]
    public async Task El_token_no_va_a_los_registros_ni_al_texto_del_comando()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        var token = Token(agendada);
        var comando = new ReprogramarCitaPorEnlace { Token = token, Fecha = Jueves, Hora = new TimeOnly(17, 0) };

        Assert.DoesNotContain(token, comando.ToString(), StringComparison.Ordinal);
        Assert.True((await EnviarAsync(escenario, comando)).EsExito);
        Assert.False((await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(16, 15))).EsExito);

        Assert.DoesNotContain(Arnes.Registros(escenario.Web), r => r.Mensaje.Contains(token[..20], StringComparison.Ordinal));
        var detalle = Assert.Single(await ReprogramadasAsync(escenario)).Detalle;
        var contenido = Assert.Single(await EventosReprogramadaAsync(escenario, agendada.CitaId.Valor)).Contenido;
        foreach (var texto in new[] { detalle, contenido })
        {
            foreach (var dato in new[] { token[..20], "Ana", "López", "4441234567", "ana@correo.com", "rodilla" })
            {
                Assert.DoesNotContain(dato, texto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task Dos_reprogramaciones_simultaneas_se_aplican_una_tras_otra_y_la_segunda_parte_de_la_primera()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);
        var jueves = HoraLocal.AUtc(Jueves, new TimeOnly(17, 0));
        var viernes = HoraLocal.AUtc(Viernes, new TimeOnly(17, 0));

        // Fechas distintas: candados distintos; las ordena el bloqueo de la fila.
        var resultados = await Task.WhenAll(
            ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0)),
            ReprogramarAsync(escenario, agendada, Viernes, new TimeOnly(17, 0)));

        Assert.All(resultados, r => Assert.True(r.EsExito, $"{r.Forma} {r.Codigo}"));

        var entradas = await ReprogramadasAsync(escenario);
        Assert.Equal(2, entradas.Count);
        Assert.Equal(2, (await EventosReprogramadaAsync(escenario, agendada.CitaId.Valor)).Count);

        var cambios = entradas.Select(e =>
        {
            using var detalle = JsonDocument.Parse(e.Detalle);
            return (Anterior: detalle.RootElement.GetProperty("inicioAnterior").GetDateTimeOffset(),
                    Nuevo: detalle.RootElement.GetProperty("inicioNuevo").GetDateTimeOffset());
        }).ToList();

        // La primera parte del martes; la segunda, de donde la dejó la primera.
        var primera = Assert.Single(cambios, c => c.Anterior == InicioActual);
        var segunda = Assert.Single(cambios, c => c.Anterior != InicioActual);
        Assert.Equal(primera.Nuevo, segunda.Anterior);
        Assert.Equal([jueves, viernes], new[] { primera.Nuevo, segunda.Nuevo }.Order());
        Assert.Equal(segunda.Nuevo, Assert.Single(await escenario.CitasAsync()).Inicio);
    }

    [Fact]
    public async Task La_reprogramacion_espera_a_quien_tiene_bloqueada_la_fila_de_la_Cita_y_ve_su_cambio()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarAsync(escenario);

        // Otra transacción bloquea la fila y, sin confirmar, cancela la Cita.
        await using var conexion = new NpgsqlConnection(escenario.Cadena);
        await conexion.OpenAsync(Cancelacion);
        await using var otra = await conexion.BeginTransactionAsync(Cancelacion);
        await using (var bloqueo = new NpgsqlCommand(
            $"select id from agenda.cita where id = '{agendada.CitaId.Valor}' for update", conexion, otra))
        {
            await bloqueo.ExecuteNonQueryAsync(Cancelacion);
        }

        await using (var cancelar = new NpgsqlCommand(
            "update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now()", conexion, otra))
        {
            await cancelar.ExecuteNonQueryAsync(Cancelacion);
        }

        var reprogramacion = ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0));
        await Task.Delay(TimeSpan.FromMilliseconds(500), Cancelacion);
        Assert.False(reprogramacion.IsCompleted);

        await otra.CommitAsync(Cancelacion);
        var resultado = await reprogramacion;

        Assert.Equal(CodigosDeAgenda.CitaNoAgendada, resultado.Codigo);
        Assert.Empty(await ReprogramadasAsync(escenario));
    }

    private static async Task<CitaAgendadaEnLinea> AgendarAsync(Escenario escenario)
    {
        var resultado = await escenario.AgendarAsync(Comando());
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    // El token de la Cita que empieza en ese instante, derivado con la clave de la web (AD-19).
    private static async Task<string> EnlaceDeAsync(Escenario escenario, DateTimeOffset inicio)
    {
        var id = await escenario.EscalarAsync<Guid>($"select id from agenda.cita where inicio = '{inicio:O}'");
        var clave = escenario.Web.Services.GetRequiredService<ClaveDeGestion>();
        return EnlaceDeGestion.Derivar(clave, new CitaId(id)).Token;
    }

    private static Task<Resultado<CitaReprogramadaPorEnlace>> ReprogramarAsync(
        Escenario escenario, CitaAgendadaEnLinea cita, DateOnly fecha, TimeOnly hora) =>
        ReprogramarAsync(escenario, Token(cita), fecha, hora);

    private static Task<Resultado<CitaReprogramadaPorEnlace>> ReprogramarAsync(
        Escenario escenario, string token, DateOnly fecha, TimeOnly hora) =>
        EnviarAsync(escenario, new ReprogramarCitaPorEnlace { Token = token, Fecha = fecha, Hora = hora });

    // El handler por su interfaz, en un ámbito propio y sin sesión, como lo pide la página: el actor es el Paciente.
    private static async Task<Resultado<CitaReprogramadaPorEnlace>> EnviarAsync(Escenario escenario, ReprogramarCitaPorEnlace comando)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        return await ambito.ServiceProvider
            .GetRequiredService<ICommandHandler<ReprogramarCitaPorEnlace, CitaReprogramadaPorEnlace>>()
            .HandleAsync(comando, Cancelacion);
    }

    // La Cita sigue el martes a las 16:00, con solo la bitácora y el evento de cuando se agendó.
    private static async Task SinCambiosAsync(Escenario escenario)
    {
        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(InicioActual, cita.Inicio);
        Assert.Equal(InicioActual.AddMinutes(30), cita.Fin);
        Assert.Equal("Agendada", Assert.Single(await escenario.FilasAsync("select accion from agenda.bitacora", l => l.GetString(0))));
        Assert.Equal(nameof(CitaAgendada), Assert.Single(await escenario.FilasAsync("select tipo from agenda.outbox", l => l.GetString(0))));
    }

    // Filas de la bitácora y del outbox, todas.
    private static async Task<(long Bitacora, long Outbox)> ConteosAsync(Escenario escenario) =>
        (await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"),
         await escenario.EscalarAsync<long>("select count(*) from agenda.outbox"));

    // La Cita cancelada sigue en el martes a las 16:00 y la bitácora y el outbox tienen las mismas filas que antes.
    private static async Task SinCambiosTrasCancelarAsync(Escenario escenario, (long Bitacora, long Outbox) antes)
    {
        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(InicioActual, cita.Inicio);
        Assert.Equal(InicioActual.AddMinutes(30), cita.Fin);
        Assert.Equal(antes, await ConteosAsync(escenario));
    }

    private static string Token(CitaAgendadaEnLinea cita) =>
        cita.EnlaceDeGestion[(cita.EnlaceDeGestion.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];

    private static Task<byte[]> TokenHashAsync(Escenario escenario, CitaId id) =>
        escenario.EscalarAsync<byte[]>($"select token_hash from agenda.cita where id = '{id.Valor}'");

    private static Task<List<Reprogramada>> ReprogramadasAsync(Escenario escenario) => escenario.FilasAsync(
        "select ocurrido_en, actor_tipo, actor_id, entidad, entidad_id, detalle::text from agenda.bitacora "
        + "where accion = 'Reprogramada' order by id",
        l => new Reprogramada(
            l.GetFieldValue<DateTimeOffset>(0), l.GetString(1), l.IsDBNull(2) ? null : l.GetString(2),
            l.GetString(3), l.GetString(4), l.GetString(5)));

    private static async Task<List<FilaLeidaDeOutbox>> EventosReprogramadaAsync(Escenario escenario, Guid citaId) =>
        [.. (await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, citaId, Cancelacion))
            .Where(f => f.Tipo == nameof(CitaReprogramada))];

    private sealed record Reprogramada(
        DateTimeOffset OcurridoEn,
        string ActorTipo,
        string? ActorId,
        string Entidad,
        string EntidadId,
        string Detalle);
}
