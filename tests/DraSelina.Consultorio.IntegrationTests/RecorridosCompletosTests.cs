using System.Security.Claims;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Features;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Los dos recorridos completos del Done when 3 de la iniciativa, de principio a fin y contra PostgreSQL real, con los
/// comandos reales, el reloj de prueba y las vueltas de entrega y de envíos programados a mano. El reloj arranca el
/// lunes 2 de noviembre de 2026 a las 12:00 local (<see cref="ReservaEnLineaTests.Ahora"/>).
/// <list type="bullet">
/// <item>El del Paciente (UJ-1 y UJ-2): reservar, recibir la confirmación y el Recordatorio, reprogramar, recibir el
/// aviso y el Recordatorio nuevo, y cancelar tarde, con la alerta al Administrador.</item>
/// <item>El de una Agenda custom (UJ-3 y UJ-7): abrir un sábado, recibir dos reservas en línea y cerrarlo cancelando una
/// Cita y conservando la otra (P-1).</item>
/// </list>
/// </summary>
public sealed class RecorridosCompletosTests(Arnes arnes)
{
    private const string Ana = "ana@correo.com";
    private const string Luis = "luis@correo.com";
    private const string Administrador = "administrador@consultorio.local";
    private const string IdDelAdministrador = "cuenta-del-administrador";

    private static readonly DateOnly Miercoles = new(2026, 11, 4);
    private static readonly DateOnly Viernes = new(2026, 11, 6);
    private static readonly DateOnly Sabado = new(2026, 11, 7);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Un_Paciente_reserva_recibe_confirmacion_y_Recordatorio_reprograma_y_cancela_tarde()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // Lunes 12:00: Ana reserva el miércoles a las 4:00 pm y le llega la confirmación.
        var reserva = await escenario.AgendarAsync(Comando(Miercoles, new TimeOnly(16, 0)));
        Assert.True(reserva.EsExito, $"{reserva.Forma} {reserva.Codigo}");
        var cita = reserva.Valor;
        await EntregarTodoAsync(escenario);

        // Martes 9:00: el Recordatorio del día anterior.
        IrA(escenario, HoraLocal.AUtc(new DateOnly(2026, 11, 3), new TimeOnly(9, 0)));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        // Martes 9:30: reprograma al viernes a las 5:00 pm con su Enlace de gestión; le llega el aviso.
        escenario.Reloj.Avanzar(TimeSpan.FromMinutes(30));
        var reprogramada = await ComoPacienteAsync<ReprogramarCitaPorEnlace, CitaReprogramadaPorEnlace>(
            escenario, new ReprogramarCitaPorEnlace { Token = Token(cita), Fecha = Viernes, Hora = new TimeOnly(17, 0) });
        Assert.True(reprogramada.EsExito, $"{reprogramada.Forma} {reprogramada.Codigo}");
        await EntregarTodoAsync(escenario);

        // Miércoles 9:00, la hora de la Cita original: ningún Recordatorio. Jueves 9:00: el de la Cita nueva.
        IrA(escenario, HoraLocal.AUtc(Miercoles, new TimeOnly(9, 0)));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);
        Assert.Equal(3, CorreosA(escenario, Ana).Count);
        IrA(escenario, HoraLocal.AUtc(new DateOnly(2026, 11, 5), new TimeOnly(9, 0)));
        await Arnes.DarUnaVueltaDeEnviosProgramadosAsync(escenario.Web, Cancelacion);

        // Viernes 4:00 pm, una hora antes: cancela tarde; le llega la cancelación y al Administrador la alerta.
        IrA(escenario, HoraLocal.AUtc(Viernes, new TimeOnly(16, 0)));
        var cancelada = await ComoPacienteAsync<CancelarCitaPorEnlace, CitaCanceladaPorEnlace>(
            escenario, new CancelarCitaPorEnlace { Token = Token(cita) });
        Assert.True(cancelada.EsExito, $"{cancelada.Forma} {cancelada.Codigo}");
        Assert.True(cancelada.Valor.Tardia);
        await EntregarTodoAsync(escenario);

        // Ana recibió los cinco correos, en orden.
        var correos = CorreosA(escenario, Ana);
        Assert.Equal(
            [
                "Tu cita está confirmada: Miércoles 4 de noviembre, 4:00 pm",
                "Mañana tienes cita: 4:00 pm",
                "Tu cita se reprogramó: Viernes 6 de noviembre, 5:00 pm",
                "Mañana tienes cita: 5:00 pm",
                PlantillaDeCancelacion.Titulo,
            ],
            correos.Select(c => c.Asunto));
        Assert.Contains(PlantillaDeCancelacion.NotaTardia, correos[^1].Texto, StringComparison.Ordinal);

        var alerta = Assert.Single(CorreosA(escenario, Administrador), c => c.Asunto.StartsWith(PlantillaDeAlertaTardia.Titulo, StringComparison.Ordinal));
        Assert.Equal("Cancelación tardía: Ana Sofía López, Viernes 6 de noviembre, 5:00 pm", alerta.Asunto);

        // Una sola Cita, la misma de principio a fin, cancelada tarde a la hora nueva.
        var final = Assert.Single(await escenario.CitasAsync());
        Assert.Equal(cita.CitaId.Valor, final.Id);
        Assert.Equal("Cancelada", final.Estado);
        Assert.Equal(HoraLocal.AUtc(Viernes, new TimeOnly(17, 0)), final.Inicio);
        Assert.Equal(CitaCancelada.MotivoTarde, await escenario.EscalarAsync<string>("select motivo_de_cancelacion from agenda.cita"));

        // Ningún evento quedó sin entregar ni Recordatorio pendiente.
        Assert.Equal(0L, await escenario.EscalarAsync<long>(
            $"select count(*) from {AgendaDbContext.Esquema}.outbox where estado <> 'Entregado'"));
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from notificaciones.recordatorio_programado"));
    }

    [Fact]
    public async Task Una_Agenda_custom_abre_un_sabado_y_al_cerrarlo_cancela_una_Cita_y_conserva_otra()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        // El Administrador abre el sábado de 10:00 a 1:00; la reserva en línea lo ofrece.
        var abrir = await ComoAdministradorAsync<GuardarAgendaCustom, AgendaCustomGuardada>(escenario, new GuardarAgendaCustom
        {
            Fecha = Sabado,
            Motivo = "sábado abierto",
            Tramos = [new TramoAGuardar { Inicio = new TimeOnly(10, 0), Fin = new TimeOnly(13, 0) }],
        });
        Assert.True(abrir.EsExito, $"{abrir.Forma} {abrir.Codigo}");
        Assert.Contains(new TimeOnly(12, 30), Assert.Single(await HorariosLibresAsync(escenario), d => d.Fecha == Sabado).Horas);

        // Ana reserva a las 10:00 y Luis a las 12:30; a los dos les llega su confirmación.
        var deAna = await escenario.AgendarAsync(Comando(Sabado, new TimeOnly(10, 0)));
        Assert.True(deAna.EsExito, $"{deAna.Forma} {deAna.Codigo}");
        var comandoDeLuis = Comando(Sabado, new TimeOnly(12, 30));
        comandoDeLuis.Nombre = "Luis Hernández";
        comandoDeLuis.Telefono = "444 765 4321";
        comandoDeLuis.Correo = Luis;
        var deLuis = await escenario.AgendarAsync(comandoDeLuis);
        Assert.True(deLuis.EsExito, $"{deLuis.Forma} {deLuis.Codigo}");
        await EntregarTodoAsync(escenario);

        // Al cerrar el sábado, el Administrador ve las dos Citas que quedan fuera y elige Cita por Cita.
        var fuera = await ComoAdministradorConsultaAsync<CitasFueraDelHorario, HorarioConCitasFuera>(
            escenario, CitasFueraDelHorario.AlGuardar(Sabado, []));
        Assert.True(fuera.EsExito, $"{fuera.Forma} {fuera.Codigo}");
        Assert.Equal([deAna.Valor.CitaId, deLuis.Valor.CitaId], fuera.Valor.Citas.Select(c => c.CitaId));

        var cerrar = await ComoAdministradorAsync<GuardarAgendaCustom, AgendaCustomGuardada>(escenario, new GuardarAgendaCustom
        {
            Fecha = Sabado,
            Motivo = "asunto personal",
            Citas =
            [
                new DecisionSobreCita { CitaId = deAna.Valor.CitaId, Cancelar = true },
                new DecisionSobreCita { CitaId = deLuis.Valor.CitaId, Cancelar = false },
            ],
        });
        Assert.True(cerrar.EsExito, $"{cerrar.Forma} {cerrar.Codigo}");

        // RN-10: guardar no toca ninguna Cita; P-1 cancela la elegida al entregarse el evento.
        Assert.All(await escenario.CitasAsync(), c => Assert.Equal("Agendada", c.Estado));
        await EntregarTodoAsync(escenario);

        // Ana recibe la disculpa; Luis, solo su confirmación.
        Assert.Equal(["Tu cita está confirmada: Sábado 7 de noviembre, 10:00 am", PlantillaDeCancelacion.TituloPorElConsultorio],
            CorreosA(escenario, Ana).Select(c => c.Asunto));
        Assert.Equal(["Tu cita está confirmada: Sábado 7 de noviembre, 12:30 pm"], CorreosA(escenario, Luis).Select(c => c.Asunto));

        // La Agenda del sábado: sin atención, la de Ana cancelada por el Consultorio, la de Luis conservada y marcada fuera.
        var dia = await ComoAdministradorConsultaAsync<DiasDeLaAgenda, IReadOnlyList<DiaDeLaAgenda>>(escenario, DiasDeLaAgenda.DelDia(Sabado));
        Assert.True(dia.EsExito, $"{dia.Forma} {dia.Codigo}");
        var sabado = Assert.Single(dia.Valor);
        Assert.Equal(TipoDeDia.CustomSinAtencion, sabado.Tipo);
        var citaDeAna = Assert.Single(sabado.Citas, c => c.CitaId == deAna.Valor.CitaId);
        Assert.Equal((EstadoEnLaAgenda.Cancelada, CancelacionEnLaAgenda.PorElConsultorio), (citaDeAna.Estado, citaDeAna.Cancelacion));
        var citaDeLuis = Assert.Single(sabado.Citas, c => c.CitaId == deLuis.Valor.CitaId);
        Assert.Equal(EstadoEnLaAgenda.Agendada, citaDeLuis.Estado);
        Assert.True(citaDeLuis.FueraDeHorario);

        // La cancelación quedó en la bitácora a nombre del Administrador; la reserva en línea ya no ofrece el sábado.
        var bitacora = await escenario.FilasAsync(
            $"select actor_tipo, actor_id from agenda.bitacora where entidad = 'Cita' and accion = 'Cancelada' and entidad_id = '{deAna.Valor.CitaId}'",
            l => (l.GetString(0), l.GetString(1)));
        Assert.Equal(("Administrador", IdDelAdministrador), Assert.Single(bitacora));
        Assert.DoesNotContain(await HorariosLibresAsync(escenario), d => d.Fecha == Sabado);
    }

    // Hasta que no quede nada pendiente: P-1 publica CitaCancelada, que se entrega en la vuelta siguiente.
    private static async Task EntregarTodoAsync(Escenario escenario)
    {
        for (var vuelta = 0; vuelta < 5 && await Arnes.DarUnaVueltaDeEntregaAsync(escenario.Web, Cancelacion) > 0; vuelta++)
        {
        }
    }

    private static List<MensajeDeCorreo> CorreosA(Escenario escenario, string destinatario) =>
        [.. Arnes.Canal(escenario.Web).Enviados.Where(c => c.Destinatario == destinatario)];

    private static void IrA(Escenario escenario, DateTimeOffset instante) => escenario.Reloj.Avanzar(instante - escenario.Reloj.UtcNow);

    private static string Token(CitaAgendadaEnLinea cita) =>
        cita.EnlaceDeGestion[(cita.EnlaceDeGestion.LastIndexOf("/c/", StringComparison.Ordinal) + 3)..];

    private static async Task<IReadOnlyList<DiaConHorarios>> HorariosLibresAsync(Escenario escenario)
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<IQueryHandler<HorariosLibres, IReadOnlyList<DiaConHorarios>>>()
            .HandleAsync(new HorariosLibres(TiposDeVisitaSembrados.PrimeraConsulta), Cancelacion);
        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        return resultado.Valor;
    }

    private static async Task<Resultado<T>> ComoPacienteAsync<TComando, T>(Escenario escenario, TComando comando)
        where TComando : ICommand<T>
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);
        return await ambito.ServiceProvider.GetRequiredService<ICommandHandler<TComando, T>>().HandleAsync(comando, Cancelacion);
    }

    private static async Task<Resultado<T>> ComoAdministradorAsync<TComando, T>(Escenario escenario, TComando comando)
        where TComando : ICommand<T>
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        return await ambito.ServiceProvider.GetRequiredService<ICommandHandler<TComando, T>>().HandleAsync(comando, Cancelacion);
    }

    private static async Task<Resultado<T>> ComoAdministradorConsultaAsync<TConsulta, T>(Escenario escenario, TConsulta consulta)
        where TConsulta : IQuery<T>
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        ComoAdministrador(ambito);
        return await ambito.ServiceProvider.GetRequiredService<IQueryHandler<TConsulta, T>>().HandleAsync(consulta, Cancelacion);
    }

    private static void ComoAdministrador(AsyncServiceScope ambito) =>
        Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, IdDelAdministrador), new Claim(ClaimTypes.Name, "administradora")],
            authenticationType: "Prueba")));
}
