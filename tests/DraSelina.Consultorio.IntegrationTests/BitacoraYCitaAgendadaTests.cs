using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Cada reserva en línea deja su entrada en <c>agenda.bitacora</c> (RN-24) y publica <see cref="CitaAgendada"/> en
/// <c>agenda.outbox</c> (AD-3), en la misma transacción que la Cita (T-30): las tres cosas quedan juntas o ninguna.
/// Contra PostgreSQL real, con la base migrada de cada prueba y el reloj de <see cref="ReservaEnLineaTests"/>.
/// </summary>
public sealed class BitacoraYCitaAgendadaTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Una_reserva_valida_deja_su_entrada_en_la_bitacora_con_actor_Paciente()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await escenario.AgendarAsync(Comando());

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        var cita = Assert.Single(await escenario.CitasAsync());
        var entrada = Assert.Single(await BitacoraAsync(escenario));
        Assert.Equal(7, entrada.Id.Version);
        Assert.Equal(Ahora, entrada.OcurridoEn);
        Assert.Equal("Paciente", entrada.ActorTipo);
        Assert.Null(entrada.ActorId);
        Assert.Equal("Cita", entrada.Entidad);
        Assert.Equal(cita.Id.ToString(), entrada.EntidadId);
        Assert.Equal("Agendada", entrada.Accion);

        using var detalle = JsonDocument.Parse(entrada.Detalle!);
        var raiz = detalle.RootElement;
        Assert.Equal(cita.Inicio, raiz.GetProperty("inicio").GetDateTimeOffset());
        Assert.Equal(cita.Fin, raiz.GetProperty("fin").GetDateTimeOffset());
        Assert.Equal(TiposDeVisitaSembrados.PrimeraConsulta.Valor, raiz.GetProperty("tipoDeVisitaId").GetGuid());
        Assert.Equal("en_linea", raiz.GetProperty("origen").GetString());
    }

    [Fact]
    public async Task Una_reserva_valida_deja_CitaAgendada_pendiente_en_el_outbox_con_los_datos_de_la_Cita()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);

        var cita = Assert.Single(await escenario.CitasAsync());
        var fila = Assert.Single(await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, cita.Id, Cancelacion));
        Assert.Equal(nameof(CitaAgendada), fila.Tipo);
        Assert.Equal("Pendiente", fila.Estado);
        Assert.Equal(0, fila.Intentos);
        Assert.Equal(Ahora, fila.OcurridoEn);
        Assert.Equal(Ahora, fila.ProximoIntentoEn);
        Assert.Null(fila.EntregadoEn);
        Assert.Equal(7, fila.Id.Version);

        // El actor va por su nombre, no por su número.
        using (var contenido = JsonDocument.Parse(fila.Contenido))
        {
            Assert.Equal("Paciente", contenido.RootElement.GetProperty(nameof(CitaAgendada.Actor)).GetString());
        }

        var evento = JsonSerializer.Deserialize<CitaAgendada>(fila.Contenido)!;
        Assert.Equal(
            new CitaAgendada(fila.Id, Ahora, new CitaId(cita.Id), new PatientId(cita.PacienteId), Actor.Paciente, cita.Inicio, cita.Fin, "en_linea"),
            evento);
    }

    [Fact]
    public async Task Una_Cita_traslapada_no_deja_bitacora_ni_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await escenario.InsertarCitaDelConsultorioAsync(Manana, new TimeOnly(15, 45), new TimeOnly(16, 15));

        var resultado = await escenario.AgendarAsync(Comando());

        Assert.Equal(CodigosDeAgenda.HorarioOcupado, resultado.Codigo);
        Assert.Empty(await BitacoraAsync(escenario));
        Assert.Equal(0, await OutboxAsync(escenario));
    }

    [Fact]
    public async Task De_dos_reservas_simultaneas_del_mismo_horario_solo_la_que_queda_deja_bitacora_y_evento()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var otra = Comando();
        otra.Nombre = "Mariana López";

        await Task.WhenAll(escenario.AgendarAsync(Comando()), escenario.AgendarAsync(otra));

        var cita = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(cita.Id.ToString(), Assert.Single(await BitacoraAsync(escenario)).EntidadId);
        Assert.Single(await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, cita.Id, Cancelacion));
        Assert.Equal(1, await OutboxAsync(escenario));
    }

    [Theory]
    [InlineData("2026-11-03", "15:45", CodigosDeAgenda.FueraDeHorario)]
    [InlineData("2026-11-02", "16:00", CodigosDeAgenda.FueraDeVentana)]
    [InlineData("2026-11-02", "11:00", CodigosDeAgenda.EnElPasado)]
    public async Task Un_rechazo_del_dominio_no_deja_bitacora_ni_evento(string fecha, string hora, string codigo)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await escenario.AgendarAsync(Comando(DateOnly.Parse(fecha), TimeOnly.Parse(hora)));

        Assert.Equal(codigo, resultado.Codigo);
        Assert.Empty(await BitacoraAsync(escenario));
        Assert.Equal(0, await OutboxAsync(escenario));
    }

    [Fact]
    public async Task La_bitacora_y_el_evento_no_llevan_nombre_telefono_correo_ni_motivo()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);

        var detalle = Assert.Single(await BitacoraAsync(escenario)).Detalle!;
        var contenido = await escenario.EscalarAsync<string>("select contenido::text from agenda.outbox");
        foreach (var texto in new[] { detalle, contenido })
        {
            foreach (var dato in new[] { "Ana", "López", "Lopez", "4441234567", "444 123 4567", "ana@correo.com", "rodilla" })
            {
                Assert.DoesNotContain(dato, texto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task El_servicio_de_entrega_entrega_CitaAgendada_aunque_aun_no_tenga_handler()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        Assert.True((await escenario.AgendarAsync(Comando())).EsExito);
        var cita = Assert.Single(await escenario.CitasAsync());

        Assert.Equal(1, await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion));

        var fila = Assert.Single(await Consultas.EventosDeLaClaveAsync(escenario.Cadena, AgendaDbContext.Esquema, cita.Id, Cancelacion));
        Assert.Equal("Entregado", fila.Estado);
        Assert.Contains(Arnes.Registros(escenario.Web), r =>
            r.Nivel == LogLevel.Warning && r.Mensaje.Contains(fila.Id.ToString()) && r.Mensaje.Contains(nameof(CitaAgendada)));
    }

    [Fact]
    public async Task Anotar_en_la_bitacora_sin_transaccion_abierta_lanza()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var bitacora = ambito.ServiceProvider.GetRequiredService<IBitacoraDeAgenda>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => bitacora.AnotarAsync(
            new EntradaDeBitacora(
                Ahora, Actor.Paciente, null, EntidadDeBitacora.Cita, Guid.CreateVersion7().ToString(),
                AccionDeBitacora.Agendada, null),
            Cancelacion));

        Assert.Empty(await BitacoraAsync(escenario));
    }

    private static Task<List<EntradaLeida>> BitacoraAsync(Escenario escenario) => escenario.FilasAsync(
        "select id, ocurrido_en, actor_tipo, actor_id, entidad, entidad_id, accion, detalle::text from agenda.bitacora order by id",
        l => new EntradaLeida(
            l.GetGuid(0), l.GetFieldValue<DateTimeOffset>(1), l.GetString(2), l.IsDBNull(3) ? null : l.GetString(3),
            l.GetString(4), l.GetString(5), l.GetString(6), l.IsDBNull(7) ? null : l.GetString(7)));

    private static Task<long> OutboxAsync(Escenario escenario) => escenario.EscalarAsync<long>("select count(*) from agenda.outbox");

    private sealed record EntradaLeida(
        Guid Id,
        DateTimeOffset OcurridoEn,
        string ActorTipo,
        string? ActorId,
        string Entidad,
        string EntidadId,
        string Accion,
        string? Detalle);
}
