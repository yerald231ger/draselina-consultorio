using DraSelina.Consultorio.BuildingBlocks.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DraSelina.Consultorio.Web;

/// <summary>
/// Los decoradores de los handlers de casos de uso, en un solo lugar. Se aplican al construir el contenedor, sobre
/// la colección de servicios ya completa: alcanzan a todo handler registrado, lo haya registrado un módulo o el
/// arnés de pruebas después de <c>Program.cs</c>, y cada uno se aplica una sola vez. Lo activa <c>Program.cs</c>
/// con <c>builder.Host.UseServiceProviderFactory(...)</c>.
/// </summary>
public static class DecoradoresDeHandlers
{
    /// <summary>
    /// Aplica los decoradores, en orden: el primero queda más cerca del handler. Un decorador nuevo se agrega aquí.
    /// Con <c>TryDecorate</c>, que no lanza si todavía no hay ningún handler registrado.
    /// </summary>
    public static void Aplicar(IServiceCollection servicios)
    {
        // Validación de forma de todo comando, antes de su handler (AD-12).
        servicios.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidacionDeComandos<,>));
    }

    /// <summary>
    /// La fábrica del contenedor de la web: la de siempre, con la misma validación de ámbitos y de construcción que
    /// la web usa sin ella (solo en Development), más los decoradores.
    /// </summary>
    public static IServiceProviderFactory<IServiceCollection> Fabrica(IHostEnvironment entorno) =>
        new FabricaConDecoradores(new ServiceProviderOptions
        {
            ValidateScopes = entorno.IsDevelopment(),
            ValidateOnBuild = entorno.IsDevelopment(),
        });

    private sealed class FabricaConDecoradores(ServiceProviderOptions opciones) : IServiceProviderFactory<IServiceCollection>
    {
        private readonly DefaultServiceProviderFactory _porDefecto = new(opciones);

        public IServiceCollection CreateBuilder(IServiceCollection services) => _porDefecto.CreateBuilder(services);

        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
        {
            Aplicar(containerBuilder);
            return _porDefecto.CreateServiceProvider(containerBuilder);
        }
    }
}
