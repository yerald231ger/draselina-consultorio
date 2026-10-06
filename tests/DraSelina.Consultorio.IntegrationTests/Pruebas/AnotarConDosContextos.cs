using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// Comando de prueba cuyo handler alcanza dos contextos: el de su módulo, por su puerto, y el de otro módulo, por
/// un servicio. Es la forma de un handler de Agenda que llama a <c>IPacientesApi</c> (AD-13, AD-28). Devuelve si
/// el otro contexto quedó fuera de la transacción.
/// </summary>
public sealed class AnotarConDosContextos : ICommand<bool>
{
    public Guid Id { get; set; }

    public string Texto { get; set; } = "";
}

/// <summary>Hace de la API de otro módulo: usa un contexto de producción que no es el del handler.</summary>
public sealed class ServicioDeOtroModulo(PacientesDbContext db)
{
    /// <summary>Usa su contexto y devuelve si lo hizo fuera de toda transacción.</summary>
    public async Task<bool> UsarSuContextoAsync(CancellationToken cancelacion)
    {
        await db.Database.ExecuteSqlRawAsync("select 1", cancelacion);
        return db.Database.CurrentTransaction is null;
    }
}

public static class AnotarConDosContextosHandler
{
    public static async Task<Resultado<bool>> Handle(
        AnotarConDosContextos comando, IAnotaciones anotaciones, ServicioDeOtroModulo otroModulo, IMessageBus bus, CancellationToken cancelacion)
    {
        var elOtroQuedoFuera = await otroModulo.UsarSuContextoAsync(cancelacion);

        anotaciones.Agregar(new Anotacion { Id = comando.Id, Texto = comando.Texto });
        await anotaciones.GuardarAsync(cancelacion);
        await bus.ScheduleAsync(new AnotacionHecha(comando.Id), TimeSpan.FromHours(1));

        return anotaciones.HayTransaccionAbierta && elOtroQuedoFuera;
    }
}
