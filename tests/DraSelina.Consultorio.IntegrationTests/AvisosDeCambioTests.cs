using System.Net;
using System.Text;
using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Contenido;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Features;
using DraSelina.Consultorio.UI.Pages.Gestion;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Los avisos de reprogramación y cancelación (CAP-18, FR-23, UJ-2; entrada 4.4): <see cref="CitaReprogramada"/> y
/// <see cref="CitaCancelada"/> salen por el outbox de Agenda, producidos por los comandos reales de la 3.2 y la 3.4
/// (la cancelación del Consultorio, que es de la épica 5, se escribe en el outbox como lo haría Agenda), y
/// Notificaciones envía por el canal de prueba del arnés. Contra PostgreSQL real, con la base migrada de cada prueba,
/// el reloj de <see cref="ReservaEnLineaTests"/> y las vueltas de entrega a mano. La Cita es la de Ana, con correo, el
/// martes 3 de noviembre a las 4:00 pm, agendada el lunes 2 a las 12:00 local.
/// </summary>
public sealed class AvisosDeCambioTests(Arnes arnes)
{
    private const string Correo = "ana@correo.com";

    private const string Reprogramar = "reprogramar";

    private static readonly DateOnly Jueves = new(2026, 11, 5);
    private static readonly DateTimeOffset Inicio = HoraLocal.AUtc(Manana, new TimeOnly(16, 0));

    private static string Reservar => $"{Arnes.UrlDelSitio}/reservar";

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reprogramada_sale_Tu_cita_se_reprogramo_con_el_boton_y_el_archivo_nuevo_y_una_fila_Reprogramada_Enviado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarYConfirmarAsync(escenario);
        var confirmacion = Assert.Single(Arnes.Canal(escenario.Web).Enviados);

        Assert.True((await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0))).EsExito);
        var evento = Assert.Single(await EventosAsync(escenario, agendada, nameof(CitaReprogramada)));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var correo = Assert.Single(Avisos(escenario));
        Assert.Equal(Correo, correo.Destinatario);
        Assert.Equal("Tu cita se reprogramó: Jueves 5 de noviembre, 5:00 pm", correo.Asunto);

        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var agenda = ambito.ServiceProvider.GetRequiredService<IAgendaApi>();
        var enlace = agenda.ObtenerEnlaceDeGestion(agendada.CitaId);
        var consultorio = ContenidoApi.Consultorio;
        var html = WebUtility.HtmlDecode(correo.Html);
        foreach (var version in new[] { html, correo.Texto })
        {
            foreach (var texto in new[]
            {
                "Dra. Selina Medina Castillo", "Traumatología y Ortopedia", "Hola, Ana Sofía López", "Tu cita se reprogramó",
                "Jueves 5 de noviembre", "5:00 pm", "Primera consulta",
                consultorio.Lugar, consultorio.Direccion, consultorio.Ciudad, "Abrir en el mapa", consultorio.EnlaceDelMapa!,
                "Ver o cambiar mi cita", enlace,
            })
            {
                Assert.Contains(texto, version);
            }

            Assert.DoesNotContain("Martes 3 de noviembre", version);
            Assert.DoesNotContain("confirmar", version, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains($"""<a href="{enlace}" """, html);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"Ver o cambiar mi cita: {enlace}", correo.Texto);

        // El archivo nuevo, con el mismo UID que el de la confirmación: el calendario mueve el evento.
        var adjunto = Assert.Single(correo.Adjuntos);
        var calendario = await agenda.ObtenerArchivoDeCalendarioAsync(agendada.CitaId, Cancelacion);
        Assert.NotNull(calendario);
        Assert.Equal(calendario.Contenido, Encoding.UTF8.GetString(adjunto.Contenido));
        Assert.Equal("cita.ics", adjunto.NombreDeArchivo);
        Assert.Equal(Uid(Assert.Single(confirmacion.Adjuntos)), Uid(adjunto));
        Assert.NotEqual(Encoding.UTF8.GetString(Assert.Single(confirmacion.Adjuntos).Contenido), calendario.Contenido);

        var mensaje = Assert.Single(await MensajesAsync(escenario));
        Assert.Equal(7, mensaje.Id.Version);
        Assert.Equal(evento.Id, mensaje.EventoId);
        Assert.Equal(agendada.CitaId.Valor, mensaje.CitaId);
        Assert.Equal("Reprogramada", mensaje.Tipo);
        Assert.Equal("correo", mensaje.Canal);
        Assert.Equal("Enviado", mensaje.Estado);
        Assert.Equal(escenario.Reloj.UtcNow, mensaje.RegistradoEn);
    }

    [Fact]
    public async Task Cancelada_a_tiempo_sale_Tu_cita_esta_cancelada_con_Agendar_otra_cita_sin_la_frase_de_tardia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarYConfirmarAsync(escenario);

        Assert.True((await CancelarAsync(escenario, agendada)).EsExito);
        var evento = Assert.Single(await EventosAsync(escenario, agendada, nameof(CitaCancelada)));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var correo = Assert.Single(Avisos(escenario));
        Assert.Equal(Correo, correo.Destinatario);
        Assert.Equal("Tu cita está cancelada", correo.Asunto);
        var html = WebUtility.HtmlDecode(correo.Html);
        foreach (var version in new[] { html, correo.Texto })
        {
            foreach (var texto in new[]
            {
                "Dra. Selina Medina Castillo", "Traumatología y Ortopedia", "Hola, Ana Sofía López", "Tu cita está cancelada",
                "Martes 3 de noviembre", "4:00 pm", "Primera consulta", "Agendar otra cita", Reservar,
            })
            {
                Assert.Contains(texto, version);
            }

            Assert.DoesNotContain(PlantillaDeCancelacion.NotaTardia, version);
            Assert.DoesNotContain("confirmar", version, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Arnes.UrlBaseDeEnlace, version);
            Assert.DoesNotContain("Ver o cambiar mi cita", version);
        }

        // Un solo vínculo, a /reservar; sin archivo de calendario.
        Assert.Contains($"""<a href="{Reservar}" """, html);
        Assert.Equal(1, CuantasVeces(html, "<a "));
        Assert.Contains($"Agendar otra cita: {Reservar}", correo.Texto);
        Assert.Empty(correo.Adjuntos);

        var mensaje = Assert.Single(await MensajesAsync(escenario));
        Assert.Equal(evento.Id, mensaje.EventoId);
        Assert.Equal(agendada.CitaId.Valor, mensaje.CitaId);
        Assert.Equal("Cancelada", mensaje.Tipo);
        Assert.Equal("Enviado", mensaje.Estado);
    }

    [Fact]
    public async Task Cancelada_tarde_sale_el_mismo_correo_con_la_frase_de_cancelacion_tardia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarYConfirmarAsync(escenario);
        escenario.Reloj.Avanzar(Inicio.AddHours(-1) - escenario.Reloj.UtcNow);

        Assert.True((await CancelarAsync(escenario, agendada)).Valor.Tardia);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var correo = Assert.Single(Avisos(escenario));
        Assert.Equal("Tu cita está cancelada", correo.Asunto);
        foreach (var version in new[] { WebUtility.HtmlDecode(correo.Html), correo.Texto })
        {
            Assert.Contains("Gracias por avisarnos. Quedó registrada como cancelación tardía.", version);
            Assert.Contains("Martes 3 de noviembre", version);
            Assert.Contains("Agendar otra cita", version);
            Assert.Contains(Reservar, version);
        }

        Assert.Empty(correo.Adjuntos);
        Assert.Equal("Cancelada", Assert.Single(await MensajesAsync(escenario)).Tipo);
    }

    [Fact]
    public async Task Cancelada_por_el_Consultorio_sale_la_disculpa_con_Elegir_otro_horario()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarYConfirmarAsync(escenario);

        var evento = await CancelarPorElConsultorioAsync(escenario, agendada);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var correo = Assert.Single(Avisos(escenario));
        Assert.Equal(Correo, correo.Destinatario);
        Assert.Equal("Tuvimos que cancelar tu cita", correo.Asunto);
        var html = WebUtility.HtmlDecode(correo.Html);
        foreach (var version in new[] { html, correo.Texto })
        {
            foreach (var texto in new[]
            {
                "Dra. Selina Medina Castillo", "Hola, Ana Sofía López", "Tuvimos que cancelar tu cita",
                "Lo sentimos mucho: tuvimos que cancelar tu cita del martes 3 de noviembre, 4:00 pm. Elige otro horario cuando te acomode.",
                "Elegir otro horario", Reservar,
            })
            {
                Assert.Contains(texto, version);
            }

            Assert.DoesNotContain("Agendar otra cita", version);
            Assert.DoesNotContain(PlantillaDeCancelacion.NotaTardia, version);
            Assert.DoesNotContain(Arnes.UrlBaseDeEnlace, version);
            Assert.DoesNotContain("confirmar", version, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains($"""<a href="{Reservar}" """, html);
        Assert.Equal(1, CuantasVeces(html, "<a "));
        Assert.Empty(correo.Adjuntos);

        var mensaje = Assert.Single(await MensajesAsync(escenario));
        Assert.Equal(evento.EventoId, mensaje.EventoId);
        Assert.Equal("CanceladaPorConsultorio", mensaje.Tipo);
        Assert.Equal("Enviado", mensaje.Estado);
    }

    [Theory]
    [InlineData(Reprogramar)]
    [InlineData(CitaCancelada.MotivoPaciente)]
    [InlineData(CitaCancelada.MotivoTarde)]
    [InlineData(CitaCancelada.MotivoConsultorio)]
    public async Task Sin_correo_no_sale_ningun_aviso_ni_queda_ninguna_fila(string caso)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando();
        comando.Correo = null;
        var agendada = (await escenario.AgendarAsync(comando)).Valor;
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        await ProvocarAsync(escenario, agendada, caso);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Empty(Arnes.Canal(escenario.Web).Enviados);
        Assert.Empty(await MensajesAsync(escenario));
        Assert.All(
            await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, agendada.CitaId.Valor, Cancelacion),
            e => Assert.Equal("Entregado", e.Estado));
    }

    [Theory]
    [InlineData(Reprogramar)]
    [InlineData(CitaCancelada.MotivoPaciente)]
    [InlineData(CitaCancelada.MotivoTarde)]
    [InlineData(CitaCancelada.MotivoConsultorio)]
    public async Task El_mismo_evento_entregado_dos_veces_deja_un_solo_aviso_y_una_sola_fila(string caso)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarYConfirmarAsync(escenario);
        await ProvocarAsync(escenario, agendada, caso);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        // El outbox lo entrega otra vez, como si la marca de Entregado no hubiera quedado.
        var evento = (await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, agendada.CitaId.Valor, Cancelacion))
            .Single(e => e.Tipo != nameof(CitaAgendada));
        await escenario.EjecutarAsync($"update agenda.outbox set estado = 'Pendiente', entregado_en = null where id = '{evento.Id}'");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Single(Avisos(escenario));
        Assert.Equal(evento.Id, Assert.Single(await MensajesAsync(escenario)).EventoId);
        Assert.Equal("Entregado", (await Consultas.EventoAsync(escenario.Cadena, AgendaDbContext.Esquema, evento.Id, Cancelacion))!.Estado);
    }

    [Theory]
    [InlineData(Reprogramar)]
    [InlineData(CitaCancelada.MotivoPaciente)]
    [InlineData(CitaCancelada.MotivoTarde)]
    [InlineData(CitaCancelada.MotivoConsultorio)]
    public async Task Con_el_canal_caido_no_queda_fila_y_el_outbox_reintenta_hasta_que_sale(string caso)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarYConfirmarAsync(escenario);
        var canal = Arnes.Canal(escenario.Web);
        canal.Caido = true;

        await ProvocarAsync(escenario, agendada, caso);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var evento = (await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, agendada.CitaId.Valor, Cancelacion))
            .Single(e => e.Tipo != nameof(CitaAgendada));
        Assert.Equal("Pendiente", evento.Estado);
        Assert.Equal(1, evento.Intentos);
        Assert.Empty(Avisos(escenario));
        Assert.Empty(await MensajesAsync(escenario));

        // El canal vuelve y pasa la espera del primer reintento: la siguiente entrega lo envía.
        canal.Caido = false;
        escenario.Reloj.Avanzar(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Equal(Correo, Assert.Single(Avisos(escenario)).Destinatario);
        Assert.Equal("Enviado", Assert.Single(await MensajesAsync(escenario)).Estado);
        Assert.Equal("Entregado", (await Consultas.EventoAsync(escenario.Cadena, AgendaDbContext.Esquema, evento.Id, Cancelacion))!.Estado);
    }

    [Fact]
    public async Task Reprogramada_y_luego_cancelada_el_Paciente_recibe_en_orden_la_confirmacion_la_reprogramacion_y_la_cancelacion()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = (await escenario.AgendarAsync(Comando())).Valor;
        Assert.True((await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0))).EsExito);
        Assert.True((await CancelarAsync(escenario, agendada)).EsExito);

        // Los tres eventos de la misma Cita, entregados en su orden.
        for (var vueltas = 0; vueltas < 5 && await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion) > 0; vueltas++)
        {
        }

        Assert.Equal(
            [
                "Tu cita está confirmada: Martes 3 de noviembre, 4:00 pm",
                "Tu cita se reprogramó: Jueves 5 de noviembre, 5:00 pm",
                "Tu cita está cancelada",
            ],
            Arnes.Canal(escenario.Web).Enviados.Select(c => c.Asunto));
        Assert.All(Arnes.Canal(escenario.Web).Enviados, c => Assert.Equal(Correo, c.Destinatario));
        Assert.Contains("Jueves 5 de noviembre", Arnes.Canal(escenario.Web).Enviados[2].Texto);
    }

    [Fact]
    public async Task Ni_la_direccion_ni_el_nombre_van_a_los_registros_ni_a_las_filas_de_los_mensajes()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await AgendarYConfirmarAsync(escenario);
        var canal = Arnes.Canal(escenario.Web);

        // Cada aviso con una entrega que falla y otra que sale: las dos dejan registros.
        foreach (var provocar in new Func<Task>[]
        {
            async () => Assert.True((await ReprogramarAsync(escenario, agendada, Jueves, new TimeOnly(17, 0))).EsExito),
            async () => Assert.True((await CancelarAsync(escenario, agendada)).EsExito),
            async () => await CancelarPorElConsultorioAsync(escenario, agendada),
        })
        {
            canal.Caido = true;
            await provocar();
            await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion);
            canal.Caido = false;
            escenario.Reloj.Avanzar(TimeSpan.FromMinutes(1));
            await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion);
        }

        Assert.Equal(3, Avisos(escenario).Count);
        var filas = await escenario.EscalarAsync<string>(
            "select json_agg(m)::text from notificaciones.mensaje m where tipo in ('Reprogramada', 'Cancelada', 'CanceladaPorConsultorio')");
        var registros = Arnes.Registros(escenario.Web).Select(r => r.Mensaje).ToList();
        Assert.NotEmpty(registros);
        foreach (var dato in new[] { Correo, "correo.com", "Ana Sofía", "López", "Lopez", "4441234567", "rodilla", "Tu cita" })
        {
            Assert.DoesNotContain(dato, filas, StringComparison.OrdinalIgnoreCase);
            Assert.All(registros, r => Assert.DoesNotContain(dato, r, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var correo in Avisos(escenario))
        {
            Assert.DoesNotContain(Correo, correo.ToString());
            Assert.DoesNotContain("Ana", correo.ToString());
        }

        var datos = new PlantillaDeCancelacion.Datos(
            Correo, "Ana Sofía López", Manana, new TimeOnly(16, 0), null, Reservar, PlantillaDeCancelacion.Clase.Tardia);
        Assert.DoesNotContain(Correo, datos.ToString());
        Assert.DoesNotContain("Ana", datos.ToString());
    }

    [Fact]
    public void La_frase_de_tardia_y_el_titulo_del_correo_son_los_de_la_pantalla_de_cancelacion()
    {
        Assert.Equal(ConfirmacionDeCancelacion.NotaTardia, PlantillaDeCancelacion.NotaTardia);
        Assert.Equal(ConfirmacionDeCancelacion.Titulo, PlantillaDeCancelacion.Titulo);
    }

    // Agenda a Ana el martes a las 16:00 y entrega su CitaAgendada: la confirmación ya salió.
    private static async Task<CitaAgendadaEnLinea> AgendarYConfirmarAsync(Escenario escenario)
    {
        var resultado = await escenario.AgendarAsync(Comando());
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        return resultado.Valor;
    }

    // Reprograma al jueves a las 17:00, cancela a tiempo, cancela tarde (una hora antes) o la cancela el Consultorio.
    private static async Task ProvocarAsync(Escenario escenario, CitaAgendadaEnLinea cita, string caso)
    {
        switch (caso)
        {
            case Reprogramar:
                Assert.True((await ReprogramarAsync(escenario, cita, Jueves, new TimeOnly(17, 0))).EsExito);
                break;
            case CitaCancelada.MotivoPaciente:
                Assert.False((await CancelarAsync(escenario, cita)).Valor.Tardia);
                break;
            case CitaCancelada.MotivoTarde:
                escenario.Reloj.Avanzar(Inicio.AddHours(-1) - escenario.Reloj.UtcNow);
                Assert.True((await CancelarAsync(escenario, cita)).Valor.Tardia);
                break;
            case CitaCancelada.MotivoConsultorio:
                await CancelarPorElConsultorioAsync(escenario, cita);
                break;
            default:
                throw new ArgumentException("Caso no previsto.", nameof(caso));
        }
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

    private static async Task<Resultado<CitaCanceladaPorEnlace>> CancelarAsync(Escenario escenario, CitaAgendadaEnLinea cita)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<ICommandHandler<CancelarCitaPorEnlace, CitaCanceladaPorEnlace>>()
            .HandleAsync(new CancelarCitaPorEnlace { Token = Token(cita) }, Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado;
    }

    // La épica 5 la cancelará por su comando; aquí la Cita queda Cancelada y el evento se escribe en el outbox como lo
    // haría Agenda.
    private static async Task<CitaCancelada> CancelarPorElConsultorioAsync(Escenario escenario, CitaAgendadaEnLinea cita)
    {
        var evento = new CitaCancelada(
            Guid.CreateVersion7(), escenario.Reloj.UtcNow, cita.CitaId, new PatientId(Assert.Single(await escenario.CitasAsync()).PacienteId),
            Actor.Administrador, Inicio, CitaCancelada.MotivoConsultorio);
        var contenido = JsonSerializer.Serialize(evento).Replace("'", "''");
        await escenario.EjecutarAsync(
            $"""
            update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'consultorio', cancelada_en = '{evento.OcurridoEn:O}'
            where id = '{cita.CitaId.Valor}';
            insert into agenda.outbox
                (id, tipo, contenido, ocurrido_en, clave_de_orden, estado, intentos, proximo_intento_en, entregado_en)
            values
                ('{evento.EventoId}', '{nameof(CitaCancelada)}', '{contenido}'::jsonb, '{evento.OcurridoEn:O}',
                 '{evento.CitaId.Valor}', 'Pendiente', 0, '{evento.OcurridoEn:O}', null)
            """);
        return evento;
    }

    private static async Task<List<FilaLeidaDeOutbox>> EventosAsync(Escenario escenario, CitaAgendadaEnLinea cita, string tipo) =>
        [.. (await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, cita.CitaId.Valor, Cancelacion))
            .Where(f => f.Tipo == tipo)];

    // Lo enviado que no es la confirmación de la 4.1.
    private static List<MensajeDeCorreo> Avisos(Escenario escenario) =>
        [.. Arnes.Canal(escenario.Web).Enviados.Where(c => !c.Asunto.StartsWith(PlantillaDeConfirmacion.Titulo, StringComparison.Ordinal))];

    private static Task<List<MensajeLeido>> MensajesAsync(Escenario escenario) => escenario.FilasAsync(
        "select id, evento_id, cita_id, tipo, canal, estado, registrado_en from notificaciones.mensaje "
        + "where tipo in ('Reprogramada', 'Cancelada', 'CanceladaPorConsultorio') order by id",
        l => new MensajeLeido(
            l.GetGuid(0), l.IsDBNull(1) ? null : l.GetGuid(1), l.IsDBNull(2) ? null : l.GetGuid(2),
            l.GetString(3), l.GetString(4), l.GetString(5), l.GetFieldValue<DateTimeOffset>(6)));

    private static string Token(CitaAgendadaEnLinea cita) =>
        cita.EnlaceDeGestion[(cita.EnlaceDeGestion.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];

    private static string Uid(AdjuntoDeCorreo adjunto) =>
        Encoding.UTF8.GetString(adjunto.Contenido).Split("\r\n").Single(l => l.StartsWith("UID:", StringComparison.Ordinal));

    private static int CuantasVeces(string texto, string parte)
    {
        var veces = 0;
        for (var i = texto.IndexOf(parte, StringComparison.Ordinal); i >= 0; i = texto.IndexOf(parte, i + parte.Length, StringComparison.Ordinal))
        {
            veces++;
        }

        return veces;
    }

    private sealed record MensajeLeido(
        Guid Id,
        Guid? EventoId,
        Guid? CitaId,
        string Tipo,
        string Canal,
        string Estado,
        DateTimeOffset RegistradoEn);
}
