using System.Net;
using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Contenido;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// El Recordatorio del día anterior (CAP-18, FR-22): Notificaciones mantiene <c>notificaciones.recordatorio_programado</c>
/// con <see cref="CitaAgendada"/>, <see cref="CitaReprogramada"/> y <see cref="CitaCancelada"/>, y la vuelta de
/// <see cref="EnviosProgramados"/> envía los que ya tocan. Contra PostgreSQL real, con el reloj de prueba y las vueltas a
/// mano. La Cita es la de Ana, con correo, el jueves 5 de noviembre a las 4:00 pm, agendada el lunes 2 a las 12:00
/// local; la hora del Recordatorio es la sembrada, 09:00.
/// </summary>
public sealed class RecordatorioTests(Arnes arnes)
{
    private const string Correo = "ana@correo.com";

    private static readonly DateOnly Jueves = new(2026, 11, 5);

    /// <summary>El miércoles 4 a las 09:00 local.</summary>
    private static readonly DateTimeOffset MiercolesALasNueve = Local(new DateOnly(2026, 11, 4), new TimeOnly(9, 0));

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public void La_hora_del_Recordatorio_es_el_dia_local_anterior_a_la_hora_configurada()
    {
        var inicio = Local(Jueves, new TimeOnly(16, 0));

        Assert.Equal(new DateTimeOffset(2026, 11, 4, 15, 0, 0, TimeSpan.Zero), HoraDelRecordatorio.ProgramadoPara(inicio, new TimeOnly(9, 0)));

        // Una Cita temprano: en UTC ya es el mismo día, pero cuenta el día local.
        var temprano = Local(Jueves, new TimeOnly(8, 0));
        Assert.Equal(MiercolesALasNueve, HoraDelRecordatorio.ProgramadoPara(temprano, new TimeOnly(9, 0)));
    }

    [Fact]
    public async Task Al_entregarse_CitaAgendada_queda_una_fila_para_el_miercoles_a_las_nueve_local()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarElJuevesAsync(escenario);

        var fila = Assert.Single(await FilasAsync(escenario));
        Assert.Equal(cita.Id, fila.CitaId);
        Assert.Equal(cita.PacienteId, fila.PacienteId);
        Assert.Equal(cita.Inicio, fila.Inicio);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 15, 0, 0, TimeSpan.Zero), fila.ProgramadoPara);
    }

    [Fact]
    public async Task Sale_a_la_hora_una_sola_vez_y_no_antes()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarElJuevesAsync(escenario);

        IrA(escenario, MiercolesALasNueve.AddMinutes(-1));
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Empty(Recordatorios(escenario));

        IrA(escenario, MiercolesALasNueve);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));

        var correo = Assert.Single(Recordatorios(escenario));
        Assert.Equal(Correo, correo.Destinatario);
        Assert.Equal("Mañana tienes cita: 4:00 pm", correo.Asunto);
        Assert.Empty(correo.Adjuntos);
        Assert.Empty(await FilasAsync(escenario));

        var mensaje = Assert.Single(await MensajesDeRecordatorioAsync(escenario));
        Assert.Null(mensaje.EventoId);
        Assert.Equal(cita.Id, mensaje.CitaId);
        Assert.Equal("correo", mensaje.Canal);
        Assert.Equal("Enviado", mensaje.Estado);
        Assert.Equal(MiercolesALasNueve, mensaje.RegistradoEn);

        IrA(escenario, MiercolesALasNueve.AddMinutes(1));
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Single(Recordatorios(escenario));
        Assert.Single(await MensajesDeRecordatorioAsync(escenario));
    }

    [Fact]
    public async Task El_correo_lleva_fecha_hora_tipo_lugar_mapa_y_el_boton_al_Enlace_de_gestion()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarElJuevesAsync(escenario);
        IrA(escenario, MiercolesALasNueve);
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        var correo = Assert.Single(Recordatorios(escenario));
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var enlace = ambito.ServiceProvider.GetRequiredService<IAgendaApi>().ObtenerEnlaceDeGestion(new CitaId(cita.Id));
        var consultorio = ContenidoApi.Consultorio;
        var mapa = consultorio.EnlaceDelMapa!;

        var html = WebUtility.HtmlDecode(correo.Html);
        foreach (var version in new[] { html, correo.Texto })
        {
            foreach (var texto in new[]
            {
                "Dra. Selina Medina Castillo", "Traumatología y Ortopedia", "Hola, Ana Sofía López", "Mañana tienes cita",
                "Jueves 5 de noviembre", "4:00 pm", "Primera consulta",
                consultorio.Lugar, consultorio.Direccion, consultorio.Ciudad, "Abrir en el mapa", mapa, "Ver o cambiar mi cita", enlace,
            })
            {
                Assert.Contains(texto, version);
            }

            Assert.DoesNotContain("confirmar", version, StringComparison.OrdinalIgnoreCase);
        }

        // Un solo botón, al Enlace de gestión; el otro vínculo es el del mapa. Sin imágenes.
        Assert.Contains($"""<a href="{enlace}" """, html);
        Assert.Equal(2, html.Split("<a ").Length - 1);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Al_reprogramar_la_fila_pasa_al_dia_anterior_del_nuevo_inicio_y_el_miercoles_no_sale_nada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarElJuevesAsync(escenario);
        var inicioNuevo = Local(new DateOnly(2026, 11, 6), new TimeOnly(17, 0));

        await PublicarAsync(escenario, Reprogramada(escenario, cita, inicioNuevo));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var fila = Assert.Single(await FilasAsync(escenario));
        Assert.Equal(inicioNuevo, fila.Inicio);
        Assert.Equal(Local(Jueves, new TimeOnly(9, 0)), fila.ProgramadoPara);

        IrA(escenario, MiercolesALasNueve);
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Empty(Recordatorios(escenario));

        IrA(escenario, Local(Jueves, new TimeOnly(9, 0)));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        var correo = Assert.Single(Recordatorios(escenario));
        Assert.Equal("Mañana tienes cita: 5:00 pm", correo.Asunto);
        Assert.Contains("Viernes 6 de noviembre", correo.Texto);
    }

    [Fact]
    public async Task Al_cancelar_no_queda_fila_y_no_sale_nada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarElJuevesAsync(escenario);

        var cancelada = new CitaCancelada(
            Guid.CreateVersion7(), escenario.Reloj.UtcNow, new CitaId(cita.Id), new PatientId(cita.PacienteId), Actor.Paciente,
            cita.Inicio, CitaCancelada.MotivoPaciente);
        await PublicarAsync(escenario, cancelada);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Empty(await FilasAsync(escenario));
        IrA(escenario, MiercolesALasNueve);
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Empty(Recordatorios(escenario));
        Assert.Empty(await MensajesDeRecordatorioAsync(escenario));
    }

    [Fact]
    public async Task Si_ya_paso_la_hora_al_agendar_no_queda_fila_y_queda_un_solo_Omitido_aunque_el_evento_llegue_dos_veces()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var paciente = await escenario.PacienteDeAnaAsync();

        // El miércoles a las 10:00 local, para el jueves: el Recordatorio era a las 9:00.
        IrA(escenario, MiercolesALasNueve.AddHours(1));
        var cita = new CitaId(Guid.CreateVersion7());
        var inicio = Local(Jueves, new TimeOnly(16, 0));
        var agendada = new CitaAgendada(
            Guid.CreateVersion7(), escenario.Reloj.UtcNow, cita, new PatientId(paciente), Actor.Paciente, inicio,
            inicio.AddMinutes(30), CitaAgendada.OrigenEnLinea);
        await PublicarAsync(escenario, agendada);

        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        await escenario.EjecutarAsync("update agenda.outbox set estado = 'Pendiente', entregado_en = null");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Empty(await FilasAsync(escenario));
        var mensaje = Assert.Single(await MensajesDeRecordatorioAsync(escenario));
        Assert.Equal(agendada.EventoId, mensaje.EventoId);
        Assert.Equal(cita.Valor, mensaje.CitaId);
        Assert.Equal("Omitido", mensaje.Estado);
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Empty(Recordatorios(escenario));
    }

    [Fact]
    public async Task El_mismo_CitaAgendada_y_el_mismo_CitaReprogramada_entregados_dos_veces_dejan_una_sola_fila()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarElJuevesAsync(escenario);
        var inicioNuevo = Local(new DateOnly(2026, 11, 6), new TimeOnly(17, 0));
        await PublicarAsync(escenario, Reprogramada(escenario, cita, inicioNuevo));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        // El outbox los entrega otra vez, en su orden, como si la marca de Entregado no hubiera quedado.
        await escenario.EjecutarAsync("update agenda.outbox set estado = 'Pendiente', entregado_en = null");
        Assert.Equal(2, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var fila = Assert.Single(await FilasAsync(escenario));
        Assert.Equal(inicioNuevo, fila.Inicio);
        Assert.Equal(Local(Jueves, new TimeOnly(9, 0)), fila.ProgramadoPara);
        Assert.Empty(await MensajesDeRecordatorioAsync(escenario));
    }

    [Fact]
    public async Task Un_CitaAgendada_que_se_reintenta_despues_de_enviado_el_Recordatorio_no_lo_envia_otra_vez()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarElJuevesAsync(escenario);
        IrA(escenario, MiercolesALasNueve);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));

        // El outbox lo entrega otra vez (otro handler del evento falló y se reintenta).
        await escenario.EjecutarAsync("update agenda.outbox set estado = 'Pendiente', entregado_en = null");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Empty(await FilasAsync(escenario));
        IrA(escenario, MiercolesALasNueve.AddMinutes(1));
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Single(Recordatorios(escenario));
        Assert.Single(await MensajesDeRecordatorioAsync(escenario));
    }

    [Fact]
    public async Task Un_CitaReprogramada_que_se_reintenta_despues_de_enviado_el_Recordatorio_no_lo_envia_otra_vez()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarElJuevesAsync(escenario);
        await PublicarAsync(escenario, Reprogramada(escenario, cita, Local(new DateOnly(2026, 11, 6), new TimeOnly(17, 0))));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        var juevesALasNueve = Local(Jueves, new TimeOnly(9, 0));
        IrA(escenario, juevesALasNueve);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));

        // El outbox entrega otra vez los dos eventos, en su orden.
        await escenario.EjecutarAsync("update agenda.outbox set estado = 'Pendiente', entregado_en = null");
        Assert.Equal(2, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Empty(await FilasAsync(escenario));
        IrA(escenario, juevesALasNueve.AddMinutes(1));
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Single(Recordatorios(escenario));
        Assert.Single(await MensajesDeRecordatorioAsync(escenario));
    }

    [Fact]
    public async Task Si_la_Cita_ya_empezo_cuando_toca_la_vuelta_borra_la_fila_sin_enviar_nada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarElJuevesAsync(escenario);

        // La web estuvo caída desde antes del miércoles a las 9:00 hasta que empezó la Cita.
        IrA(escenario, cita.Inicio);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));

        Assert.Empty(Recordatorios(escenario));
        Assert.Empty(await FilasAsync(escenario));
        Assert.Empty(await MensajesDeRecordatorioAsync(escenario));
    }

    [Fact]
    public async Task Reprogramar_a_una_hora_cuyo_Recordatorio_ya_paso_borra_la_fila_y_deja_un_solo_Omitido()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarElJuevesAsync(escenario);

        // El lunes a las 12:00 local, al martes: el Recordatorio habría sido el lunes a las 9:00.
        var reprogramada = Reprogramada(escenario, cita, Local(new DateOnly(2026, 11, 3), new TimeOnly(16, 0)));
        await PublicarAsync(escenario, reprogramada);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        await escenario.EjecutarAsync(
            $"update agenda.outbox set estado = 'Pendiente', entregado_en = null where id = '{reprogramada.EventoId}'");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Empty(await FilasAsync(escenario));
        var mensaje = Assert.Single(await MensajesDeRecordatorioAsync(escenario));
        Assert.Equal(reprogramada.EventoId, mensaje.EventoId);
        Assert.Equal("Omitido", mensaje.Estado);
    }

    [Fact]
    public async Task Sin_correo_la_vuelta_borra_la_fila_sin_enviar_nada()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando(Jueves);
        comando.Correo = null;
        Assert.True((await escenario.AgendarAsync(comando)).EsExito);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        Assert.Single(await FilasAsync(escenario));

        IrA(escenario, MiercolesALasNueve);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));

        Assert.Empty(Arnes.Canal(escenario.Web).Enviados);
        Assert.Empty(await FilasAsync(escenario));
        Assert.Empty(await MensajesDeRecordatorioAsync(escenario));
    }

    [Fact]
    public async Task Con_el_canal_caido_la_fila_sigue_y_sale_en_la_siguiente_vuelta()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarElJuevesAsync(escenario);
        var canal = Arnes.Canal(escenario.Web);
        canal.Caido = true;

        IrA(escenario, MiercolesALasNueve);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));

        Assert.Empty(Recordatorios(escenario));
        Assert.Single(await FilasAsync(escenario));
        Assert.Empty(await MensajesDeRecordatorioAsync(escenario));
        Assert.Contains(Arnes.Registros(escenario.Web), r => r.Nivel == LogLevel.Warning && r.Categoria == typeof(EnviosProgramados).FullName);

        canal.Caido = false;
        IrA(escenario, MiercolesALasNueve.AddMinutes(1));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));

        Assert.Equal(Correo, Assert.Single(Recordatorios(escenario)).Destinatario);
        Assert.Empty(await FilasAsync(escenario));
        Assert.Equal("Enviado", Assert.Single(await MensajesDeRecordatorioAsync(escenario)).Estado);
    }

    [Fact]
    public async Task Ni_la_direccion_ni_el_nombre_van_a_los_registros_ni_a_las_filas()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarElJuevesAsync(escenario);
        var canal = Arnes.Canal(escenario.Web);

        // Una vuelta que falla y otra que sale: las dos dejan registros.
        canal.Caido = true;
        IrA(escenario, MiercolesALasNueve);
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        var filaPendiente = await escenario.EscalarAsync<string>("select row_to_json(r)::text from notificaciones.recordatorio_programado r");
        canal.Caido = false;
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        Assert.Single(Recordatorios(escenario));

        var mensajes = await escenario.EscalarAsync<string>(
            "select json_agg(m)::text from notificaciones.mensaje m where tipo = 'Recordatorio'");
        var registros = Arnes.Registros(escenario.Web)
            .Where(r => r.Categoria.StartsWith("DraSelina", StringComparison.Ordinal))
            .Select(r => r.Mensaje)
            .ToList();
        Assert.NotEmpty(registros);
        foreach (var dato in new[] { Correo, "correo.com", "Ana Sofía", "López", "Lopez", "4441234567", "rodilla", "Mañana tienes" })
        {
            Assert.DoesNotContain(dato, filaPendiente, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(dato, mensajes, StringComparison.OrdinalIgnoreCase);
            Assert.All(registros, r => Assert.DoesNotContain(dato, r, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Una_Cita_del_Consultorio_se_programa_igual_que_una_en_linea()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var paciente = await escenario.PacienteDeAnaAsync();
        await escenario.InsertarCitaDelConsultorioAsync(Jueves, new TimeOnly(16, 0), new TimeOnly(16, 30), paciente);
        var cita = Assert.Single(await escenario.CitasAsync());

        await PublicarAsync(escenario, new CitaAgendada(
            Guid.CreateVersion7(), escenario.Reloj.UtcNow, new CitaId(cita.Id), new PatientId(paciente), Actor.Administrador,
            cita.Inicio, cita.Fin, CitaAgendada.OrigenConsultorio));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var fila = Assert.Single(await FilasAsync(escenario));
        Assert.Equal(cita.Id, fila.CitaId);
        Assert.Equal(MiercolesALasNueve, fila.ProgramadoPara);

        IrA(escenario, MiercolesALasNueve);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Equal("Mañana tienes cita: 4:00 pm", Assert.Single(Recordatorios(escenario)).Asunto);
    }

    [Fact]
    public async Task Sin_reloj_de_prueba_el_servicio_da_sus_vueltas_por_su_cuenta_sin_registrar_errores()
    {
        var cadena = await arnes.CrearBaseMigradaAsync(Cancelacion);

        // Un Recordatorio que ya toca, de un Paciente que no existe: la vuelta lo borra sin enviar nada. Que
        // desaparezca dice que el servicio dio su vuelta al arrancar.
        await using (var conexion = new NpgsqlConnection(cadena))
        {
            await conexion.OpenAsync(Cancelacion);
            await using var comando = new NpgsqlCommand(
                $"""
                insert into notificaciones.recordatorio_programado (cita_id, paciente_id, inicio, programado_para)
                values ('{Guid.CreateVersion7()}', '{Guid.CreateVersion7()}', now() + interval '1 day', now() - interval '1 minute')
                """,
                conexion);
            await comando.ExecuteNonQueryAsync(Cancelacion);
        }

        await using var web = arnes.ArrancarWebConEnviosProgramados(cadena);
        _ = web.Services;

        var limite = DateTime.UtcNow.AddSeconds(30);
        long quedan;
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), Cancelacion);
            await using var conexion = new NpgsqlConnection(cadena);
            await conexion.OpenAsync(Cancelacion);
            await using var comando = new NpgsqlCommand("select count(*) from notificaciones.recordatorio_programado", conexion);
            quedan = (long)(await comando.ExecuteScalarAsync(Cancelacion))!;
        }
        while (quedan > 0 && DateTime.UtcNow < limite);

        Assert.Equal(0, quedan);

        // Según la hora real, la vuelta también puede enviar la Agenda del día (entrada 4.6): solo cuentan los Recordatorios.
        Assert.DoesNotContain(Arnes.Canal(web).Enviados, c => c.Asunto.StartsWith(PlantillaDeRecordatorio.Titulo, StringComparison.Ordinal));
        Assert.DoesNotContain(Arnes.Registros(web), r =>
            r.Nivel >= LogLevel.Warning && r.Categoria.Contains("EnviosProgramados", StringComparison.Ordinal));

        await web.DisposeAsync();
        NpgsqlConnection.ClearPool(new NpgsqlConnection(cadena));
    }

    // Agenda a Ana el jueves 5 a las 16:00 y entrega su CitaAgendada (la confirmación y el Recordatorio).
    private static async Task<CitaLeida> AgendarElJuevesAsync(Escenario escenario)
    {
        Assert.True((await escenario.AgendarAsync(Comando(Jueves))).EsExito);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        return Assert.Single(await escenario.CitasAsync());
    }

    private static CitaReprogramada Reprogramada(Escenario escenario, CitaLeida cita, DateTimeOffset inicioNuevo) => new(
        Guid.CreateVersion7(), escenario.Reloj.UtcNow, new CitaId(cita.Id), new PatientId(cita.PacienteId), Actor.Paciente,
        cita.Inicio, inicioNuevo, inicioNuevo.AddMinutes(30));

    // Lo que hoy envió la web como Recordatorio; la confirmación de la 4.1 también sale por el canal.
    private static List<MensajeDeCorreo> Recordatorios(Escenario escenario) =>
        [.. Arnes.Canal(escenario.Web).Enviados.Where(c => c.Asunto.StartsWith(PlantillaDeRecordatorio.Titulo, StringComparison.Ordinal))];

    private static DateTimeOffset Local(DateOnly fecha, TimeOnly hora) => new(
        TimeZoneInfo.ConvertTimeToUtc(fecha.ToDateTime(hora, DateTimeKind.Unspecified), ZonaDelConsultorio.Zona), TimeSpan.Zero);

    private static void IrA(Escenario escenario, DateTimeOffset instante) => escenario.Reloj.Avanzar(instante - escenario.Reloj.UtcNow);

    // Escribe el evento en el outbox de Agenda como lo haría Agenda, listo para la siguiente vuelta de entrega.
    private static Task PublicarAsync<TEvento>(Escenario escenario, TEvento evento)
        where TEvento : notnull
    {
        var (eventoId, ocurridoEn, citaId) = evento switch
        {
            CitaAgendada e => (e.EventoId, e.OcurridoEn, e.CitaId),
            CitaReprogramada e => (e.EventoId, e.OcurridoEn, e.CitaId),
            CitaCancelada e => (e.EventoId, e.OcurridoEn, e.CitaId),
            _ => throw new ArgumentException("Evento no previsto.", nameof(evento)),
        };
        var contenido = JsonSerializer.Serialize(evento).Replace("'", "''");
        return escenario.EjecutarAsync(
            $"""
            insert into agenda.outbox
                (id, tipo, contenido, ocurrido_en, clave_de_orden, estado, intentos, proximo_intento_en, entregado_en)
            values
                ('{eventoId}', '{typeof(TEvento).Name}', '{contenido}'::jsonb, '{ocurridoEn:O}',
                 '{citaId.Valor}', 'Pendiente', 0, '{escenario.Reloj.UtcNow:O}', null)
            """);
    }

    private static Task<List<FilaLeida>> FilasAsync(Escenario escenario) => escenario.FilasAsync(
        "select cita_id, paciente_id, inicio, programado_para from notificaciones.recordatorio_programado order by programado_para",
        l => new FilaLeida(l.GetGuid(0), l.GetGuid(1), l.GetFieldValue<DateTimeOffset>(2), l.GetFieldValue<DateTimeOffset>(3)));

    private static Task<List<MensajeLeido>> MensajesDeRecordatorioAsync(Escenario escenario) => escenario.FilasAsync(
        "select evento_id, cita_id, canal, estado, registrado_en from notificaciones.mensaje where tipo = 'Recordatorio' order by id",
        l => new MensajeLeido(
            l.IsDBNull(0) ? null : l.GetGuid(0), l.IsDBNull(1) ? null : l.GetGuid(1),
            l.GetString(2), l.GetString(3), l.GetFieldValue<DateTimeOffset>(4)));

    private sealed record FilaLeida(Guid CitaId, Guid PacienteId, DateTimeOffset Inicio, DateTimeOffset ProgramadoPara);

    private sealed record MensajeLeido(Guid? EventoId, Guid? CitaId, string Canal, string Estado, DateTimeOffset RegistradoEn);
}
