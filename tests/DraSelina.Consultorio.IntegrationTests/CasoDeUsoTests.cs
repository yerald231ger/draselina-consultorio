using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Kernel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Un caso de uso recorre su handler, su transacción y <see cref="Resultado"/> (AD-10). Los comandos y la consulta
/// son de prueba (<c>Pruebas/</c>) y corren en la web en memoria, con su <c>Program</c> real. El handler siempre se
/// pide al contenedor por su interfaz, dentro de un ámbito; ninguna prueba crea la clase del handler.
/// </summary>
public sealed class CasoDeUsoTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Un_comando_con_exito_devuelve_su_valor_y_deja_su_fila()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando();

        var resultado = await EjecutarAsync(web, comando);

        Assert.True(resultado.EsExito);
        Assert.Equal(comando.Id, resultado.Valor);
        Assert.True(await ExisteAnotacionAsync(comando.Id));
    }

    [Fact]
    public async Task Un_fallo_antes_de_cambiar_vuelve_como_resultado_con_codigo_y_no_deja_fila()
    {
        await using var web = arnes.ArrancarWeb();
        var primero = NuevoComando();
        Assert.True((await EjecutarAsync(web, primero)).EsExito);

        // El mismo texto otra vez: la condición que el handler rechaza de entrada.
        var repetido = NuevoComando();
        repetido.Texto = primero.Texto;

        var resultado = await EjecutarAsync(web, repetido);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAnotacion.TextoRepetido, resultado.Codigo);
        Assert.False(await ExisteAnotacionAsync(repetido.Id));
    }

    [Fact]
    public async Task Un_fallo_despues_de_cambiar_vuelve_como_resultado_con_codigo_y_deshace_el_cambio()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando(Desenlace.FalloAlFinal);

        // El handler guardó antes de descubrir el fallo. No hay excepción.
        var resultado = await EjecutarAsync(web, comando);

        Assert.Equal(FormaDeResultado.Fallo, resultado.Forma);
        Assert.Equal(CodigosDeAnotacion.RechazadaAlFinal, resultado.Codigo);
        Assert.False(await ExisteAnotacionAsync(comando.Id));
    }

    [Fact]
    public async Task Un_fallo_inesperado_sale_como_excepcion_y_no_deja_fila()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando(Desenlace.LanzarAlFinal);

        // El handler guardó antes de lanzar.
        await Assert.ThrowsAsync<FalloInesperadoDePrueba>(() => EjecutarAsync(web, comando));

        Assert.False(await ExisteAnotacionAsync(comando.Id));
    }

    [Fact]
    public async Task Lo_que_el_handler_guarda_y_no_confirma_no_queda()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando(Desenlace.ExitoSinConfirmar);

        var resultado = await EjecutarAsync(web, comando);

        Assert.True(resultado.EsExito);
        Assert.False(await ExisteAnotacionAsync(comando.Id));
    }

    [Theory]
    [InlineData(Desenlace.FalloAlFinal)]
    [InlineData(Desenlace.FalloSinGuardar)]
    [InlineData(Desenlace.LanzarAlFinal)]
    [InlineData(Desenlace.ExitoSinConfirmar)]
    public async Task Un_comando_que_no_confirma_no_estorba_al_siguiente_del_mismo_ambito(Desenlace desenlace)
    {
        // En administración el ámbito dura lo que el circuito: dos comandos seguidos comparten contexto. Lo que el
        // primero agregó y no confirmó no debe guardarse con el segundo.
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        var handler = ambito.ServiceProvider.GetRequiredService<ICommandHandler<Anotar, Guid>>();
        var sinConfirmar = NuevoComando(desenlace);
        var siguiente = NuevoComando();

        try
        {
            await handler.HandleAsync(sinConfirmar, Cancelacion);
        }
        catch (FalloInesperadoDePrueba)
        {
            // El desenlace que lanza.
        }

        var resultado = await handler.HandleAsync(siguiente, Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.True(await ExisteAnotacionAsync(siguiente.Id));
        Assert.False(await ExisteAnotacionAsync(sinConfirmar.Id));
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
        var resultado = await ambito.ServiceProvider
            .GetRequiredService<IQueryHandler<LeerAnotacion, AnotacionLeida>>()
            .HandleAsync(new LeerAnotacion { Id = anotacion.Id }, Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(anotacion.Texto, resultado.Valor.Texto);
        Assert.False(resultado.Valor.HabiaTransaccion, "El handler de la consulta vio una transacción abierta.");
    }

    [Fact]
    public async Task Pedir_el_handler_de_un_comando_que_no_tiene_lanza_una_excepcion_que_nombra_su_tipo()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();

        // Nada propio: es lo que hace el contenedor cuando se le pide un servicio que nadie registró.
        var error = Assert.Throws<InvalidOperationException>(
            () => ambito.ServiceProvider.GetRequiredService<ICommandHandler<ComandoSinHandler, int>>());

        Assert.Contains(nameof(ComandoSinHandler), error.Message);
    }

    private static Anotar NuevoComando(Desenlace desenlace = Desenlace.Exito) =>
        new() { Id = Guid.CreateVersion7(), Texto = $"anotación {Guid.NewGuid():N}", Desenlace = desenlace };

    // El handler se pide al contenedor de la web en memoria por su interfaz, en un ámbito propio.
    private static async Task<Resultado<T>> EjecutarAsync<TComando, T>(WebApplicationFactory<Program> web, TComando comando)
        where TComando : ICommand<T>
    {
        await using var ambito = web.Services.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<ICommandHandler<TComando, T>>().HandleAsync(comando, Cancelacion);
    }

    private static Task<Resultado<Guid>> EjecutarAsync(WebApplicationFactory<Program> web, Anotar comando) =>
        EjecutarAsync<Anotar, Guid>(web, comando);

    private async Task<bool> ExisteAnotacionAsync(Guid id)
    {
        await using var contexto = arnes.CrearContextoDePruebas();
        return await contexto.Anotaciones.AsNoTracking().AnyAsync(a => a.Id == id, Cancelacion);
    }
}
