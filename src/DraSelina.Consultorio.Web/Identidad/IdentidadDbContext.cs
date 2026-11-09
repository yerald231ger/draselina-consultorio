using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.Web.Identidad;

/// <summary>
/// La cuenta del Administrador (ASP.NET Core Identity) vive en el esquema <c>identidad</c>, del host, con su propio
/// historial de migraciones (AD-2, AD-25). Solo lo aplica el MigrationService; la web nunca migra. Sin roles en uso: el
/// único usuario es el Administrador (AD-22).
/// </summary>
public sealed class IdentidadDbContext(DbContextOptions<IdentidadDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public const string Esquema = "identidad";

    /// <summary>
    /// La versión del esquema de Identity, fija: sin ella, el modelo dependería de las opciones registradas en cada
    /// proceso (la web, el MigrationService, las herramientas de EF Core) y la migración podría no coincidir. La 2 no
    /// trae la tabla de llaves de acceso (passkeys), que no se usan.
    /// </summary>
    public static readonly Version VersionDelEsquema = IdentitySchemaVersions.Version2;

    protected override Version SchemaVersion => VersionDelEsquema;

    /// <summary>
    /// La única forma de configurar este contexto: la usan la web, el MigrationService y las herramientas de EF Core,
    /// para que el historial de migraciones quede siempre en el esquema <c>identidad</c> (AD-25).
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion) =>
        opciones.UseNpgsql(cadenaDeConexion, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Esquema));

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Esquema);
    }
}

/// <summary>
/// Solo para <c>dotnet ef migrations add</c>: crea el contexto sin arrancar la aplicación, pero con las mismas opciones de
/// Identity que la web y el MigrationService (<see cref="Registro.AddCuentasDeIdentidad"/>): el modelo de Identity
/// depende de ellas y la migración debe coincidir con el de ejecución. Generar una migración no abre conexión, así que la
/// cadena no lleva contraseña ni apunta a una base real.
/// </summary>
public sealed class IdentidadDbContextFactory : IDesignTimeDbContextFactory<IdentidadDbContext>
{
    public IdentidadDbContext CreateDbContext(string[] args)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddCuentasDeIdentidad("Host=localhost;Database=consultorio");
        return servicios.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IdentidadDbContext>();
    }
}
