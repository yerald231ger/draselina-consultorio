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
public static class LeerAnotacionHandler
{
    public static async Task<Resultado<AnotacionLeida>> Handle(LeerAnotacion consulta, IAnotaciones anotaciones, CancellationToken cancelacion)
    {
        var texto = await anotaciones.LeerTextoAsync(consulta.Id, cancelacion);

        return texto is null
            ? Resultado.Fallo(CodigosDeAnotacion.NoExiste).Como<AnotacionLeida>()
            : new AnotacionLeida(texto, anotaciones.HayTransaccionAbierta);
    }
}

/// <summary>Una consulta mal escrita: su handler agrega una entidad. No guarda, y nadie guarda por ella.</summary>
public sealed class LeerAgregando : IQuery<Guid>
{
    public Guid Id { get; set; }
}

public static class LeerAgregandoHandler
{
    public static Resultado<Guid> Handle(LeerAgregando consulta, IAnotaciones anotaciones)
    {
        anotaciones.Agregar(new Anotacion { Id = consulta.Id, Texto = $"agregada por una consulta {consulta.Id:N}" });
        return consulta.Id;
    }
}
