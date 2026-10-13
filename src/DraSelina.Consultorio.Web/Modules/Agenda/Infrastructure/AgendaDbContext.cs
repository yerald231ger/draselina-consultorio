using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>
/// Los datos de Agenda viven en el esquema <c>agenda</c> y solo este contexto los toca (AD-2).
/// Hoy solo tiene su outbox; las demás tablas llegan con las historias del módulo.
/// </summary>
public sealed class AgendaDbContext(DbContextOptions<AgendaDbContext> options) : DbContext(options)
{
    public const string Esquema = "agenda";

    /// <summary>
    /// La única forma de configurar este contexto: la usan la aplicación, el MigrationService y las
    /// herramientas de EF Core, para que el historial de migraciones quede siempre en el esquema del módulo (AD-25).
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion) =>
        opciones.UseNpgsql(cadenaDeConexion, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Esquema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);

        // agenda.outbox: los eventos de integración que Agenda publica (AD-2, AD-3).
        modelBuilder.Entity<FilaDeOutbox>(fila =>
        {
            fila.ToTable(FilaDeOutbox.Tabla);
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
