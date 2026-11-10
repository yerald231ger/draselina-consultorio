using System.Net;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La alerta de cancelación tardía (CAP-19, FR-24; entrada 4.5): un <see cref="CitaCancelada"/> con motivo
/// <c>tarde</c>, del comando real de la 3.4, llega al correo del Administrador de <c>configuracion_de_envio</c> en la
/// misma vuelta de entrega que el aviso al Paciente. Contra PostgreSQL real, con el reloj de
/// <see cref="ReservaEnLineaTests"/>. La Cita es la de Ana, el martes 3 de noviembre a las 4:00 pm.
/// </summary>
public sealed class AlertaDeCancelacionTardiaTests(Arnes arnes)
{
    private const string Administrador = "administrador@consultorio.local";

    private static readonly DateTimeOffset Inicio = Agenda.Domain.HoraLocal.AUtc(new DateOnly(2026, 11, 3), new TimeOnly(16, 0));

    /// <summary>La Agenda del Administrador en la fecha local de la Cita.</summary>
    private const string UrlDeLaAgenda = $"{Arnes.UrlDelSitio}/admin?fecha=2026-11-03";

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Una_cancelacion_tardia_alerta_al_Administrador_en_la_misma_vuelta_que_el_aviso_al_Paciente()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarYConfirmarAsync(escenario, Comando());
        escenario.Reloj.Avanzar(Inicio.AddHours(-1) - escenario.Reloj.UtcNow);

        Assert.True((await CancelarAsync(escenario, cita)).Tardia);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var alerta = Assert.Single(Alertas(escenario));
        Assert.Equal(Administrador, alerta.Destinatario);
        Assert.Equal("Cancelación tardía: Ana Sofía López, Martes 3 de noviembre, 4:00 pm", alerta.Asunto);
        foreach (var version in new[] { WebUtility.HtmlDecode(alerta.Html), alerta.Texto })
        {
            Assert.Contains("Hola:", version, StringComparison.Ordinal);
            Assert.Contains("Ana Sofía López canceló su cita tarde.", version, StringComparison.Ordinal);
            Assert.Contains("Martes 3 de noviembre", version, StringComparison.Ordinal);
            Assert.Contains("4:00 pm", version, StringComparison.Ordinal);
        }

        // El botón lleva a la Agenda en la fecha local de la Cita, sin datos personales en la dirección (AD-11, AD-23).
        Assert.Contains($"{PlantillaDeAlertaTardia.AbrirLaAgenda}: {UrlDeLaAgenda}", alerta.Texto, StringComparison.Ordinal);
        Assert.Contains($"href=\"{UrlDeLaAgenda}\"", alerta.Html, StringComparison.Ordinal);
        Assert.Contains($">{PlantillaDeAlertaTardia.AbrirLaAgenda}</a>", alerta.Html, StringComparison.Ordinal);

        Assert.Contains(Arnes.Canal(escenario.Web).Enviados, c => c.Destinatario == "ana@correo.com" && c.Asunto == PlantillaDeCancelacion.Titulo);
        Assert.Equal(1, await AlertasRegistradasAsync(escenario));
    }

    [Fact]
    public async Task Una_cancelacion_a_tiempo_o_por_el_Consultorio_no_alerta()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarYConfirmarAsync(escenario, Comando());

        Assert.False((await CancelarAsync(escenario, cita)).Tardia);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var otra = await AgendarYConfirmarAsync(escenario, Comando(new DateOnly(2026, 11, 5), new TimeOnly(16, 0)));
        await escenario.EjecutarAsync(
            $"""
            insert into agenda.outbox
                (id, tipo, contenido, ocurrido_en, clave_de_orden, estado, intentos, proximo_intento_en, entregado_en)
            values
                ('{Guid.CreateVersion7()}', 'CitaCancelada',
                 '{System.Text.Json.JsonSerializer.Serialize(new CitaCancelada(Guid.CreateVersion7(), escenario.Reloj.UtcNow, otra.CitaId, new Kernel.PatientId((await escenario.CitasAsync()).First().PacienteId), Kernel.Actor.Administrador, Inicio.AddDays(2), CitaCancelada.MotivoConsultorio))}'::jsonb,
                 '{escenario.Reloj.UtcNow:O}', '{otra.CitaId.Valor}', 'Pendiente', 0, '{escenario.Reloj.UtcNow:O}', null)
            """);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        // El evento del Consultorio sí corrió por los handlers: salió la disculpa y quedó Entregado.
        Assert.Contains(Arnes.Canal(escenario.Web).Enviados, c => c.Asunto == PlantillaDeCancelacion.TituloPorElConsultorio);
        Assert.Equal(0, await escenario.EscalarAsync<long>(
            $"select count(*) from agenda.outbox where tipo = 'CitaCancelada' and clave_de_orden = '{otra.CitaId.Valor}' and estado <> 'Entregado'"));

        Assert.Empty(Alertas(escenario));
        Assert.Equal(0, await AlertasRegistradasAsync(escenario));
    }

    [Fact]
    public async Task El_mismo_evento_entregado_dos_veces_deja_una_sola_alerta()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarYConfirmarAsync(escenario, Comando());
        escenario.Reloj.Avanzar(Inicio.AddHours(-1) - escenario.Reloj.UtcNow);
        await CancelarAsync(escenario, cita);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        await escenario.EjecutarAsync(
            $"update agenda.outbox set estado = 'Pendiente', entregado_en = null where tipo = 'CitaCancelada' and clave_de_orden = '{cita.CitaId.Valor}'");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Single(Alertas(escenario));
        Assert.Equal(1, await AlertasRegistradasAsync(escenario));
    }

    [Fact]
    public async Task Sin_correo_del_Paciente_la_alerta_sale_igual()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var comando = Comando();
        comando.Correo = null;
        var cita = await AgendarYConfirmarAsync(escenario, comando);
        escenario.Reloj.Avanzar(Inicio.AddHours(-1) - escenario.Reloj.UtcNow);

        await CancelarAsync(escenario, cita);
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Equal(Administrador, Assert.Single(Alertas(escenario)).Destinatario);
    }

    [Fact]
    public async Task Con_el_canal_caido_no_queda_fila_y_la_alerta_sale_en_el_reintento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var cita = await AgendarYConfirmarAsync(escenario, Comando());
        escenario.Reloj.Avanzar(Inicio.AddHours(-1) - escenario.Reloj.UtcNow);
        await CancelarAsync(escenario, cita);

        Arnes.Canal(escenario.Web).Caido = true;
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        Assert.Equal(0, await AlertasRegistradasAsync(escenario));

        Arnes.Canal(escenario.Web).Caido = false;
        escenario.Reloj.Avanzar(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        Assert.Single(Alertas(escenario));
        Assert.Equal(1, await AlertasRegistradasAsync(escenario));
    }

    [Fact]
    public void Los_datos_de_la_alerta_no_muestran_el_nombre_ni_la_direccion()
    {
        var datos = new PlantillaDeAlertaTardia.Datos(
            Administrador, "Ana Sofía López", new DateOnly(2026, 11, 3), new TimeOnly(16, 0), UrlDeLaAgenda);

        Assert.DoesNotContain("Ana", datos.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Administrador, datos.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Ana", PlantillaDeAlertaTardia.Armar(datos).ToString(), StringComparison.Ordinal);
    }

    // Agenda la Cita y entrega su CitaAgendada: la confirmación (si hay correo) ya salió.
    private static async Task<CitaAgendadaEnLinea> AgendarYConfirmarAsync(Escenario escenario, AgendarCitaEnLinea comando)
    {
        var resultado = await escenario.AgendarAsync(comando);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));
        return resultado.Valor;
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

    private static List<Notificaciones.Features.MensajeDeCorreo> Alertas(Escenario escenario) =>
        [.. Arnes.Canal(escenario.Web).Enviados.Where(c => c.Asunto.StartsWith(PlantillaDeAlertaTardia.Titulo, StringComparison.Ordinal))];

    private static Task<long> AlertasRegistradasAsync(Escenario escenario) =>
        escenario.EscalarAsync<long>("select count(*) from notificaciones.mensaje where tipo = 'AlertaTardia' and estado = 'Enviado'");
}
