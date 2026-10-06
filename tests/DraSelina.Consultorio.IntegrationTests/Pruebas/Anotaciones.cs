using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// El puerto por el que los handlers de prueba llegan a la base, como en un módulo (AD-4): el handler no recibe
/// el contexto. Guardar es explícito y lo pide el handler.
/// </summary>
public interface IAnotaciones
{
    Task<bool> ExisteTextoAsync(string texto, CancellationToken cancelacion);

    Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion);

    Task<string?> LeerTextoAsync(Guid id, CancellationToken cancelacion);

    Task<bool> ExisteRastroAsync(Guid id, CancellationToken cancelacion);

    void Agregar(Anotacion anotacion);

    void Agregar(RastroDeAnotacion rastro);

    Task GuardarAsync(CancellationToken cancelacion);

    /// <summary>Solo para las pruebas: si el handler corre dentro de una transacción.</summary>
    bool HayTransaccionAbierta { get; }
}

/// <summary>El puerto sobre <see cref="PruebasDbContext"/>.</summary>
public sealed class Anotaciones(PruebasDbContext db) : IAnotaciones
{
    public Task<bool> ExisteTextoAsync(string texto, CancellationToken cancelacion) =>
        db.Anotaciones.AsNoTracking().AnyAsync(a => a.Texto == texto, cancelacion);

    public Task<bool> ExisteAsync(Guid id, CancellationToken cancelacion) =>
        db.Anotaciones.AsNoTracking().AnyAsync(a => a.Id == id, cancelacion);

    public Task<string?> LeerTextoAsync(Guid id, CancellationToken cancelacion) =>
        db.Anotaciones.AsNoTracking().Where(a => a.Id == id).Select(a => a.Texto).SingleOrDefaultAsync(cancelacion);

    public Task<bool> ExisteRastroAsync(Guid id, CancellationToken cancelacion) =>
        db.Rastros.AsNoTracking().AnyAsync(r => r.Id == id, cancelacion);

    public void Agregar(Anotacion anotacion) => db.Anotaciones.Add(anotacion);

    public void Agregar(RastroDeAnotacion rastro) => db.Rastros.Add(rastro);

    public Task GuardarAsync(CancellationToken cancelacion) => db.SaveChangesAsync(cancelacion);

    public bool HayTransaccionAbierta => db.Database.CurrentTransaction is not null;
}
