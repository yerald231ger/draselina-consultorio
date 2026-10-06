using DraSelina.Consultorio.IntegrationTests;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Wolverine;

// Un solo arnés para toda la corrida: xUnit lo crea antes de la primera prueba que lo pide y lo apaga al final.
[assembly: AssemblyFixture(typeof(Arnes))]

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// PostgreSQL real para las pruebas de integración. Una vez por corrida levanta un contenedor, migra la base
/// ejecutando el <c>MigrationService</c> y crea el esquema <c>pruebas</c> con las tablas de los tipos de prueba.
/// Una prueba lo recibe por su constructor.
/// </summary>
public sealed class Arnes : IAsyncLifetime
{
    /// <summary>La misma versión mayor de PostgreSQL que levanta el AppHost.</summary>
    public const string Imagen = "postgres:18.3";

    private PostgreSqlContainer? _contenedor;
    private string? _cadenaDeConexion;

    /// <summary>La base de la corrida, ya migrada. La comparten todas las pruebas: cada una crea sus datos.</summary>
    public string CadenaDeConexion =>
        _cadenaDeConexion ?? throw new InvalidOperationException("El arnés todavía no arranca.");

    public async ValueTask InitializeAsync()
    {
        try
        {
            _contenedor = new PostgreSqlBuilder(Imagen).WithDatabase(BaseDeDatos.Nombre).Build();
            await _contenedor.StartAsync();
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                "No se pudo levantar PostgreSQL en un contenedor. Las pruebas de integración necesitan Docker: lo "
                + "primero es comprobar que esté en ejecución; si lo está, la causa está en el detalle. "
                + $"Detalle: {error.Message}", error);
        }

        var cadena = _contenedor.GetConnectionString();

        await MigrationService.EjecutarAsync(cadena);
        await CrearEsquemaDePruebasAsync(cadena);

        _cadenaDeConexion = cadena;
    }

    public async ValueTask DisposeAsync()
    {
        if (_contenedor is not null)
        {
            await _contenedor.DisposeAsync();
        }
    }

    /// <summary>Un contexto nuevo sobre el esquema <c>pruebas</c> de la base de la corrida.</summary>
    public PruebasDbContext CrearContextoDePruebas() => new(PruebasDbContext.Opciones(CadenaDeConexion));

    /// <summary>
    /// Crea una base vacía, sin migrar, en el mismo servidor y devuelve su cadena de conexión.
    /// Sirve para comprobar qué hace algo al arrancar contra una base donde no hay nada.
    /// </summary>
    public async Task<string> CrearBaseVaciaAsync(CancellationToken cancelacion = default)
    {
        var nombre = $"vacia_{Guid.NewGuid():N}";

        await using var conexion = new NpgsqlConnection(CadenaDeConexion);
        await conexion.OpenAsync(cancelacion);
        await using var comando = new NpgsqlCommand($"create database \"{nombre}\"", conexion);
        await comando.ExecuteNonQueryAsync(cancelacion);

        return new NpgsqlConnectionStringBuilder(CadenaDeConexion) { Database = nombre }.ConnectionString;
    }

    /// <summary>
    /// Arranca la web en memoria, con su <c>Program</c> real, contra la base de <paramref name="cadenaDeConexion"/>.
    /// Quien la pide la desecha: <c>await using var web = arnes.ArrancarWeb(cadena);</c>
    /// </summary>
    public WebApplicationFactory<Program> ArrancarWeb(string cadenaDeConexion) =>
        new Web(cadenaDeConexion);

    /// <summary>Arranca la web en memoria contra la base migrada de la corrida.</summary>
    public WebApplicationFactory<Program> ArrancarWeb() => ArrancarWeb(CadenaDeConexion);

    // Una sola fábrica por llamada, que desecha quien la pidió.
    private sealed class Web(string cadenaDeConexion) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting($"ConnectionStrings:{BaseDeDatos.Nombre}", cadenaDeConexion);

            // Los tipos de prueba entran a la web en memoria como entra un módulo: su contexto, su puerto y su
            // renglón en la política de transacción (AddPruebas), y sus handlers descubiertos por Wolverine. La
            // política no se toca aquí: es la que fijó el host.
            builder.ConfigureServices(servicios =>
            {
                servicios.AddPruebas(cadenaDeConexion);
                servicios.ConfigureWolverine(opciones => opciones.Discovery.IncludeAssembly(typeof(Arnes).Assembly));
            });
        }
    }

    // Después de migrar: el esquema de los tipos de prueba y sus tablas. No pasa por migraciones ni toca
    // los esquemas de producción.
    private static async Task CrearEsquemaDePruebasAsync(string cadena)
    {
        await using var contexto = new PruebasDbContext(PruebasDbContext.Opciones(cadena));
        await contexto.Database.ExecuteSqlRawAsync($"create schema \"{PruebasDbContext.Esquema}\"");
        await contexto.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }
}
