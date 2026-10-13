using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// El servicio del host entrega cada evento confirmado a sus handlers (AD-3). Los eventos y sus handlers son de
/// prueba (<c>Pruebas/Eventos.cs</c>); lo que cada prueba afirma sale de la base: la fila del outbox y el rastro que
/// dejó cada handler.
/// </summary>
public sealed class EntregaDeEventosTests(Arnes arnes)
{
    // Lo más que se espera al servicio en segundo plano, que da una vuelta cada 5 segundos.
    private static readonly TimeSpan Limite = TimeSpan.FromSeconds(30);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task El_servicio_en_segundo_plano_entrega_el_evento_despues_de_confirmada_la_transaccion()
    {
        // La web real, con su servicio de entrega, contra la base de la corrida.
        await using var web = arnes.ArrancarWeb();
        var comando = new Anotar { Id = Guid.CreateVersion7(), Texto = $"anotación {Guid.NewGuid():N}" };

        await using (var ambito = web.Services.CreateAsyncScope())
        {
            var handler = ambito.ServiceProvider.GetRequiredService<ICommandHandler<Anotar, Guid>>();
            Assert.True((await handler.HandleAsync(comando, Cancelacion)).EsExito);
        }

        var entregado = await Espera.HastaAsync(async () => (await EventoDeLaAnotacionAsync(comando.Id)).Estado == "Entregado", Limite, Cancelacion);
        Assert.True(entregado, "El servicio de entrega no entregó el evento.");

        var fila = await EventoDeLaAnotacionAsync(comando.Id);
        Assert.NotNull(fila.EntregadoEn);
        Assert.Equal(0, fila.Intentos);

        // El handler solo deja su rastro si ya ve la anotación, es decir, con la transacción del comando confirmada.
        // Puede haberlo recibido más de una vez: en la base de la corrida lo entrega cualquier web, y una que se
        // apaga a media entrega lo deja Pendiente para la siguiente.
        await using var contexto = arnes.CrearContextoDePruebas();
        var rastro = await contexto.Entregas.AsNoTracking().Where(e => e.EventoId == fila.Id).ToListAsync(Cancelacion);
        Assert.NotEmpty(rastro);
        Assert.All(rastro, e => Assert.Equal(nameof(AlHacerseUnaAnotacion), e.Handler));
    }

    [Fact]
    public async Task Un_evento_con_dos_handlers_llega_a_los_dos()
    {
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
        var eventoId = await escenario.PublicarAsync(id => new EventoConDosHandlers(id), claveDeOrden: Guid.CreateVersion7());

        Assert.Equal(1, await escenario.VueltaAsync());

        var rastro = await escenario.EntregasAsync(eventoId);
        Assert.Equal([nameof(PrimeraEscucha), nameof(SegundaEscucha)], rastro.Select(e => e.Handler).Order());

        var fila = await escenario.EventoAsync(eventoId);
        Assert.Equal("Entregado", fila.Estado);
        Assert.Equal(escenario.Reloj.UtcNow, fila.EntregadoEn);
    }

    [Fact]
    public async Task Un_evento_publicado_desde_una_variable_de_tipo_object_llega_a_su_handler()
    {
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);

        // Quien publica tiene el evento en una variable de tipo object: cuenta el tipo real del evento.
        var eventoId = await escenario.PublicarAsync<object>(id => new EventoDePrueba(id), claveDeOrden: Guid.CreateVersion7());

        Assert.Equal(nameof(EventoDePrueba), (await escenario.EventoAsync(eventoId)).Tipo);
        Assert.Equal(1, await escenario.VueltaAsync());

        Assert.Equal(nameof(EscuchaDePrueba), Assert.Single(await escenario.EntregasAsync(eventoId)).Handler);
        Assert.Equal("Entregado", (await escenario.EventoAsync(eventoId)).Estado);
    }

    [Fact]
    public async Task Un_evento_que_nadie_escucha_queda_entregado_y_se_registra_un_aviso()
    {
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
        var eventoId = await escenario.PublicarAsync(id => new EventoQueNadieEscucha(id), claveDeOrden: Guid.CreateVersion7());

        Assert.Equal(1, await escenario.VueltaAsync());

        var fila = await escenario.EventoAsync(eventoId);
        Assert.Equal("Entregado", fila.Estado);
        Assert.Equal(escenario.Reloj.UtcNow, fila.EntregadoEn);
        Assert.Equal(0, fila.Intentos);

        Assert.Contains(Arnes.Registros(escenario.Web), r =>
            r.Nivel == LogLevel.Warning && r.Mensaje.Contains(eventoId.ToString()) && r.Mensaje.Contains(nameof(EventoQueNadieEscucha)));
    }

    [Fact]
    public async Task Un_evento_ya_entregado_no_se_vuelve_a_entregar()
    {
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
        var eventoId = await escenario.PublicarAsync(id => new EventoDePrueba(id), claveDeOrden: Guid.CreateVersion7());

        Assert.Equal(1, await escenario.VueltaAsync());
        escenario.Reloj.Avanzar(TimeSpan.FromDays(1));
        Assert.Equal(0, await escenario.VueltaAsync());

        Assert.Single(await escenario.EntregasAsync(eventoId));
    }

    [Fact]
    public async Task Dos_instancias_que_dan_una_vuelta_a_la_vez_entregan_el_evento_una_sola_vez()
    {
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
        await using var otraWeb = escenario.OtraWeb();

        // El handler deja su rastro y tarda: la fila sigue apartada mientras la otra instancia da su vuelta.
        var eventoId = await escenario.PublicarAsync(id => new EventoDePrueba(id, DemoraMs: 1500), claveDeOrden: Guid.CreateVersion7());

        // Las dos webs ya arrancadas antes de la primera vuelta: la segunda no gasta su vuelta en arrancar.
        _ = escenario.Web.Services;
        _ = otraWeb.Services;

        var primera = escenario.VueltaAsync();
        Assert.True(
            await Espera.HastaAsync(async () => (await escenario.EntregasAsync(eventoId)).Count == 1, Limite, Cancelacion),
            "La primera instancia no llegó a llamar al handler.");

        var intentadosPorLaOtra = await Arnes.DarUnaVueltaDeEntregaAsync(otraWeb, Cancelacion);
        var intentadosPorLaPrimera = await primera;

        Assert.Equal(1, intentadosPorLaPrimera);
        Assert.Equal(0, intentadosPorLaOtra);
        Assert.Single(await escenario.EntregasAsync(eventoId));
        Assert.Equal("Entregado", (await escenario.EventoAsync(eventoId)).Estado);
    }

    [Fact]
    public async Task Dos_instancias_que_arrancan_su_vuelta_juntas_entregan_cada_evento_una_sola_vez()
    {
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
        await using var otraWeb = escenario.OtraWeb();

        var eventos = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            eventos.Add(await escenario.PublicarAsync(id => new EventoDePrueba(id, DemoraMs: 20), claveDeOrden: Guid.CreateVersion7()));
        }

        // Las dos webs ya arrancadas: si una arrancara dentro de su vuelta, la otra terminaría sola.
        _ = escenario.Web.Services;
        _ = otraWeb.Services;

        var intentados = await Task.WhenAll(escenario.VueltaAsync(), Arnes.DarUnaVueltaDeEntregaAsync(otraWeb, Cancelacion));

        Assert.Equal(eventos.Count, intentados.Sum());
        foreach (var eventoId in eventos)
        {
            Assert.Single(await escenario.EntregasAsync(eventoId));
            Assert.Equal("Entregado", (await escenario.EventoAsync(eventoId)).Estado);
        }
    }

    [Fact]
    public async Task Un_evento_que_quedo_pendiente_se_entrega_cuando_arranca_una_web_nueva()
    {
        var cadena = await arnes.CrearBaseMigradaAsync(Cancelacion);
        Guid eventoId;

        // La web que publica nunca llega a entregar: se apaga con el evento Pendiente.
        await using (var escenario = new EscenarioPrestado(arnes, cadena))
        {
            eventoId = await escenario.PublicarAsync();
        }

        var pendiente = await Consultas.EventoAsync(cadena, PruebasDbContext.Esquema, eventoId, Cancelacion);
        Assert.Equal("Pendiente", pendiente!.Estado);

        // La web nueva es la real, con su servicio de entrega y el reloj del sistema.
        await using var nueva = arnes.ArrancarWeb(cadena);
        _ = nueva.Services;

        var entregado = await Espera.HastaAsync(
            async () => (await Consultas.EventoAsync(cadena, PruebasDbContext.Esquema, eventoId, Cancelacion))!.Estado == "Entregado", Limite, Cancelacion);
        Assert.True(entregado, "La web nueva no entregó el evento que había quedado Pendiente.");

        await using var contexto = arnes.CrearContextoDePruebas(cadena);
        Assert.Equal(1, await contexto.Entregas.CountAsync(e => e.EventoId == eventoId, Cancelacion));
    }

    private async Task<FilaLeidaDeOutbox> EventoDeLaAnotacionAsync(Guid anotacionId) =>
        Assert.Single(await Consultas.EventosDeLaClaveAsync(arnes.CadenaDeConexion, PruebasDbContext.Esquema, anotacionId, Cancelacion));

    // Una web sin entrega automática sobre una base que ya existe: publica y se apaga.
    private sealed class EscenarioPrestado(Arnes arnes, string cadena) : IAsyncDisposable
    {
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _web =
            arnes.ArrancarWebSinEntregaAutomatica(cadena, new RelojDePrueba());

        public async Task<Guid> PublicarAsync()
        {
            var eventoId = Guid.CreateVersion7();

            await using var ambito = _web.Services.CreateAsyncScope();
            var transaccion = ambito.ServiceProvider.GetRequiredService<ITransaccionDePruebas>();
            var outbox = ambito.ServiceProvider.GetRequiredService<IOutboxDePruebas>();

            await using var tx = await transaccion.AbrirAsync(Cancelacion);

            // Ocurrió hace un momento según el reloj del sistema, que es el de la web que lo va a entregar.
            await outbox.PublicarAsync(new EventoDePrueba(eventoId), eventoId, Guid.CreateVersion7(), DateTimeOffset.UtcNow.AddSeconds(-1), Cancelacion);
            await tx.ConfirmarAsync(Cancelacion);

            return eventoId;
        }

        public ValueTask DisposeAsync() => _web.DisposeAsync();
    }
}
