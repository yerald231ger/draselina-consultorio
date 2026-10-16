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

        // Solo la envuelve el rechazo sin Administrador (AD-22); debajo está el handler real.
        var rechazo = Assert.IsType<RechazoSinAdministradorDeConsultas<LeerAnotacion, AnotacionLeida>>(handler);
        Assert.IsType<LeerAnotacionHandler>(rechazo.Siguiente);
    }

    [Fact]
    public async Task Cada_decorador_envuelve_una_sola_vez_al_handler_real_de_cada_comando_y_consulta()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();

        var casos = CasosDeUso.DeLaWebYDePrueba();
        var deComando = CasosDeUso.HandlersDeComando(casos).ToList();
        var deConsulta = casos.SelectMany(CasosDeUso.Handlers).Except(deComando).ToList();
        Assert.Contains(typeof(ICommandHandler<Anotar, Guid>), deComando);
        Assert.Contains(typeof(ICommandHandler<Inscribir, Guid>), deComando);
        Assert.Contains(typeof(IQueryHandler<LeerAnotacion, AnotacionLeida>), deConsulta);

        // Comando: rechazo → validador → real. Consulta: rechazo → real. Cada uno una vez.
        foreach (var interfaz in deComando)
        {
            var cadena = Cadena(Assert.Single(ambito.ServiceProvider.GetServices(interfaz))!);
            Assert.True(
                cadena.Count == 3
                && EsDe(cadena[0], typeof(RechazoSinAdministradorDeComandos<,>))
                && EsDe(cadena[1], typeof(ValidacionDeComandos<,>))
                && !EsDecorador(cadena[2]),
                $"{interfaz} no tiene la cadena rechazo → validador → real: {Nombres(cadena)}.");
        }

        foreach (var interfaz in deConsulta)
        {
            var cadena = Cadena(Assert.Single(ambito.ServiceProvider.GetServices(interfaz))!);
            Assert.True(
                cadena.Count == 2
                && EsDe(cadena[0], typeof(RechazoSinAdministradorDeConsultas<,>))
                && !EsDecorador(cadena[1]),
                $"{interfaz} no tiene la cadena rechazo → real: {Nombres(cadena)}.");
        }

        // Y en un comando conocido, lo que queda al fondo es el handler real.
        var anotar = ambito.ServiceProvider.GetRequiredService<ICommandHandler<Anotar, Guid>>();
        var rechazo = Assert.IsType<RechazoSinAdministradorDeComandos<Anotar, Guid>>(anotar);
        Assert.IsType<AnotarHandler>(Assert.IsType<ValidacionDeComandos<Anotar, Guid>>(rechazo.Siguiente).Siguiente);
    }

    private static readonly Type[] Decoradores =
    [
        typeof(ValidacionDeComandos<,>),
        typeof(RechazoSinAdministradorDeComandos<,>),
        typeof(RechazoSinAdministradorDeConsultas<,>),
    ];

    private static bool EsDe(object handler, Type decorador) =>
        handler.GetType() is { IsConstructedGenericType: true } tipo && tipo.GetGenericTypeDefinition() == decorador;

    private static bool EsDecorador(object handler) => Decoradores.Any(d => EsDe(handler, d));

    /// <summary>Del de afuera al de adentro, siguiendo <c>Siguiente</c> de cada decorador.</summary>
    private static List<object> Cadena(object handler)
    {
        var cadena = new List<object> { handler };
        while (EsDecorador(cadena[^1]))
        {
            cadena.Add(cadena[^1].GetType().GetProperty(nameof(ValidacionDeComandos<Anotar, Guid>.Siguiente))!.GetValue(cadena[^1])!);
        }

        return cadena;
    }

    private static string Nombres(List<object> cadena) => string.Join(" → ", cadena.Select(h => h.GetType().Name));

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
