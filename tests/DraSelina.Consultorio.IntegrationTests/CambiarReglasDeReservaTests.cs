using System.Security.Claims;
using System.Text.Json;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Contracts;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// <see cref="CambiarReglasDeReserva"/> y <see cref="ReglasDeReservaVigentes"/> contra PostgreSQL real (CAP-16, FR-19,
/// entrada 6.6): los valores que decide <c>ReglasDeReserva</c>, la bitácora y el efecto en la siguiente reserva sin
/// reiniciar. Con el escenario y el reloj de <see cref="ReservaEnLineaTests"/>: lunes 2 de noviembre de 2026 a las 12:00
/// local; reglas sembradas 1 día, 2 meses, 120 minutos y 3 citas.
/// </summary>
public sealed class CambiarReglasDeReservaTests(Arnes arnes)
{
    private const string IdDelAdministrador = "cuenta-del-administrador";

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Las_reglas_vigentes_son_las_sembradas()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var vigentes = await ComoAdministradorAsync(escenario, (IQueryHandler<ReglasDeReservaVigentes, ReglasDeReservaValores> q) =>
            q.HandleAsync(new ReglasDeReservaVigentes(), Cancelacion));

        Assert.Equal(new ReglasDeReservaValores(1, 2, 120, 3), vigentes.Valor);
    }

    [Fact]
    public async Task Subir_la_ventana_a_tres_meses_ofrece_el_tercer_mes_en_la_siguiente_reserva()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        var limite = new DateOnly(2027, 1, 2);
        Assert.All(await HorariosAsync(escenario), d => Assert.True(d.Fecha <= limite));

        var resultado = await CambiarAsync(escenario, Comando(1, 3, 120, 3));

        Assert.True(resultado.EsExito, $"{resultado.Forma} {resultado.Codigo}");
        Assert.Equal(new ReglasDeReservaValores(1, 3, 120, 3), resultado.Valor);
        Assert.Contains(await HorariosAsync(escenario), d => d.Fecha > limite);

        var entrada = Assert.Single(await escenario.FilasAsync(
            "select actor_tipo, actor_id, entidad, entidad_id, accion, detalle::text from agenda.bitacora",
            l => (Actor: l.GetString(0), Id: l.GetString(1), Entidad: l.GetString(2), EntidadId: l.GetString(3), Accion: l.GetString(4), Detalle: l.GetString(5))));
        Assert.Equal(("Administrador", IdDelAdministrador, "ReglasDeReserva", "1", "Cambiada"), (entrada.Actor, entrada.Id, entrada.Entidad, entrada.EntidadId, entrada.Accion));
        using var detalle = JsonDocument.Parse(entrada.Detalle);
        Assert.Equal(2, detalle.RootElement.GetProperty("antes").GetProperty("ventanaMeses").GetInt32());
        Assert.Equal(3, detalle.RootElement.GetProperty("despues").GetProperty("ventanaMeses").GetInt32());
    }

    [Theory]
    [InlineData(-1, 2, 120, 3, nameof(CambiarReglasDeReserva.AnticipacionMinimaDias), CambiarReglasDeReserva.MensajeDeAnticipacion)]
    [InlineData(1, 0, 120, 3, nameof(CambiarReglasDeReserva.VentanaMeses), CambiarReglasDeReserva.MensajeDeVentana)]
    [InlineData(1, 13, 120, 3, nameof(CambiarReglasDeReserva.VentanaMeses), CambiarReglasDeReserva.MensajeDeVentanaMaxima)]
    [InlineData(1, 2, -5, 3, nameof(CambiarReglasDeReserva.UmbralCancelacionTardiaMin), CambiarReglasDeReserva.MensajeDeUmbral)]
    [InlineData(1, 2, 120, 0, nameof(CambiarReglasDeReserva.TopeSemanalPorTelefono), CambiarReglasDeReserva.MensajeDeTope)]
    [InlineData(1, 40000, 120, 3, nameof(CambiarReglasDeReserva.VentanaMeses), CambiarReglasDeReserva.MensajeDeMaximo)]
    public async Task Un_valor_que_no_sirve_va_bajo_su_campo_y_nada_cambia(
        int anticipacion, int ventana, int umbral, int tope, string propiedad, string mensaje)
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        var resultado = await CambiarAsync(escenario, Comando(anticipacion, ventana, umbral, tope));

        Assert.Equal([mensaje], resultado.Errores[propiedad]);
        Assert.Equal(2, await escenario.EscalarAsync<short>("select ventana_meses from agenda.reglas_de_reserva"));
        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"));
    }

    [Fact]
    public async Task Los_mismos_valores_no_dejan_bitacora()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);

        Assert.True((await CambiarAsync(escenario, Comando(1, 2, 120, 3))).EsExito);

        Assert.Equal(0L, await escenario.EscalarAsync<long>("select count(*) from agenda.bitacora"));
    }

    [Fact]
    public async Task Sin_sesion_los_dos_comandos_de_Configuracion_vuelven_no_autorizado()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        SinSesion(ambito);

        var reglas = await ambito.ServiceProvider.GetRequiredService<ICommandHandler<CambiarReglasDeReserva, ReglasDeReservaValores>>()
            .HandleAsync(Comando(1, 3, 120, 3), Cancelacion);
        var envio = await ambito.ServiceProvider.GetRequiredService<ICommandHandler<CambiarConfiguracionDeEnvio, ConfiguracionDeEnvioValores>>()
            .HandleAsync(
                new CambiarConfiguracionDeEnvio
                {
                    HoraDelRecordatorio = new TimeOnly(8, 0),
                    HoraDeLaAgendaDelDia = new TimeOnly(14, 0),
                    CorreoDeLaDoctora = "doctora@correo.com",
                    CorreoDelAdministrador = "admin@correo.com",
                },
                Cancelacion);

        Assert.Equal(FormaDeResultado.NoAutorizado, reglas.Forma);
        Assert.Equal(FormaDeResultado.NoAutorizado, envio.Forma);
        Assert.Equal(2, await escenario.EscalarAsync<short>("select ventana_meses from agenda.reglas_de_reserva"));
    }

    private static CambiarReglasDeReserva Comando(int anticipacion, int ventana, int umbral, int tope) => new()
    {
        AnticipacionMinimaDias = anticipacion,
        VentanaMeses = ventana,
        UmbralCancelacionTardiaMin = umbral,
        TopeSemanalPorTelefono = tope,
    };

    private static async Task<T> ComoAdministradorAsync<THandler, T>(Escenario escenario, Func<THandler, Task<T>> llamar)
        where THandler : notnull
    {
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, IdDelAdministrador), new Claim(ClaimTypes.Name, "administradora")],
            authenticationType: "Prueba")));
        return await llamar(ambito.ServiceProvider.GetRequiredService<THandler>());
    }

    private static Task<Resultado<ReglasDeReservaValores>> CambiarAsync(Escenario escenario, CambiarReglasDeReserva comando) =>
        ComoAdministradorAsync(escenario, (ICommandHandler<CambiarReglasDeReserva, ReglasDeReservaValores> h) => h.HandleAsync(comando, Cancelacion));

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
}
