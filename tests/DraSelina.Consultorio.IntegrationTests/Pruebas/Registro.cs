using DraSelina.Consultorio.BuildingBlocks.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// Da de alta los tipos de prueba como se da de alta un módulo: su contexto, su puerto de transacción, sus
/// puertos y sus handlers, cada handler por su interfaz. <see cref="ComandoSinHandler"/> no tiene handler a propósito.
/// </summary>
public static class Registro
{
    public static IServiceCollection AddPruebas(this IServiceCollection servicios, string cadenaDeConexion) =>
        servicios
            .AddDbContext<PruebasDbContext>(opciones => opciones.UseNpgsql(cadenaDeConexion))
            .AddScoped<ITransaccionDePruebas, TransaccionDePruebas>()
            .AddScoped<IAnotaciones, Anotaciones>()
            .AddScoped<ICommandHandler<Anotar, Guid>, AnotarHandler>()
            .AddScoped<IQueryHandler<LeerAnotacion, AnotacionLeida>, LeerAnotacionHandler>();
}
