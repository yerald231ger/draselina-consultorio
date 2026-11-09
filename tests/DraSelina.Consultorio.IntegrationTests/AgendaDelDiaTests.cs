using System.Net;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Features;
using DraSelina.Consultorio.Pacientes.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// "Tus citas de hoy" (CAP-20, FR-25; entrada 4.6): la vuelta de <see cref="EnviosProgramados"/> le envía a la doctora,
/// una sola vez por fecha y desde la hora de la Agenda del día (la sembrada, 14:00 local), las Citas Agendadas de hoy
/// que le da Agenda. Contra PostgreSQL real, con el reloj de <see cref="ReservaEnLineaTests"/> (lunes 2 a las 12:00
/// local) y la agenda base sembrada (lunes a viernes, 15:30 a 18:00).
/// </summary>
public sealed class AgendaDelDiaTests(Arnes arnes)
{
    private const string Doctora = "doctora@consultorio.local";

    private static readonly DateOnly Martes = new(2026, 11, 3);

    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Un_dia_con_Citas_envia_una_linea_por_Cita_en_orden_y_la_del_Consultorio_sin_motivo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAnaAsync(escenario, Martes, new TimeOnly(16, 0));
        var luis = await PacienteAsync(escenario, "Luis Hernández Ruiz", "4449876543");
        await escenario.InsertarCitaDelConsultorioAsync(Martes, new TimeOnly(17, 0), new TimeOnly(17, 30), luis);

        IrA(escenario, Martes, new TimeOnly(14, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        var correo = Assert.Single(Agendas(escenario));
        Assert.Equal(Doctora, correo.Destinatario);
        Assert.Equal("Tus citas de hoy: Martes 3 de noviembre", correo.Asunto);
        Assert.Empty(correo.Adjuntos);

        var html = WebUtility.HtmlDecode(correo.Html);
        foreach (var version in new[] { html, correo.Texto })
        {
            Assert.Contains("Hola, doctora:", version, StringComparison.Ordinal);
            Assert.Contains("Tus citas de hoy", version, StringComparison.Ordinal);
            var ana = version.IndexOf("4:00 pm · Ana Sofía López · Dolor de rodilla derecha", StringComparison.Ordinal);
            var conLuis = version.IndexOf("5:00 pm · Luis Hernández Ruiz", StringComparison.Ordinal);
            Assert.True(ana >= 0 && conLuis > ana, version);
            Assert.DoesNotContain("Luis Hernández Ruiz ·", version, StringComparison.Ordinal);
            Assert.DoesNotContain("No hay citas hoy", version, StringComparison.Ordinal);
        }

        // Sin botones ni imágenes.
        Assert.DoesNotContain("<a ", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);

        var mensaje = Assert.Single(await MensajesAsync(escenario));
        Assert.Equal(("Enviado", (DateOnly?)Martes, (Guid?)null, (Guid?)null), (mensaje.Estado, mensaje.Fecha, mensaje.EventoId, mensaje.CitaId));
        Assert.Equal("correo", mensaje.Canal);
        Assert.Equal(HoraLocal.AUtc(Martes, new TimeOnly(14, 0)), mensaje.RegistradoEn);
    }

    [Fact]
    public async Task Antes_de_la_hora_no_sale_nada_y_otra_vuelta_el_mismo_dia_no_envia_otro()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAnaAsync(escenario, Martes, new TimeOnly(16, 0));

        IrA(escenario, Martes, new TimeOnly(13, 59));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        Assert.Empty(Agendas(escenario));
        Assert.Empty(await MensajesAsync(escenario));

        IrA(escenario, Martes, new TimeOnly(14, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        Assert.Single(Agendas(escenario));

        IrA(escenario, Martes, new TimeOnly(14, 5));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        Assert.Single(Agendas(escenario));
        Assert.Single(await MensajesAsync(escenario));
    }

    [Fact]
    public async Task Un_dia_con_Tramos_sin_Citas_dice_que_no_hay_citas()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        IrA(escenario, Martes, new TimeOnly(14, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        var correo = Assert.Single(Agendas(escenario));
        Assert.Equal(Doctora, correo.Destinatario);
        foreach (var version in new[] { WebUtility.HtmlDecode(correo.Html), correo.Texto })
        {
            Assert.Contains("No hay citas hoy", version, StringComparison.Ordinal);
            Assert.DoesNotContain(" · ", version, StringComparison.Ordinal);
        }

        Assert.Equal("Enviado", Assert.Single(await MensajesAsync(escenario)).Estado);
    }

    [Fact]
    public async Task Un_dia_sin_Tramos_ni_Citas_no_envia_nada_y_queda_Omitido_una_vez()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        IrA(escenario, Sabado, new TimeOnly(14, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        IrA(escenario, Sabado, new TimeOnly(14, 30));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        Assert.Empty(Arnes.Canal(escenario.Web).Enviados);
        var mensaje = Assert.Single(await MensajesAsync(escenario));
        Assert.Equal(("Omitido", (DateOnly?)Sabado), (mensaje.Estado, mensaje.Fecha));
    }

    [Fact]
    public async Task Un_sabado_sin_Tramos_con_una_Cita_del_Consultorio_la_envia()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteDeAnaAsync();
        await escenario.InsertarCitaDelConsultorioAsync(Sabado, new TimeOnly(10, 0), new TimeOnly(10, 30), ana);

        IrA(escenario, Sabado, new TimeOnly(14, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        var correo = Assert.Single(Agendas(escenario));
        Assert.Equal("Tus citas de hoy: Sábado 7 de noviembre", correo.Asunto);
        Assert.Contains("10:00 am · Ana Sofía López", correo.Texto, StringComparison.Ordinal);
        Assert.Equal("Enviado", Assert.Single(await MensajesAsync(escenario)).Estado);
    }

    [Fact]
    public async Task Una_Cita_cancelada_no_aparece()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var ana = await escenario.PacienteDeAnaAsync();
        var luis = await PacienteAsync(escenario, "Luis Hernández Ruiz", "4449876543");
        await escenario.InsertarCitaDelConsultorioAsync(Martes, new TimeOnly(16, 0), new TimeOnly(16, 30), luis);
        await escenario.InsertarCitaDelConsultorioAsync(Martes, new TimeOnly(17, 0), new TimeOnly(17, 30), ana);
        await escenario.EjecutarAsync(
            $"update agenda.cita set estado = 'Cancelada', motivo_de_cancelacion = 'paciente', cancelada_en = now() where paciente_id = '{luis}'");

        IrA(escenario, Martes, new TimeOnly(14, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        var correo = Assert.Single(Agendas(escenario));
        Assert.Contains("5:00 pm · Ana Sofía López", correo.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Luis", correo.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("4:00 pm", correo.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Con_el_canal_caido_no_queda_fila_y_sale_en_la_siguiente_vuelta()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAnaAsync(escenario, Martes, new TimeOnly(16, 0));
        var canal = Arnes.Canal(escenario.Web);

        canal.Caido = true;
        IrA(escenario, Martes, new TimeOnly(14, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        Assert.Empty(Agendas(escenario));
        Assert.Empty(await MensajesAsync(escenario));
        Assert.Contains(Arnes.Registros(escenario.Web), r => r.Nivel == LogLevel.Warning && r.Categoria == typeof(EnviosProgramados).FullName);

        canal.Caido = false;
        IrA(escenario, Martes, new TimeOnly(14, 1));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        Assert.Equal(Doctora, Assert.Single(Agendas(escenario)).Destinatario);
        Assert.Equal("Enviado", Assert.Single(await MensajesAsync(escenario)).Estado);
    }

    [Fact]
    public async Task No_recupera_dias_anteriores()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // La web estuvo apagada todo el martes: el miércoles a las 14:00 sale el del miércoles, no el del martes.
        IrA(escenario, Martes.AddDays(1), new TimeOnly(14, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        Assert.Equal("Tus citas de hoy: Miércoles 4 de noviembre", Assert.Single(Agendas(escenario)).Asunto);
        Assert.Equal(Martes.AddDays(1), Assert.Single(await MensajesAsync(escenario)).Fecha);
    }

    [Fact]
    public async Task Un_Recordatorio_vencido_y_la_Agenda_del_dia_salen_en_la_misma_vuelta()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // La Cita del miércoles deja su Recordatorio para el martes a las 9:00.
        await AgendarAnaAsync(escenario, Martes.AddDays(1), new TimeOnly(16, 0));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        IrA(escenario, Martes, new TimeOnly(14, 0));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));

        Assert.Single(Arnes.Canal(escenario.Web).Enviados, c => c.Asunto.StartsWith(PlantillaDeRecordatorio.Titulo, StringComparison.Ordinal));
        Assert.Contains("No hay citas hoy", Assert.Single(Agendas(escenario)).Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Si_la_Agenda_del_dia_falla_el_Recordatorio_sale_igual()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAnaAsync(escenario, Martes.AddDays(1), new TimeOnly(16, 0));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        // Una Cita de hoy con un Paciente que Pacientes no conoce: la Agenda del día no puede armarse.
        await escenario.InsertarCitaDelConsultorioAsync(Martes, new TimeOnly(16, 0), new TimeOnly(16, 30));

        IrA(escenario, Martes, new TimeOnly(14, 0));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion));

        Assert.Single(Arnes.Canal(escenario.Web).Enviados, c => c.Asunto.StartsWith(PlantillaDeRecordatorio.Titulo, StringComparison.Ordinal));
        Assert.Empty(Agendas(escenario));
        Assert.Empty(await MensajesAsync(escenario));
        Assert.Contains(Arnes.Registros(escenario.Web), r => r.Nivel == LogLevel.Warning && r.Categoria == typeof(EnviosProgramados).FullName);
    }

    [Fact]
    public async Task Ni_nombres_ni_motivos_ni_direcciones_van_a_los_registros_ni_a_las_filas()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await AgendarAnaAsync(escenario, Martes, new TimeOnly(16, 0));
        var canal = Arnes.Canal(escenario.Web);

        // Una vuelta que falla y otra que sale: las dos dejan registros.
        canal.Caido = true;
        IrA(escenario, Martes, new TimeOnly(14, 0));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        canal.Caido = false;
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        Assert.Single(Agendas(escenario));

        var mensajes = await escenario.EscalarAsync<string>(
            "select json_agg(m)::text from notificaciones.mensaje m where tipo = 'AgendaDelDia'");
        var registros = Arnes.Registros(escenario.Web)
            .Where(r => r.Categoria.StartsWith("DraSelina", StringComparison.Ordinal))
            .Select(r => r.Mensaje)
            .ToList();
        Assert.NotEmpty(registros);
        foreach (var dato in new[] { Doctora, "consultorio.local", "Ana Sofía", "López", "rodilla", "Tus citas" })
        {
            Assert.DoesNotContain(dato, mensajes, StringComparison.OrdinalIgnoreCase);
            Assert.All(registros, r => Assert.DoesNotContain(dato, r, StringComparison.OrdinalIgnoreCase));
        }

        // Ni lo que viaja entre módulos ni lo que arma la plantilla se muestran al registrarlos.
        var cita = new CitaDelDia(new TimeOnly(16, 0), new PatientId(Guid.CreateVersion7()), "Dolor de rodilla derecha");
        var datos = new PlantillaDeAgendaDelDia.Datos(
            Doctora, Martes, [new PlantillaDeAgendaDelDia.Cita(cita.Hora, "Ana Sofía López", cita.MotivoDeConsulta)]);
        foreach (var texto in new[]
        {
            cita.ToString(), new AgendaDelDia(true, [cita]).ToString(), datos.ToString(), datos.Citas[0].ToString(),
            PlantillaDeAgendaDelDia.Armar(datos).ToString(),
        })
        {
            Assert.DoesNotContain("rodilla", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("Ana", texto, StringComparison.Ordinal);
            Assert.DoesNotContain(Doctora, texto, StringComparison.Ordinal);
        }
    }

    // Agenda a Ana en línea (con motivo "Dolor de rodilla derecha"), sin entregar su evento.
    private static async Task AgendarAnaAsync(Escenario escenario, DateOnly fecha, TimeOnly hora)
    {
        var resultado = await escenario.AgendarAsync(Comando(fecha, hora));
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
    }

    private static async Task<Guid> PacienteAsync(Escenario escenario, string nombre, string telefono)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var resultado = await ambito.ServiceProvider.GetRequiredService<IPacientesApi>()
            .BuscarOCrearAsync(nombre, telefono, null, Cancelacion);
        Assert.True(resultado.EsExito);
        return resultado.Valor.Valor;
    }

    private static void IrA(Escenario escenario, DateOnly fecha, TimeOnly hora) =>
        escenario.Reloj.Avanzar(HoraLocal.AUtc(fecha, hora) - escenario.Reloj.UtcNow);

    private static List<MensajeDeCorreo> Agendas(Escenario escenario) =>
        [.. Arnes.Canal(escenario.Web).Enviados.Where(c => c.Asunto.StartsWith(PlantillaDeAgendaDelDia.Titulo, StringComparison.Ordinal))];

    private static Task<List<MensajeLeido>> MensajesAsync(Escenario escenario) => escenario.FilasAsync(
        "select evento_id, cita_id, canal, estado, registrado_en, fecha from notificaciones.mensaje where tipo = 'AgendaDelDia' order by id",
        l => new MensajeLeido(
            l.IsDBNull(0) ? null : l.GetGuid(0), l.IsDBNull(1) ? null : l.GetGuid(1),
            l.GetString(2), l.GetString(3), l.GetFieldValue<DateTimeOffset>(4), l.IsDBNull(5) ? null : l.GetFieldValue<DateOnly>(5)));

    private sealed record MensajeLeido(Guid? EventoId, Guid? CitaId, string Canal, string Estado, DateTimeOffset RegistradoEn, DateOnly? Fecha);
}
