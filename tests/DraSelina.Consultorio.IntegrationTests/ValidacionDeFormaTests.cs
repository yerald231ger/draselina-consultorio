using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La forma de un comando se valida antes de su handler y el fallo llega como <see cref="Resultado"/> inválido
/// (AD-12). Contra la web real del arnés: el handler se pide al contenedor por su interfaz, como lo pide la
/// interfaz, y el decorador es el que el host aplica a todo handler de comando.
/// </summary>
public sealed class ValidacionDeFormaTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Un_comando_valido_llega_a_su_handler_una_vez_y_su_resultado_vuelve_tal_cual()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = Valido();

        var resultado = await InscribirAsync(web, comando);

        Assert.True(resultado.EsExito);
        Assert.Equal(comando.Id, resultado.Valor);
        Assert.Equal(1, Llamadas(web));
    }

    [Fact]
    public async Task Un_comando_invalido_en_varios_campos_vuelve_invalido_por_propiedad_y_el_handler_no_corre()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = Valido();
        comando.Nombre = "";
        comando.Correo = "no-es-correo";
        comando.Edad = 5;

        var resultado = await InscribirAsync(web, comando);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Equal(
            new[] { nameof(Inscribir.Correo), nameof(Inscribir.Edad), nameof(Inscribir.Nombre) },
            resultado.Errores.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([Inscribir.MensajeNombreRequerido], resultado.Errores[nameof(Inscribir.Nombre)]);
        Assert.Equal([Inscribir.MensajeCorreoMalFormado], resultado.Errores[nameof(Inscribir.Correo)]);
        Assert.Equal([Inscribir.MensajeEdadFueraDeRango], resultado.Errores[nameof(Inscribir.Edad)]);
        Assert.Equal(0, Llamadas(web));
    }

    [Fact]
    public async Task Un_campo_que_rompe_dos_atributos_lleva_los_dos_mensajes()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = Valido();
        comando.Correo = new string('x', 50);   // sin arroba y más largo que 40

        var resultado = await InscribirAsync(web, comando);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Equal([nameof(Inscribir.Correo)], resultado.Errores.Keys);
        Assert.Equal(
            new[] { Inscribir.MensajeCorreoLargo, Inscribir.MensajeCorreoMalFormado }.Order(StringComparer.Ordinal),
            resultado.Errores[nameof(Inscribir.Correo)].Order(StringComparer.Ordinal));
        Assert.Equal(0, Llamadas(web));
    }

    [Fact]
    public async Task Un_error_del_objeto_va_bajo_la_propiedad_vacia_y_el_handler_no_corre()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = Valido();
        comando.ErrorDelObjeto = true;

        var resultado = await InscribirAsync(web, comando);

        Assert.Equal(FormaDeResultado.Invalido, resultado.Forma);
        Assert.Equal([""], resultado.Errores.Keys);
        Assert.Equal([Inscribir.MensajeDelObjeto], resultado.Errores[""]);
        Assert.Equal(0, Llamadas(web));
    }

    [Fact]
    public async Task Un_comando_sin_atributos_pasa_directo_a_su_handler()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        var comando = new Anotar { Id = Guid.CreateVersion7(), Texto = $"anotación {Guid.NewGuid():N}" };

        var resultado = await ambito.ServiceProvider
            .GetRequiredService<ICommandHandler<Anotar, Guid>>()
            .HandleAsync(comando, Cancelacion);

        Assert.True(resultado.EsExito);
        Assert.Equal(comando.Id, resultado.Valor);
    }

    [Fact]
    public async Task El_handler_de_una_consulta_no_lo_envuelve_el_validador()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();

        var handler = ambito.ServiceProvider.GetRequiredService<IQueryHandler<LeerAnotacion, AnotacionLeida>>();

        Assert.IsType<LeerAnotacionHandler>(handler);
    }

    [Fact]
    public async Task El_validador_envuelve_una_sola_vez_al_handler_real_de_cada_comando()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();

        var interfaces = CasosDeUso.HandlersDeComando(CasosDeUso.DeLaWebYDePrueba()).ToList();
        Assert.Contains(typeof(ICommandHandler<Anotar, Guid>), interfaces);
        Assert.Contains(typeof(ICommandHandler<Inscribir, Guid>), interfaces);

        foreach (var interfaz in interfaces)
        {
            var handler = Assert.Single(ambito.ServiceProvider.GetServices(interfaz));
            Assert.True(EsValidador(handler), $"{interfaz} no lo envuelve el validador: es {handler!.GetType()}.");

            var siguiente = handler!.GetType().GetProperty(nameof(ValidacionDeComandos<Anotar, Guid>.Siguiente))!.GetValue(handler);
            Assert.False(EsValidador(siguiente), $"{interfaz} está envuelto más de una vez por el validador.");
        }

        // Y en un comando conocido, lo que queda directo bajo el validador es el handler real.
        var anotar = ambito.ServiceProvider.GetRequiredService<ICommandHandler<Anotar, Guid>>();
        Assert.IsType<AnotarHandler>(Assert.IsType<ValidacionDeComandos<Anotar, Guid>>(anotar).Siguiente);
    }

    private static bool EsValidador(object? handler) =>
        handler?.GetType() is { IsConstructedGenericType: true } tipo
        && tipo.GetGenericTypeDefinition() == typeof(ValidacionDeComandos<,>);

    private static Inscribir Valido() => new()
    {
        Id = Guid.CreateVersion7(),
        Nombre = "Ana López",
        Correo = "ana@ejemplo.mx",
        Edad = 34,
    };

    private static async Task<Resultado<Guid>> InscribirAsync(WebApplicationFactory<Program> web, Inscribir comando)
    {
        await using var ambito = web.Services.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<ICommandHandler<Inscribir, Guid>>().HandleAsync(comando, Cancelacion);
    }

    private static int Llamadas(WebApplicationFactory<Program> web) => web.Services.GetRequiredService<LlamadasDeInscribir>().Cuantas;
}
