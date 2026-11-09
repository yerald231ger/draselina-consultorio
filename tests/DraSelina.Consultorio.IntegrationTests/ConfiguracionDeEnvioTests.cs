using System.Security.Claims;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Contracts;
using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Horas y destinatarios configurables (CAP-16; entrada 4.7): la consulta <see cref="ConfiguracionDeEnvioVigente"/> y el
/// comando <see cref="CambiarConfiguracionDeEnvio"/>, solo del Administrador (AD-22). El siguiente envío ya usa los
/// valores nuevos, y al cambiar la hora del Recordatorio los pendientes se mueven (o se omiten si ya pasó). Contra
/// PostgreSQL real, con el reloj de <see cref="ReservaEnLineaTests"/> (lunes 2 de noviembre a las 12:00 local). El
/// usuario del ámbito se fija como en <see cref="ActorYAdministracionTests"/>.
/// </summary>
public sealed class ConfiguracionDeEnvioTests(Arnes arnes)
{
    private const string Doctora = "doctora@consultorio.local";

    private const string Administrador = "administrador@consultorio.local";

    private static readonly DateOnly Lunes = new(2026, 11, 2);

    private static readonly DateOnly Martes = new(2026, 11, 3);

    private static readonly DateOnly Miercoles = new(2026, 11, 4);

    private static readonly DateOnly Jueves = new(2026, 11, 5);

    private static readonly ConfiguracionDeEnvioValores Sembrada = new(new TimeOnly(9, 0), new TimeOnly(14, 0), Doctora, Administrador);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    private static ClaimsPrincipal Autenticado =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "administradora")], authenticationType: "Prueba"));

    [Fact]
    public async Task Sin_sesion_el_comando_y_la_consulta_vuelven_no_autorizado_y_nada_cambia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        await using (var ambito = escenario.Web.Services.CreateAsyncScope())
        {
            SinSesion(ambito);
            var cambio = await Comando(ambito).HandleAsync(
                Cambio(horaDelRecordatorio: new TimeOnly(10, 0), correoDeLaDoctora: "otra@consultorio.local"), Cancelacion);
            var lectura = await Consulta(ambito).HandleAsync(new ConfiguracionDeEnvioVigente(), Cancelacion);

            Assert.Equal(FormaDeResultado.NoAutorizado, cambio.Forma);
            Assert.Empty(cambio.Errores);
            Assert.Equal(FormaDeResultado.NoAutorizado, lectura.Forma);
        }

        Assert.Equal(Sembrada, await FilaAsync(escenario));
    }

    [Fact]
    public async Task Con_sesion_la_consulta_lee_los_valores_sembrados()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        Assert.Equal(Sembrada, await LeerAsync(escenario));
    }

    [Fact]
    public async Task El_comando_guarda_y_devuelve_los_valores_y_la_consulta_los_lee()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var esperada = new ConfiguracionDeEnvioValores(new TimeOnly(10, 30), new TimeOnly(13, 0), "dra@clinica.mx", "admin@clinica.mx");

        var resultado = await CambiarAsync(escenario, Cambio(
            esperada.HoraDelRecordatorio, esperada.HoraDeLaAgendaDelDia, "  dra@clinica.mx ", esperada.CorreoDelAdministrador));

        Assert.True(resultado.EsExito, $"{resultado.Forma}");
        Assert.Equal(esperada, resultado.Valor);
        Assert.Equal(esperada, await FilaAsync(escenario));
        Assert.Equal(esperada, await LeerAsync(escenario));
    }

    [Fact]
    public async Task Con_la_Agenda_del_dia_a_las_13_sale_a_las_13_y_no_antes()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await CambiarAsync(escenario, Cambio(horaDeLaAgendaDelDia: new TimeOnly(13, 0)))).EsExito);

        IrA(escenario, Martes, new TimeOnly(12, 59));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        Assert.Empty(Agendas(escenario));

        IrA(escenario, Martes, new TimeOnly(13, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        Assert.Equal("Tus citas de hoy: Martes 3 de noviembre", Assert.Single(Agendas(escenario)).Asunto);
    }

    [Fact]
    public async Task Con_otro_correo_de_la_doctora_la_siguiente_Agenda_del_dia_va_a_esa_direccion()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await CambiarAsync(escenario, Cambio(correoDeLaDoctora: "dra@clinica.mx"))).EsExito);

        IrA(escenario, Martes, new TimeOnly(14, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        Assert.Equal("dra@clinica.mx", Assert.Single(Agendas(escenario)).Destinatario);
    }

    [Fact]
    public async Task Con_otro_correo_del_Administrador_la_siguiente_alerta_tardia_va_ahi()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var agendada = await escenario.AgendarAsync(ReservaEnLineaTests.Comando(Martes, new TimeOnly(16, 0)));
        Assert.True(agendada.EsExito, $"{agendada.Forma} {agendada.Codigo}");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        Assert.True((await CambiarAsync(escenario, Cambio(correoDelAdministrador: "admin@clinica.mx"))).EsExito);

        // Una hora antes de la Cita: la cancelación es tardía.
        IrA(escenario, Martes, new TimeOnly(15, 0));
        Assert.True((await CancelarAsync(escenario, agendada.Valor)).Tardia);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var alerta = Assert.Single(Arnes.Canal(escenario.Web).Enviados, c => c.Asunto.StartsWith(PlantillaDeAlertaTardia.Titulo, StringComparison.Ordinal));
        Assert.Equal("admin@clinica.mx", alerta.Destinatario);
    }

    [Fact]
    public async Task Al_cambiar_la_hora_del_Recordatorio_el_pendiente_se_mueve_y_sale_a_la_hora_nueva()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarAsync(escenario, Jueves);
        Assert.Equal(Local(Miercoles, new TimeOnly(9, 0)), Assert.Single(await FilasAsync(escenario)).ProgramadoPara);

        Assert.True((await CambiarAsync(escenario, Cambio(horaDelRecordatorio: new TimeOnly(10, 0)))).EsExito);

        var fila = Assert.Single(await FilasAsync(escenario));
        Assert.Equal((cita.Id, cita.PacienteId, cita.Inicio), (fila.CitaId, fila.PacienteId, fila.Inicio));
        Assert.Equal(Local(Miercoles, new TimeOnly(10, 0)), fila.ProgramadoPara);

        IrA(escenario, Miercoles, new TimeOnly(9, 0));
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Empty(Recordatorios(escenario));

        IrA(escenario, Miercoles, new TimeOnly(10, 0));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Single(Recordatorios(escenario));
        Assert.Equal("Enviado", Assert.Single(await MensajesDeRecordatorioAsync(escenario)).Estado);
    }

    [Fact]
    public async Task Un_Recordatorio_que_queda_con_su_hora_ya_pasada_se_borra_y_queda_Omitido()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // A las 13:00, el Recordatorio de la Cita del martes cae el lunes a las 13:00: todavía no pasa.
        Assert.True((await CambiarAsync(escenario, Cambio(horaDelRecordatorio: new TimeOnly(13, 0)))).EsExito);
        var cita = await AgendarAsync(escenario, Martes);
        Assert.Equal(Local(Lunes, new TimeOnly(13, 0)), Assert.Single(await FilasAsync(escenario)).ProgramadoPara);

        // A las 11:00, el lunes a las 11:00 ya pasó.
        Assert.True((await CambiarAsync(escenario, Cambio(horaDelRecordatorio: new TimeOnly(11, 0)))).EsExito);

        Assert.Empty(await FilasAsync(escenario));
        var mensaje = Assert.Single(await MensajesDeRecordatorioAsync(escenario));
        Assert.Equal(("Omitido", (Guid?)cita.Id, (Guid?)null, "correo"), (mensaje.Estado, mensaje.CitaId, mensaje.EventoId, mensaje.Canal));
        Assert.Equal(escenario.Reloj.UtcNow, mensaje.RegistradoEn);

        IrA(escenario, Lunes, new TimeOnly(13, 0));
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Empty(Recordatorios(escenario));
    }

    [Fact]
    public async Task Un_CitaAgendada_que_se_reintenta_despues_del_Omitido_por_el_cambio_no_lo_vuelve_a_programar()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // Agendada el lunes a las 12:00, con el Recordatorio del lunes a las 13:00.
        Assert.True((await CambiarAsync(escenario, Cambio(horaDelRecordatorio: new TimeOnly(13, 0)))).EsExito);
        await AgendarAsync(escenario, Martes);

        // A las 12:30, la hora pasa a las 12:15: ya pasó y se omite. Pero es posterior al OcurridoEn del evento.
        IrA(escenario, Lunes, new TimeOnly(12, 30));
        Assert.True((await CambiarAsync(escenario, Cambio(horaDelRecordatorio: new TimeOnly(12, 15)))).EsExito);
        Assert.Equal("Omitido", Assert.Single(await MensajesDeRecordatorioAsync(escenario)).Estado);

        await escenario.EjecutarAsync("update agenda.outbox set estado = 'Pendiente', entregado_en = null");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Empty(await FilasAsync(escenario));
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Empty(Recordatorios(escenario));
        Assert.Single(await MensajesDeRecordatorioAsync(escenario));
    }

    [Fact]
    public async Task Un_CitaReprogramada_que_se_reintenta_despues_del_Omitido_por_el_cambio_no_lo_vuelve_a_programar()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // Agendada para el jueves y reprogramada al martes, el lunes a las 12:00: el Recordatorio, el lunes a las 13:00.
        Assert.True((await CambiarAsync(escenario, Cambio(horaDelRecordatorio: new TimeOnly(13, 0)))).EsExito);
        var cita = await AgendarAsync(escenario, Jueves);
        var inicioNuevo = Local(Martes, new TimeOnly(16, 0));
        await PublicarReprogramadaAsync(escenario, new CitaReprogramada(
            Guid.CreateVersion7(), escenario.Reloj.UtcNow, new CitaId(cita.Id), new PatientId(cita.PacienteId), Actor.Paciente,
            cita.Inicio, inicioNuevo, inicioNuevo.AddMinutes(30)));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        Assert.Equal(Local(Lunes, new TimeOnly(13, 0)), Assert.Single(await FilasAsync(escenario)).ProgramadoPara);

        IrA(escenario, Lunes, new TimeOnly(12, 30));
        Assert.True((await CambiarAsync(escenario, Cambio(horaDelRecordatorio: new TimeOnly(12, 15)))).EsExito);
        Assert.Empty(await FilasAsync(escenario));

        // El outbox entrega otra vez los dos eventos, en su orden.
        await escenario.EjecutarAsync("update agenda.outbox set estado = 'Pendiente', entregado_en = null");
        Assert.Equal(2, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Empty(await FilasAsync(escenario));
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));
        Assert.Empty(Recordatorios(escenario));
        Assert.Equal("Omitido", Assert.Single(await MensajesDeRecordatorioAsync(escenario)).Estado);
    }

    [Fact]
    public async Task Sin_cambiar_la_hora_del_Recordatorio_los_pendientes_se_quedan_igual()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAsync(escenario, Jueves);
        var antes = Assert.Single(await FilasAsync(escenario));

        Assert.True((await CambiarAsync(escenario, Cambio(horaDeLaAgendaDelDia: new TimeOnly(15, 0)))).EsExito);

        Assert.Equal(antes, Assert.Single(await FilasAsync(escenario)));
        Assert.Empty(await MensajesDeRecordatorioAsync(escenario));
    }

    [Theory]
    [InlineData(nameof(CambiarConfiguracionDeEnvio.HoraDelRecordatorio), "Escribe la hora del Recordatorio.")]
    [InlineData(nameof(CambiarConfiguracionDeEnvio.HoraDeLaAgendaDelDia), "Escribe la hora de la Agenda del día.")]
    [InlineData(nameof(CambiarConfiguracionDeEnvio.CorreoDeLaDoctora), CambiarConfiguracionDeEnvio.MensajeDeCorreo)]
    [InlineData(nameof(CambiarConfiguracionDeEnvio.CorreoDelAdministrador), CambiarConfiguracionDeEnvio.MensajeDeCorreo)]
    public async Task Una_hora_o_un_correo_invalidos_vuelven_invalido_bajo_su_propiedad_y_nada_cambia(string propiedad, string mensaje)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAsync(escenario, Jueves);
        var filaAntes = Assert.Single(await FilasAsync(escenario));
        var comando = Cambio(horaDelRecordatorio: new TimeOnly(10, 0), correoDeLaDoctora: "dra@clinica.mx");
        switch (propiedad)
        {
            case nameof(CambiarConfiguracionDeEnvio.HoraDelRecordatorio): comando.HoraDelRecordatorio = null; break;
            case nameof(CambiarConfiguracionDeEnvio.HoraDeLaAgendaDelDia): comando.HoraDeLaAgendaDelDia = null; break;
            case nameof(CambiarConfiguracionDeEnvio.CorreoDeLaDoctora): comando.CorreoDeLaDoctora = "dra@"; break;
            default: comando.CorreoDelAdministrador = "admin arroba clinica"; break;
        }

        var resultado = await CambiarAsync(escenario, comando);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Equal([mensaje], Assert.Single(resultado.Errores, e => e.Key == propiedad).Value);
        Assert.Equal(Sembrada, await FilaAsync(escenario));
        Assert.Equal(filaAntes, Assert.Single(await FilasAsync(escenario)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Un_correo_vacio_vuelve_invalido_bajo_su_propiedad(string? correo)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Cambio();
        comando.CorreoDeLaDoctora = correo;
        comando.CorreoDelAdministrador = correo;

        var resultado = await CambiarAsync(escenario, comando);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Equal(["Escribe el correo de la doctora."], resultado.Errores[nameof(CambiarConfiguracionDeEnvio.CorreoDeLaDoctora)]);
        Assert.Equal(["Escribe el correo del Administrador."], resultado.Errores[nameof(CambiarConfiguracionDeEnvio.CorreoDelAdministrador)]);
        Assert.Equal(Sembrada, await FilaAsync(escenario));
    }

    [Fact]
    public async Task Un_correo_de_mas_de_254_caracteres_vuelve_invalido()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var largo = new string('a', 64) + "@" + string.Join('.', Enumerable.Repeat(new string('b', 60), 4)) + ".mx";
        Assert.True(largo.Length > 254);

        var resultado = await CambiarAsync(escenario, Cambio(correoDeLaDoctora: largo));

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Contains("El correo tiene a lo más 254 caracteres.", resultado.Errores[nameof(CambiarConfiguracionDeEnvio.CorreoDeLaDoctora)]);
        Assert.Equal(Sembrada, await FilaAsync(escenario));
    }

    [Fact]
    public void Ni_el_comando_ni_los_valores_muestran_las_direcciones()
    {
        var comando = Cambio(correoDeLaDoctora: "dra@clinica.mx", correoDelAdministrador: "admin@clinica.mx");
        var valores = new ConfiguracionDeEnvioValores(new TimeOnly(9, 0), new TimeOnly(14, 0), "dra@clinica.mx", "admin@clinica.mx");

        foreach (var texto in new[] { comando.ToString(), valores.ToString() })
        {
            Assert.DoesNotContain("clinica", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("@", texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Las_direcciones_no_van_a_los_registros()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await CambiarAsync(escenario, Cambio(correoDeLaDoctora: "dra@clinica.mx", correoDelAdministrador: "admin@clinica.mx"))).EsExito);
        await LeerAsync(escenario);

        Assert.All(
            Arnes.Registros(escenario.Web).Where(r => r.Categoria.StartsWith("DraSelina", StringComparison.Ordinal)),
            r => Assert.DoesNotContain("clinica", r.Mensaje, StringComparison.OrdinalIgnoreCase));
    }

    // Un comando con los valores sembrados, salvo los que se den.
    private static CambiarConfiguracionDeEnvio Cambio(
        TimeOnly? horaDelRecordatorio = null,
        TimeOnly? horaDeLaAgendaDelDia = null,
        string? correoDeLaDoctora = null,
        string? correoDelAdministrador = null) => new()
    {
        HoraDelRecordatorio = horaDelRecordatorio ?? Sembrada.HoraDelRecordatorio,
        HoraDeLaAgendaDelDia = horaDeLaAgendaDelDia ?? Sembrada.HoraDeLaAgendaDelDia,
        CorreoDeLaDoctora = correoDeLaDoctora ?? Sembrada.CorreoDeLaDoctora,
        CorreoDelAdministrador = correoDelAdministrador ?? Sembrada.CorreoDelAdministrador,
    };

    private static async Task<Resultado<ConfiguracionDeEnvioValores>> CambiarAsync(Escenario escenario, CambiarConfiguracionDeEnvio comando)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ConSesion(ambito);
        return await Comando(ambito).HandleAsync(comando, Cancelacion);
    }

    private static async Task<ConfiguracionDeEnvioValores> LeerAsync(Escenario escenario)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ConSesion(ambito);
        var resultado = await Consulta(ambito).HandleAsync(new ConfiguracionDeEnvioVigente(), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma}");
        return resultado.Valor;
    }

    private static ICommandHandler<CambiarConfiguracionDeEnvio, ConfiguracionDeEnvioValores> Comando(AsyncServiceScope ambito) =>
        ambito.ServiceProvider.GetRequiredService<ICommandHandler<CambiarConfiguracionDeEnvio, ConfiguracionDeEnvioValores>>();

    private static IQueryHandler<ConfiguracionDeEnvioVigente, ConfiguracionDeEnvioValores> Consulta(AsyncServiceScope ambito) =>
        ambito.ServiceProvider.GetRequiredService<IQueryHandler<ConfiguracionDeEnvioVigente, ConfiguracionDeEnvioValores>>();

    // Fija un usuario autenticado en el ámbito, como ActorYAdministracionTests: el actor es el Administrador.
    private static void ConSesion(AsyncServiceScope ambito)
    {
        var proveedor = Assert.IsAssignableFrom<IHostEnvironmentAuthenticationStateProvider>(
            ambito.ServiceProvider.GetRequiredService<AuthenticationStateProvider>());
        proveedor.SetAuthenticationState(Task.FromResult(new AuthenticationState(Autenticado)));
    }

    // Agenda a Ana ese día a las 16:00 y entrega su CitaAgendada (la confirmación y el Recordatorio).
    private static async Task<CitaLeida> AgendarAsync(Escenario escenario, DateOnly fecha)
    {
        var resultado = await escenario.AgendarAsync(ReservaEnLineaTests.Comando(fecha, new TimeOnly(16, 0)));
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        return Assert.Single(await escenario.CitasAsync());
    }

    private static async Task<CitaCanceladaPorEnlace> CancelarAsync(Escenario escenario, CitaAgendadaEnLinea cita)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        var token = cita.EnlaceDeGestion[(cita.EnlaceDeGestion.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<ICommandHandler<CancelarCitaPorEnlace, CitaCanceladaPorEnlace>>()
            .HandleAsync(new CancelarCitaPorEnlace { Token = token }, Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    // Escribe el evento en el outbox de Agenda como lo haría Agenda, listo para la siguiente vuelta de entrega.
    private static Task PublicarReprogramadaAsync(Escenario escenario, CitaReprogramada evento)
    {
        var contenido = System.Text.Json.JsonSerializer.Serialize(evento).Replace("'", "''");
        return escenario.EjecutarAsync(
            $"""
            insert into agenda.outbox
                (id, tipo, contenido, ocurrido_en, clave_de_orden, estado, intentos, proximo_intento_en, entregado_en)
            values
                ('{evento.EventoId}', '{nameof(CitaReprogramada)}', '{contenido}'::jsonb, '{evento.OcurridoEn:O}',
                 '{evento.CitaId.Valor}', 'Pendiente', 0, '{escenario.Reloj.UtcNow:O}', null)
            """);
    }

    private static DateTimeOffset Local(DateOnly fecha, TimeOnly hora) => HoraLocal.AUtc(fecha, hora);

    private static void IrA(Escenario escenario, DateOnly fecha, TimeOnly hora) =>
        escenario.Reloj.Avanzar(Local(fecha, hora) - escenario.Reloj.UtcNow);

    private static List<MensajeDeCorreo> Agendas(Escenario escenario) =>
        [.. Arnes.Canal(escenario.Web).Enviados.Where(c => c.Asunto.StartsWith(PlantillaDeAgendaDelDia.Titulo, StringComparison.Ordinal))];

    private static List<MensajeDeCorreo> Recordatorios(Escenario escenario) =>
        [.. Arnes.Canal(escenario.Web).Enviados.Where(c => c.Asunto.StartsWith(PlantillaDeRecordatorio.Titulo, StringComparison.Ordinal))];

    private static async Task<ConfiguracionDeEnvioValores> FilaAsync(Escenario escenario) => Assert.Single(await escenario.FilasAsync(
        "select hora_recordatorio, hora_agenda_del_dia, correo_doctora, correo_administrador from notificaciones.configuracion_de_envio",
        l => new ConfiguracionDeEnvioValores(l.GetFieldValue<TimeOnly>(0), l.GetFieldValue<TimeOnly>(1), l.GetString(2), l.GetString(3))));

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
