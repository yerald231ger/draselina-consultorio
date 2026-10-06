using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas.FueraDeModulo;

/// <summary>Un comando cuyo handler vive en un namespace que no es de ningún módulo dado de alta.</summary>
public sealed class ComandoFueraDeModulo : ICommand<int>;

/// <summary>
/// Su handler. El nombre no termina en <c>Handler</c> a propósito: Wolverine no lo descubre solo, y así no estorba
/// a las demás pruebas. La prueba que lo usa lo incluye de forma explícita.
/// </summary>
public static class DespachoFueraDeModulo
{
    public static Resultado<int> Handle(ComandoFueraDeModulo comando) => 1;
}
