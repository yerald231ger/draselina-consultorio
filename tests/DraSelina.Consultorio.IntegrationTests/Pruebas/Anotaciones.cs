using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// El puerto por el que los handlers de prueba llegan a la base, como en un módulo (AD-4): el handler no recibe
/// el contexto. Guardar es explícito y lo pide el handler.
/// </summary>
public interface IAnotaciones
{
    Task<bool> ExisteTextoAsync(string texto, CancellationToken cancellationToken);

    Task<string?> LeerTextoAsync(Guid id, CancellationToken cancellationToken);

    void Agregar(Anotacion anotacion);

    Task GuardarAsync(CancellationToken cancellationToken);

    /// <summary>Solo para las pruebas: si quien pregunta corre con una transacción abierta.</summary>
    bool HayTransaccionAbierta { get; }
}

/// <summary>El puerto sobre <see cref="PruebasDbContext"/>.</summary>
public sealed class Anotaciones(PruebasDbContext db) : IAnotaciones
{
    public Task<bool> ExisteTextoAsync(string texto, CancellationToken cancellationToken) =>
        db.Anotaciones.AsNoTracking().AnyAsync(a => a.Texto == texto, cancellationToken);

    public Task<string?> LeerTextoAsync(Guid id, CancellationToken cancellationToken) =>
        db.Anotaciones.AsNoTracking().Where(a => a.Id == id).Select(a => a.Texto).SingleOrDefaultAsync(cancellationToken);

    public void Agregar(Anotacion anotacion) => db.Anotaciones.Add(anotacion);

    public Task GuardarAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public bool HayTransaccionAbierta => db.Database.CurrentTransaction is not null;
}
