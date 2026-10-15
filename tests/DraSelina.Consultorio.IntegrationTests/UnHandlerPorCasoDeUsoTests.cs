using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Todo caso de uso tiene exactamente un handler (AD-10). No se comprueba al arrancar la web: lo comprueba esta
/// prueba, contra el contenedor de la web real del arnés. Recorre los comandos y consultas de Web y los de prueba
/// (<see cref="CasosDeUso"/>), así que cuando los módulos tengan casos de uso la misma prueba los cubre sin cambios.
/// </summary>
public sealed class UnHandlerPorCasoDeUsoTests(Arnes arnes)
{
    [Fact]
    public async Task Cada_comando_y_cada_consulta_tiene_exactamente_un_handler()
    {
        var casosDeUso = CasosDeUso.DeLaWebYDePrueba();
        Assert.NotEmpty(casosDeUso);

        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();

        var problemas = Revisar(casosDeUso, ambito.ServiceProvider);

        Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
    }

    [Fact]
    public void El_verificador_nombra_el_caso_de_uso_sin_handler_y_los_que_tienen_dos()
    {
        var servicios = new ServiceCollection()
            .AddScoped<ICommandHandler<Anotar, Guid>, UnaAnotacion>()
            .AddScoped<ICommandHandler<Anotar, Guid>, OtraAnotacion>()
            .AddScoped<IQueryHandler<LeerAnotacion, AnotacionLeida>, UnaLectura>()
            .AddScoped<IQueryHandler<LeerAnotacion, AnotacionLeida>, OtraLectura>();
        using var proveedor = servicios.BuildServiceProvider();
        using var ambito = proveedor.CreateScope();

        var problemas = Revisar([typeof(ComandoSinHandler), typeof(Anotar), typeof(LeerAnotacion)], ambito.ServiceProvider);

        Assert.Equal(3, problemas.Count);
        Assert.Contains(problemas, p => p.Contains(nameof(ComandoSinHandler)) && p.Contains("0 handlers"));
        Assert.Contains(problemas, p => p.Contains($".{nameof(Anotar)} ") && p.Contains("2 handlers"));
        Assert.Contains(problemas, p => p.Contains(nameof(LeerAnotacion)) && p.Contains("2 handlers"));
    }

    /// <summary>Un mensaje por caso de uso que no tiene exactamente un handler; vacío si todos lo tienen.</summary>
    private static List<string> Revisar(IEnumerable<Type> casosDeUso, IServiceProvider proveedor) =>
        [
            .. from caso in casosDeUso
               from handler in CasosDeUso.Handlers(caso)
               let cuantos = proveedor.GetServices(handler).Count()
               where cuantos != 1
               select $"{caso.FullName} tiene {cuantos} handlers ({handler.Name}); debe tener exactamente uno.",
        ];

    private sealed class UnaAnotacion : ICommandHandler<Anotar, Guid>
    {
        public Task<Resultado<Guid>> HandleAsync(Anotar comando, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class OtraAnotacion : ICommandHandler<Anotar, Guid>
    {
        public Task<Resultado<Guid>> HandleAsync(Anotar comando, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnaLectura : IQueryHandler<LeerAnotacion, AnotacionLeida>
    {
        public Task<Resultado<AnotacionLeida>> HandleAsync(LeerAnotacion consulta, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class OtraLectura : IQueryHandler<LeerAnotacion, AnotacionLeida>
    {
        public Task<Resultado<AnotacionLeida>> HandleAsync(LeerAnotacion consulta, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
