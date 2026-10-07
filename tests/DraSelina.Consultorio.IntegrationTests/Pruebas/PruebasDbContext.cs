using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.IntegrationTests.Pruebas;

/// <summary>
/// Los tipos que solo existen para probar viven aquí y sus tablas en el esquema <c>pruebas</c>, nunca en un
/// esquema de producción. Para agregar uno: la clase en esta carpeta y su <c>DbSet</c> aquí; el arnés crea
/// la tabla al arrancar la corrida.
/// </summary>
public sealed class PruebasDbContext(DbContextOptions<PruebasDbContext> options) : DbContext(options)
{
    public const string Esquema = "pruebas";

    public DbSet<Apunte> Apuntes => Set<Apunte>();

    public DbSet<Anotacion> Anotaciones => Set<Anotacion>();

    public static DbContextOptions<PruebasDbContext> Opciones(string cadenaDeConexion) =>
        new DbContextOptionsBuilder<PruebasDbContext>().UseNpgsql(cadenaDeConexion).Options;

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Esquema);
}
