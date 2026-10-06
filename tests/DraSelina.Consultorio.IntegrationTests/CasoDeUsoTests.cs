using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.IntegrationTests.Pruebas.FueraDeModulo;
using DraSelina.Consultorio.Kernel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Un caso de uso recorre <see cref="ISender"/>, la transacción y el outbox. Los comandos, las consultas y el
/// evento son de prueba (<c>Pruebas/</c>) y corren en la web en memoria, con su <c>Program</c> real: la política
/// de transacción es la del host. Todo se despacha por <see cref="ISender"/>.
/// </summary>
public sealed class CasoDeUsoTests(Arnes arnes)
{
    private static readonly TimeSpan UnaHora = TimeSpan.FromHours(1);

    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Un_comando_con_exito_guarda_su_cambio_y_su_mensaje()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando(avisarEn: UnaHora);

        var resultado = await EnviarAsync(web, comando);

        Assert.True(resultado.EsExito);
        Assert.Equal(comando.Id, resultado.Valor);
        Assert.True(await ExisteAnotacionAsync(comando.Id));
        Assert.Equal(1, await MensajesDeAsync(comando.Id));
    }

    [Fact]
    public async Task Un_fallo_antes_de_cambiar_vuelve_como_resultado_con_codigo_y_no_deja_nada()
    {
        await using var web = arnes.ArrancarWeb();
        var primero = NuevoComando(avisarEn: UnaHora);
        Assert.True((await EnviarAsync(web, primero)).EsExito);

        // El mismo texto otra vez: la condición que el handler rechaza de entrada.
        var repetido = NuevoComando(avisarEn: UnaHora);
        repetido.Texto = primero.Texto;

        var resultado = await EnviarAsync(web, repetido);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAnotacion.TextoRepetido, resultado.Codigo);
        Assert.False(await ExisteAnotacionAsync(repetido.Id));
        Assert.Equal(0, await MensajesDeAsync(repetido.Id));
    }

    [Fact]
    public async Task Un_fallo_despues_de_cambiar_deshace_el_cambio_y_el_mensaje()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando(avisarEn: UnaHora);
        comando.Desenlace = Desenlace.FalloAlFinal;

        // El handler guardó y publicó antes de devolver el fallo. No hay excepción.
        var resultado = await EnviarAsync(web, comando);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAnotacion.RechazadaAlFinal, resultado.Codigo);
        Assert.False(await ExisteAnotacionAsync(comando.Id));
        Assert.Equal(0, await MensajesDeAsync(comando.Id));
    }

    [Fact]
    public async Task Un_fallo_inesperado_sale_como_excepcion_y_no_deja_ni_cambio_ni_mensaje()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando(avisarEn: UnaHora);
        comando.Desenlace = Desenlace.LanzarAlFinal;

        // El handler guardó y publicó antes de lanzar.
        await Assert.ThrowsAsync<FalloInesperadoDePrueba>(() => EnviarAsync(web, comando));

        Assert.False(await ExisteAnotacionAsync(comando.Id));
        Assert.Equal(0, await MensajesDeAsync(comando.Id));
    }

    [Fact]
    public async Task Si_el_handler_no_guarda_el_host_no_guarda_por_el()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando(avisarEn: UnaHora);
        comando.Desenlace = Desenlace.ExitoSinGuardar;

        var resultado = await EnviarAsync(web, comando);

        Assert.True(resultado.EsExito);
        Assert.False(await ExisteAnotacionAsync(comando.Id));
    }

    [Fact]
    public async Task Un_handler_que_alcanza_dos_contextos_corre_en_la_transaccion_del_suyo()
    {
        // Que la web arranque ya es parte de la prueba: el handler de este comando está siempre descubierto.
        await using var web = arnes.ArrancarWeb();
        var comando = new AnotarConDosContextos { Id = Guid.CreateVersion7(), Texto = $"anotación {Guid.NewGuid():N}" };

        var resultado = await EnviarAsync(web, comando);

        Assert.True(resultado.EsExito);
        Assert.True(resultado.Valor, "El contexto del módulo no tenía transacción, o el del otro módulo quedó dentro de una.");
        Assert.True(await ExisteAnotacionAsync(comando.Id));
        Assert.Equal(1, await MensajesDeAsync(comando.Id));
    }

    [Fact]
    public async Task El_mensaje_llega_a_su_handler_despues_de_confirmada_la_transaccion()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando(avisarEn: null);

        Assert.True((await EnviarAsync(web, comando)).EsExito);

        var rastro = await EsperarRastroAsync(comando.Id, TimeSpan.FromSeconds(30));
        Assert.True(rastro.AnotacionConfirmada, "El handler del evento corrió sin ver la anotación confirmada.");
    }

    [Fact]
    public async Task Una_consulta_devuelve_el_dato_leido_y_corre_sin_transaccion()
    {
        await using var web = arnes.ArrancarWeb();
        var anotacion = new Anotacion { Id = Guid.CreateVersion7(), Texto = $"anotación {Guid.NewGuid():N}" };
        await using (var escritura = arnes.CrearContextoDePruebas())
        {
            escritura.Anotaciones.Add(anotacion);
            await escritura.SaveChangesAsync(Cancelacion);
        }

        await using var ambito = web.Services.CreateAsyncScope();
        var resultado = await ambito.ServiceProvider.GetRequiredService<ISender>()
            .SendAsync(new LeerAnotacion { Id = anotacion.Id }, Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(anotacion.Texto, resultado.Valor.Texto);
        Assert.False(resultado.Valor.HabiaTransaccion, "El handler de la consulta vio una transacción abierta.");
    }

    [Fact]
    public async Task Lo_que_una_consulta_agrega_y_no_guarda_no_queda()
    {
        await using var web = arnes.ArrancarWeb();
        var consulta = new LeerAgregando { Id = Guid.CreateVersion7() };

        await using var ambito = web.Services.CreateAsyncScope();
        var resultado = await ambito.ServiceProvider.GetRequiredService<ISender>().SendAsync(consulta, Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.False(await ExisteAnotacionAsync(consulta.Id));
    }

    [Fact]
    public async Task Un_comando_sin_handler_lanza_una_excepcion_que_nombra_su_tipo()
    {
        await using var web = arnes.ArrancarWeb();

        var error = await Assert.ThrowsAnyAsync<Exception>(() => EnviarAsync(web, new ComandoSinHandler()));

        Assert.Contains(nameof(ComandoSinHandler), error.Message);
    }

    [Fact]
    public async Task Un_handler_de_comando_fuera_de_todo_modulo_impide_arrancar_y_el_error_lo_nombra()
    {
        await using var arranque = arnes.ArrancarWeb();
        await using var web = arranque.WithWebHostBuilder(builder => builder.ConfigureServices(servicios =>
            servicios.ConfigureWolverine(opciones => opciones.Discovery.IncludeType(typeof(DespachoFueraDeModulo)))));

        var error = Assert.ThrowsAny<Exception>(() => web.Services);

        Assert.Contains(nameof(DespachoFueraDeModulo), error.ToString());
        Assert.Contains(typeof(DespachoFueraDeModulo).Namespace!, error.ToString());
    }

    private static Anotar NuevoComando(TimeSpan? avisarEn) =>
        new() { Id = Guid.CreateVersion7(), Texto = $"anotación {Guid.NewGuid():N}", AvisarEn = avisarEn };

    // Siempre por ISender, resuelto del contenedor de la web en memoria: el único camino.
    private static async Task<Resultado<T>> EnviarAsync<T>(WebApplicationFactory<Program> web, ICommand<T> comando)
    {
        await using var ambito = web.Services.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<ISender>().SendAsync(comando, Cancelacion);
    }

    private async Task<bool> ExisteAnotacionAsync(Guid id)
    {
        await using var contexto = arnes.CrearContextoDePruebas();
        return await contexto.Anotaciones.AsNoTracking().AnyAsync(a => a.Id == id, Cancelacion);
    }

    // El evento lleva el id de su anotación: con él se encuentra su mensaje en el esquema wolverine.
    private Task<long> MensajesDeAsync(Guid anotacionId) =>
        Consultas.MensajesGuardadosAsync(arnes.CadenaDeConexion, anotacionId.ToString(), Cancelacion);

    private async Task<RastroDeAnotacion> EsperarRastroAsync(Guid anotacionId, TimeSpan limite)
    {
        var fin = DateTime.UtcNow + limite;
        while (true)
        {
            await using (var contexto = arnes.CrearContextoDePruebas())
            {
                var rastro = await contexto.Rastros.AsNoTracking().SingleOrDefaultAsync(r => r.Id == anotacionId, Cancelacion);
                if (rastro is not null)
                {
                    return rastro;
                }
            }

            Assert.True(DateTime.UtcNow < fin, $"El handler del evento no dejó su rastro en {limite.TotalSeconds:0} segundos.");
            await Task.Delay(TimeSpan.FromMilliseconds(100), Cancelacion);
        }
    }
}
