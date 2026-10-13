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

    public DbSet<EntregaRecibida> Entregas => Set<EntregaRecibida>();

    public static DbContextOptions<PruebasDbContext> Opciones(string cadenaDeConexion) =>
        new DbContextOptionsBuilder<PruebasDbContext>().UseNpgsql(cadenaDeConexion).Options;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);

        // pruebas.outbox: el outbox de los tipos de prueba, con las columnas de agenda.outbox.
        modelBuilder.Entity<FilaDeOutboxDePruebas>(fila =>
        {
            fila.ToTable("outbox");
            fila.HasKey(f => f.Id);
            fila.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
            fila.Property(f => f.Tipo).HasColumnName("tipo");
            fila.Property(f => f.Contenido).HasColumnName("contenido").HasColumnType("jsonb");
            fila.Property(f => f.OcurridoEn).HasColumnName("ocurrido_en");
            fila.Property(f => f.ClaveDeOrden).HasColumnName("clave_de_orden");
            fila.Property(f => f.Estado).HasColumnName("estado");
            fila.Property(f => f.Intentos).HasColumnName("intentos");
            fila.Property(f => f.ProximoIntentoEn).HasColumnName("proximo_intento_en");
            fila.Property(f => f.EntregadoEn).HasColumnName("entregado_en");
        });
    }
}
