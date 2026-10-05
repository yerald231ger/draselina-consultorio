using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Pacientes.Infrastructure;

/// <summary>
/// Los datos de Pacientes viven en el esquema <c>pacientes</c> y solo este contexto los toca (AD-2).
/// Todavía no tiene tablas: llegan con las historias del módulo.
/// </summary>
public sealed class PacientesDbContext(DbContextOptions<PacientesDbContext> options) : DbContext(options)
{
    public const string Esquema = "pacientes";

    /// <summary>
    /// La única forma de configurar este contexto: la usan la aplicación, el MigrationService y las
    /// herramientas de EF Core, para que el historial de migraciones quede siempre en el esquema del módulo (AD-25).
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion) =>
        opciones.UseNpgsql(cadenaDeConexion, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Esquema));

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.HasDefaultSchema(Esquema);
}
