using System.Reflection;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>Los casos de uso de la aplicación y de los tipos de prueba, y la interfaz del handler de cada uno.</summary>
public static class CasosDeUso
{
    /// <summary>
    /// Casos de uso que no tienen handler a propósito. <see cref="ComandoSinHandler"/> existe para probar qué pasa
    /// al pedir el handler de un comando que no lo tiene (<c>CasoDeUsoTests</c>). Solo tipos de prueba.
    /// </summary>
    public static readonly Type[] SinHandlerAProposito = [typeof(ComandoSinHandler)];

    /// <summary>Los de Web y los de prueba, sin los que no tienen handler a propósito.</summary>
    public static IReadOnlyList<Type> DeLaWebYDePrueba() =>
        [.. En(typeof(Program).Assembly, typeof(Arnes).Assembly).Except(SinHandlerAProposito)];

    /// <summary>Los tipos concretos, no genéricos, que implementan <see cref="ICommand{T}"/> o <see cref="IQuery{T}"/>.</summary>
    public static IEnumerable<Type> En(params Assembly[] ensamblados) =>
        ensamblados
            .SelectMany(e => e.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } && Handlers(t).Any());

    /// <summary>La interfaz del handler que le toca a cada interfaz de caso de uso que el tipo implementa.</summary>
    public static IEnumerable<Type> Handlers(Type caso) =>
        caso.GetInterfaces()
            .Where(i => i.IsConstructedGenericType)
            .Select(i => i.GetGenericTypeDefinition() switch
            {
                var d when d == typeof(ICommand<>) => typeof(ICommandHandler<,>).MakeGenericType(caso, i.GenericTypeArguments[0]),
                var d when d == typeof(IQuery<>) => typeof(IQueryHandler<,>).MakeGenericType(caso, i.GenericTypeArguments[0]),
                _ => null,
            })
            .OfType<Type>();

    /// <summary>Las interfaces de handler de comando de los casos de uso.</summary>
    public static IEnumerable<Type> HandlersDeComando(IEnumerable<Type> casos) =>
        casos.SelectMany(Handlers)
            .Where(h => h.GetGenericTypeDefinition() == typeof(ICommandHandler<,>));
}
