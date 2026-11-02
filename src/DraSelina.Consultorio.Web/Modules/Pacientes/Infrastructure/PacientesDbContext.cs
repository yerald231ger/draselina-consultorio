using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Pacientes.Domain;
using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Pacientes.Infrastructure;

/// <summary>Los datos de Pacientes viven en el esquema <c>pacientes</c> y solo este contexto los toca (AD-2).</summary>
public sealed class PacientesDbContext(DbContextOptions<PacientesDbContext> options) : DbContext(options)
{
    public const string Esquema = "pacientes";

    /// <summary>
    /// La collation ICU no determinista del nombre normalizado: no distingue mayúsculas ni acentos (AD-18). Vive en el
    /// esquema del módulo y la crea su migración.
    /// </summary>
    public const string CollationSinMayusculasNiAcentos = "es_ci_ai";

    /// <summary>El índice único que decide la identidad del Paciente (AD-18, RN-26).</summary>
    public const string IndiceDeIdentidad = "ux_paciente_identidad";

    /// <summary>
    /// La única forma de configurar este contexto: la usan la aplicación, el MigrationService y las
    /// herramientas de EF Core, para que el historial de migraciones quede siempre en el esquema del módulo (AD-25).
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion) =>
        opciones.UseNpgsql(cadenaDeConexion, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Esquema));

    internal DbSet<Paciente> Pacientes => Set<Paciente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);

        modelBuilder.HasCollation(
            schema: Esquema,
            name: CollationSinMayusculasNiAcentos,
            locale: "es-u-ks-level1",
            provider: "icu",
            deterministic: false);

        // pacientes.paciente (modelo de datos, Parte II).
        modelBuilder.Entity<Paciente>(paciente =>
        {
            paciente.ToTable("paciente");
            paciente.HasKey(p => p.Id);
            paciente.Property(p => p.Id)
                .HasColumnName("id")
                .HasConversion(id => id.Valor, valor => new PatientId(valor))
                .ValueGeneratedNever();

            paciente.ComplexProperty(p => p.Nombre, nombre =>
            {
                nombre.Property(n => n.Original).HasColumnName("nombre_completo");
                // Su collation (es_ci_ai) la pone la migración con SQL explícito: Npgsql escribe una collation con
                // esquema como un solo identificador entre comillas ("pacientes.es_ci_ai") y PostgreSQL no la encuentra.
                nombre.Property(n => n.Normalizado).HasColumnName("nombre_normalizado");
                nombre.Property(n => n.Busqueda).HasColumnName("nombre_busqueda");
            });

            paciente.Property(p => p.Telefono)
                .HasColumnName("telefono")
                .HasConversion(t => t.Valor, valor => Telefono.Reconstituir(valor));

            paciente.Property(p => p.Correo)
                .HasColumnName("correo")
                .HasConversion(c => c!.Valor, valor => CorreoElectronico.Reconstituir(valor));

            paciente.Property(p => p.CreadoEn).HasColumnName("creado_en");
            paciente.Property(p => p.ActualizadoEn).HasColumnName("actualizado_en");

            // Para el tope semanal (AD-13). El índice de identidad y el de búsqueda por nombre van sobre columnas del
            // nombre y los crea la migración con SQL explícito.
            paciente.HasIndex(p => p.Telefono).HasDatabaseName("ix_paciente_telefono");
        });
    }
}
