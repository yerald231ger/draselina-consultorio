using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Contenido;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Features;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.UI.Pages.Reserva;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La confirmación por correo (CAP-18, FR-21): <see cref="CitaAgendada"/> sale por el outbox de Agenda, Notificaciones
/// pide contacto a Pacientes, datos a Contenido y enlace y archivo de calendario a Agenda, y envía por el canal de
/// prueba del arnés (<see cref="Arnes.Canal"/>). Contra PostgreSQL real, con la base migrada de cada prueba, el reloj
/// de <see cref="ReservaEnLineaTests"/> y las vueltas de entrega a mano. La reserva de <see cref="ReservaEnLineaTests.Comando"/>
/// es la de Ana, con correo, el martes 3 de noviembre a las 4:00 pm.
/// </summary>
public sealed class ConfirmacionPorCorreoTests(Arnes arnes)
{
    private const string Correo = "ana@correo.com";

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Con_correo_sale_un_correo_al_Paciente_y_queda_una_fila_Confirmacion_Enviado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        var cita = Assert.Single(await escenario.CitasAsync());
        var evento = Assert.Single(await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, cita.Id, Cancelacion));

        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var correo = Assert.Single(Arnes.Canal(escenario.Web).Enviados);
        Assert.Equal(Correo, correo.Destinatario);
        Assert.Equal("Tu cita está confirmada: Martes 3 de noviembre, 4:00 pm", correo.Asunto);

        var mensaje = Assert.Single(await MensajesAsync(escenario));
        Assert.Equal(7, mensaje.Id.Version);
        Assert.Equal(evento.Id, mensaje.EventoId);
        Assert.Equal(cita.Id, mensaje.CitaId);
        Assert.Equal("Confirmacion", mensaje.Tipo);
        Assert.Equal("correo", mensaje.Canal);
        Assert.Equal("Enviado", mensaje.Estado);
        Assert.Equal(Ahora, mensaje.RegistradoEn);
        Assert.Equal("Entregado", (await Consultas.EventoAsync(escenario.Cadena, AgendaDbContext.Esquema, evento.Id, Cancelacion))!.Estado);
    }

    [Fact]
    public async Task El_correo_lleva_fecha_hora_tipo_lugar_mapa_el_boton_al_Enlace_de_gestion_y_el_archivo_de_calendario()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        var cita = new CitaId(Assert.Single(await escenario.CitasAsync()).Id);

        await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion);

        var correo = Assert.Single(Arnes.Canal(escenario.Web).Enviados);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var agenda = ambito.ServiceProvider.GetRequiredService<IAgendaApi>();
        var enlace = agenda.ObtenerEnlaceDeGestion(cita);
        var calendario = await agenda.ObtenerArchivoDeCalendarioAsync(cita, Cancelacion);
        var consultorio = ContenidoApi.Consultorio;
        var mapa = ContenidoApi.BusquedaEnGoogleMaps(consultorio.Lugar, consultorio.Direccion, consultorio.Ciudad);
        Assert.Equal(mapa, consultorio.EnlaceDelMapa);

        // Lo mismo en las dos versiones; en el HTML, una vez quitado el escape de las entidades.
        var html = WebUtility.HtmlDecode(correo.Html);
        foreach (var version in new[] { html, correo.Texto })
        {
            foreach (var texto in new[]
            {
                "Dra. Selina Medina Castillo", "Traumatología y Ortopedia", "Hola, Ana Sofía López", "Tu cita está confirmada",
                "Martes 3 de noviembre", "4:00 pm", "Primera consulta",
                consultorio.Lugar, consultorio.Direccion, consultorio.Ciudad, "Abrir en el mapa", mapa, "Ver o cambiar mi cita", enlace,
            })
            {
                Assert.Contains(texto, version);
            }

            Assert.DoesNotContain("confirmar", version, StringComparison.OrdinalIgnoreCase);
        }

        // Un solo botón, al Enlace de gestión; el otro vínculo es el del mapa. Sin imágenes.
        Assert.Contains($"""<a href="{enlace}" """, html);
        Assert.Equal(2, CuantasVeces(html, "<a "));
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"Ver o cambiar mi cita: {enlace}", correo.Texto);
        Assert.Contains($"Abrir en el mapa: {mapa}", correo.Texto);

        var adjunto = Assert.Single(correo.Adjuntos);
        Assert.Equal("cita.ics", adjunto.NombreDeArchivo);
        Assert.Equal("text/calendar; charset=utf-8", adjunto.TipoDeContenido);
        Assert.NotNull(calendario);
        Assert.Equal(calendario.Contenido, Encoding.UTF8.GetString(adjunto.Contenido));
    }

    [Fact]
    public async Task Sin_correo_no_sale_ningun_correo_ni_queda_ninguna_fila()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando();
        comando.Correo = null;
        Assert.True((await escenario.AgendarAsync(comando)).EsExito);
        var cita = Assert.Single(await escenario.CitasAsync());

        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Empty(Arnes.Canal(escenario.Web).Enviados);
        Assert.Empty(await MensajesAsync(escenario));
        Assert.Equal("Entregado", Assert.Single(await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, cita.Id, Cancelacion)).Estado);
    }

    [Fact]
    public async Task El_mismo_evento_entregado_dos_veces_deja_un_solo_correo_y_una_sola_fila()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        // El outbox lo entrega otra vez, como si la marca de Entregado no hubiera quedado.
        await escenario.EjecutarAsync("update agenda.outbox set estado = 'Pendiente', entregado_en = null");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Single(Arnes.Canal(escenario.Web).Enviados);
        Assert.Single(await MensajesAsync(escenario));
        Assert.Equal("Entregado", await escenario.EscalarAsync<string>("select estado from agenda.outbox"));
    }

    [Fact]
    public async Task Con_el_canal_caido_la_reserva_queda_confirmada_no_queda_fila_y_el_outbox_reintenta_hasta_que_sale()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var canal = Arnes.Canal(escenario.Web);
        canal.Caido = true;

        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal("Agendada", cita.Estado);

        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var evento = Assert.Single(await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, cita.Id, Cancelacion));
        Assert.Equal("Pendiente", evento.Estado);
        Assert.Equal(1, evento.Intentos);
        Assert.Empty(canal.Enviados);
        Assert.Empty(await MensajesAsync(escenario));

        // El canal vuelve y pasa la espera del primer reintento: la siguiente entrega lo envía.
        canal.Caido = false;
        escenario.Reloj.Avanzar(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Equal(Correo, Assert.Single(canal.Enviados).Destinatario);
        Assert.Equal("Enviado", Assert.Single(await MensajesAsync(escenario)).Estado);
        Assert.Equal("Entregado", (await Consultas.EventoAsync(escenario.Cadena, AgendaDbContext.Esquema, evento.Id, Cancelacion))!.Estado);
    }

    [Fact]
    public async Task Una_Cita_del_Consultorio_recibe_la_misma_confirmacion()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var paciente = await escenario.PacienteDeAnaAsync();
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(16, 0), new TimeOnly(16, 30), paciente);
        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(CitaAgendada.OrigenConsultorio, cita.Origen);

        // La épica 5 la publicará al registrarla; aquí se escribe en el outbox como lo haría Agenda.
        var eventoId = Guid.CreateVersion7();
        var evento = new CitaAgendada(
            eventoId, Ahora, new CitaId(cita.Id), new PatientId(paciente), Actor.Administrador, cita.Inicio, cita.Fin,
            CitaAgendada.OrigenConsultorio);
        await PublicarAsync(escenario, evento);

        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var correo = Assert.Single(Arnes.Canal(escenario.Web).Enviados);
        Assert.Equal(Correo, correo.Destinatario);
        Assert.Equal("Tu cita está confirmada: Martes 3 de noviembre, 4:00 pm", correo.Asunto);
        Assert.Single(correo.Adjuntos);
        var mensaje = Assert.Single(await MensajesAsync(escenario));
        Assert.Equal(eventoId, mensaje.EventoId);
        Assert.Equal("Confirmacion", mensaje.Tipo);
    }

    [Fact]
    public async Task Sin_archivo_de_calendario_el_correo_sale_sin_adjunto()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        var cita = new CitaId(Assert.Single(await escenario.CitasAsync()).Id);

        // La entrega llega cuando la Cita ya empezó: Agenda ya no da su archivo.
        escenario.Reloj.Avanzar(TimeSpan.FromDays(2));
        await using (var ambito = escenario.Web.Services.CreateAsyncScope())
        {
            Assert.Null(await ambito.ServiceProvider.GetRequiredService<IAgendaApi>().ObtenerArchivoDeCalendarioAsync(cita, Cancelacion));
        }

        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var correo = Assert.Single(Arnes.Canal(escenario.Web).Enviados);
        Assert.Empty(correo.Adjuntos);
        Assert.Equal("Tu cita está confirmada: Martes 3 de noviembre, 4:00 pm", correo.Asunto);
        Assert.Equal("Enviado", Assert.Single(await MensajesAsync(escenario)).Estado);
    }

    [Fact]
    public async Task Ni_la_direccion_ni_el_nombre_van_a_los_registros_ni_a_la_fila_del_mensaje()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var canal = Arnes.Canal(escenario.Web);

        // Una entrega que falla y otra que sale: las dos dejan registros.
        canal.Caido = true;
        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion);
        canal.Caido = false;
        escenario.Reloj.Avanzar(TimeSpan.FromMinutes(1));
        await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion);
        Assert.Single(canal.Enviados);

        var fila = await escenario.EscalarAsync<string>("select row_to_json(m)::text from notificaciones.mensaje m");
        var registros = Arnes.Registros(escenario.Web).Select(r => r.Mensaje).ToList();
        Assert.NotEmpty(registros);
        foreach (var dato in new[] { Correo, "correo.com", "Ana Sofía", "López", "Lopez", "4441234567", "rodilla", "Tu cita está" })
        {
            Assert.DoesNotContain(dato, fila, StringComparison.OrdinalIgnoreCase);
            Assert.All(registros, r => Assert.DoesNotContain(dato, r, StringComparison.OrdinalIgnoreCase));
        }

        // Lo que se pase a un registro por error tampoco los lleva.
        var correo = Assert.Single(canal.Enviados);
        Assert.DoesNotContain(Correo, correo.ToString());
        Assert.DoesNotContain("Ana", correo.ToString());
    }

    [Fact]
    public void La_fecha_y_la_hora_del_correo_se_escriben_igual_que_en_las_pantallas()
    {
        for (var fecha = new DateOnly(2026, 1, 1); fecha.Year == 2026; fecha = fecha.AddDays(1))
        {
            Assert.Equal(FormatoDeCita.Largo(fecha), FormatoDeFechaEnCorreo.Largo(fecha));
        }

        for (var minutos = 0; minutos < 24 * 60; minutos += 15)
        {
            var hora = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutos));
            Assert.Equal(FormatoDeCita.Hora(hora), FormatoDeFechaEnCorreo.Hora(hora));
        }
    }

    [Fact]
    public async Task Si_el_servidor_SMTP_no_responde_el_canal_real_lanza_sin_la_direccion_ni_el_asunto()
    {
        // Un puerto local que nadie escucha: se pide uno libre y se suelta.
        var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        escucha.Stop();

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{CanalDeCorreoSmtp.CadenaDeConexion}"] = $"Endpoint=smtp://127.0.0.1:{puerto}",
            })
            .Build();
        var canal = new CanalDeCorreoSmtp(
            configuracion, Options.Create(new OpcionesDeNotificaciones { Remitente = "citas@consultorio.local" }));
        const string asunto = "Tu cita está confirmada: Martes 3 de noviembre, 4:00 pm";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            canal.EnviarAsync(new MensajeDeCorreo(Correo, asunto, "<p>Hola, Ana</p>", "Hola, Ana", []), Cancelacion));

        foreach (var texto in new[] { error.Message, error.ToString() })
        {
            Assert.DoesNotContain(Correo, texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(asunto, texto, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Null(error.InnerException);
    }

    private static Task PublicarAsync(Escenario escenario, CitaAgendada evento)
    {
        var contenido = JsonSerializer.Serialize(evento).Replace("'", "''");
        return escenario.EjecutarAsync(
            $"""
            insert into agenda.outbox
                (id, tipo, contenido, ocurrido_en, clave_de_orden, estado, intentos, proximo_intento_en, entregado_en)
            values
                ('{evento.EventoId}', '{nameof(CitaAgendada)}', '{contenido}'::jsonb, '{evento.OcurridoEn:O}',
                 '{evento.CitaId.Valor}', 'Pendiente', 0, '{evento.OcurridoEn:O}', null)
            """);
    }

    private static Task<List<MensajeLeido>> MensajesAsync(Escenario escenario) => escenario.FilasAsync(
        "select id, evento_id, cita_id, tipo, canal, estado, registrado_en from notificaciones.mensaje order by id",
        l => new MensajeLeido(
            l.GetGuid(0), l.IsDBNull(1) ? null : l.GetGuid(1), l.IsDBNull(2) ? null : l.GetGuid(2),
            l.GetString(3), l.GetString(4), l.GetString(5), l.GetFieldValue<DateTimeOffset>(6)));

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
