using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>Consulta de prueba: el texto de una <see cref="Anotacion"/>.</summary>
public sealed class LeerAnotacion : IQuery<AnotacionLeida>
{
    public Guid Id { get; set; }
}

/// <summary>Lo leído, y si el handler de la consulta vio una transacción abierta (no debe).</summary>
public sealed record AnotacionLeida(string Texto, bool HabiaTransaccion);

/// <summary>Un handler por consulta: lee sin seguimiento, proyecta y nunca guarda. Corre sin transacción.</summary>
public sealed class LeerAnotacionHandler(IAnotaciones anotaciones) : IQueryHandler<LeerAnotacion, AnotacionLeida>
{
    public async Task<Resultado<AnotacionLeida>> HandleAsync(LeerAnotacion consulta, CancellationToken cancellationToken)
    {
        var texto = await anotaciones.LeerTextoAsync(consulta.Id, cancellationToken);

        return texto is null
            ? Resultado.Fallo(CodigosDeAnotacion.NoExiste).Como<AnotacionLeida>()
            : new AnotacionLeida(texto, anotaciones.HayTransaccionAbierta);
    }
}
