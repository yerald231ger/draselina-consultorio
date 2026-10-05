using Arranque.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Arranque.Web;

public sealed class Nota
{
    public Guid Id { get; set; }
    public string Texto { get; set; } = "";
    public string Forma { get; set; } = "";
    public DateTime CreadaEn { get; set; }
}

public sealed class NotasDbContext(DbContextOptions<NotasDbContext> options) : DbContext(options)
{
    public DbSet<Nota> Notas => Set<Nota>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Nota>(b =>
        {
            b.ToTable("notas", "arranque");
            b.HasKey(x => x.Id);
            b.Property(x => x.Texto).HasMaxLength(20);
        });
    }
}

/// <summary>Handlers de Wolverine: uno por forma del comando. Escriben por EF Core.</summary>
public static class NotasHandler
{
    public static Task<Resultado> Handle(RegistrarNota c, NotasDbContext db, Diagnostico d, CancellationToken ct)
        => Guardar(c.Texto, "clase-set", db, d, ct);

    public static Task<Resultado> Handle(RegistrarNotaInit c, NotasDbContext db, Diagnostico d, CancellationToken ct)
        => Guardar(c.Texto, "record-init", db, d, ct);

    public static Task<Resultado> Handle(RegistrarNotaPosicional c, NotasDbContext db, Diagnostico d, CancellationToken ct)
        => Guardar(c.Texto, "record-posicional", db, d, ct);

    private static async Task<Resultado> Guardar(string? texto, string forma, NotasDbContext db, Diagnostico d, CancellationToken ct)
    {
        d.HandlerEjecutado();
        db.Notas.Add(new Nota { Id = Guid.CreateVersion7(), Texto = texto!, Forma = forma, CreadaEn = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }
}
