using DraSelina.Consultorio.IntegrationTests;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Features;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;

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

    /// <summary>La clave del Enlace de gestión que reciben las webs de las pruebas (AD-19).</summary>
    public const string ClaveDeEnlace = "clave-de-enlace-de-las-pruebas-de-integracion";

    /// <summary>La URL base del Enlace de gestión que reciben las webs de las pruebas.</summary>
    public const string UrlBaseDeEnlace = "https://citas.prueba.mx";

    private PostgreSqlContainer? _contenedor;
    private string? _cadenaDeConexion;

    /// <summary>La base de la corrida, ya migrada. La comparten todas las pruebas: cada una crea sus datos.</summary>
    public string CadenaDeConexion =>
        _cadenaDeConexion ?? throw new InvalidOperationException("El arnés todavía no arranca.");

    public async ValueTask InitializeAsync()
    {
        try
        {
            // Muchas pruebas crean su propia base y cada una deja conexiones en su pool: las 100 de fábrica no alcanzan.
            _contenedor = new PostgreSqlBuilder(Imagen)
                .WithDatabase(BaseDeDatos.Nombre)
                .WithCommand("-c", "max_connections=400")
                .Build();
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
    public PruebasDbContext CrearContextoDePruebas() => CrearContextoDePruebas(CadenaDeConexion);

    /// <summary>Un contexto nuevo sobre el esquema <c>pruebas</c> de otra base, creada con <see cref="CrearBaseMigradaAsync"/>.</summary>
    public PruebasDbContext CrearContextoDePruebas(string cadenaDeConexion) => new(PruebasDbContext.Opciones(cadenaDeConexion));

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
    /// Crea una base propia, igual a la de la corrida: migrada por el <c>MigrationService</c> y con el esquema
    /// <c>pruebas</c>. Es para la prueba que necesita que nadie más toque sus datos: las webs de las demás pruebas
    /// entregan los eventos de la base de la corrida, cada una con su reloj.
    /// </summary>
    public async Task<string> CrearBaseMigradaAsync(CancellationToken cancelacion = default)
    {
        var cadena = await CrearBaseVaciaAsync(cancelacion);

        await MigrationService.EjecutarAsync(cadena, cancelacion);
        await CrearEsquemaDePruebasAsync(cadena);

        return cadena;
    }

    /// <summary>
    /// Arranca la web en memoria, con su <c>Program</c> real, contra la base de <paramref name="cadenaDeConexion"/>.
    /// Quien la pide la desecha: <c>await using var web = arnes.ArrancarWeb(cadena);</c>
    /// </summary>
    public WebApplicationFactory<Program> ArrancarWeb(string cadenaDeConexion) =>
        new Web(cadenaDeConexion, reloj: null);

    /// <summary>
    /// Como <see cref="ArrancarWeb(string)"/>, pero con el servicio de envíos programados en segundo plano, que las
    /// demás webs de las pruebas no llevan: para la prueba que comprueba que da sus vueltas por su cuenta.
    /// </summary>
    public WebApplicationFactory<Program> ArrancarWebConEnviosProgramados(string cadenaDeConexion) =>
        new Web(cadenaDeConexion, reloj: null, conEnviosProgramados: true);

    /// <summary>
    /// La web en memoria contra la base de la corrida, sin la clave del Enlace de gestión en su configuración: para
    /// comprobar que así no arranca.
    /// </summary>
    public WebApplicationFactory<Program> ArrancarWebSinClaveDeEnlace() =>
        new Web(CadenaDeConexion, reloj: null, conClave: false);

    /// <summary>
    /// Arranca la web en memoria sin los servicios en segundo plano (la entrega de eventos y los envíos programados)
    /// y con el reloj de la prueba. La prueba da cada vuelta cuando quiere, con
    /// <see cref="DarUnaVueltaDeEntregaAsync"/> y <see cref="DarUnaVueltaDeEnviosProgramadosAsync"/>, y mueve el
    /// tiempo con <paramref name="reloj"/>. Se usa contra una base propia (<see cref="CrearBaseMigradaAsync"/>).
    /// </summary>
    public WebApplicationFactory<Program> ArrancarWebSinEntregaAutomatica(
        string cadenaDeConexion, RelojDePrueba reloj, TimeSpan? limitePorHandler = null) =>
        new Web(cadenaDeConexion, reloj, limitePorHandler);

    /// <summary>
    /// Da una vuelta de entrega de eventos en esa web, la misma que da su servicio en segundo plano. Devuelve
    /// cuántos eventos intentó entregar.
    /// </summary>
    public static Task<int> DarUnaVueltaDeEntregaAsync(WebApplicationFactory<Program> web, CancellationToken cancelacion = default) =>
        web.Services.GetRequiredService<EntregaDeEventos>().DarUnaVueltaAsync(cancelacion);

    /// <summary>
    /// Da una vuelta de envíos programados de Notificaciones en esa web, la misma que da su servicio en segundo plano.
    /// Devuelve cuántos Recordatorios intentó enviar.
    /// </summary>
    public static Task<int> DarUnaVueltaDeEnviosProgramadosAsync(
        WebApplicationFactory<Program> web, CancellationToken cancelacion = default) =>
        web.Services.GetRequiredService<EnviosProgramados>().DarUnaVueltaAsync(cancelacion);

    /// <summary>Lo que esa web ha registrado (avisos, errores...) desde que arrancó.</summary>
    public static IReadOnlyCollection<RegistroEscrito> Registros(WebApplicationFactory<Program> web) =>
        web.Services.GetRequiredService<RegistrosDePrueba>().Escritos;

    /// <summary>El canal de correo de esa web: lo que ha enviado, y si está caído.</summary>
    public static CanalDePrueba Canal(WebApplicationFactory<Program> web) =>
        web.Services.GetRequiredService<CanalDePrueba>();

    /// <summary>Arranca la web en memoria contra la base migrada de la corrida.</summary>
    public WebApplicationFactory<Program> ArrancarWeb() => ArrancarWeb(CadenaDeConexion);

    // Una sola fábrica por llamada, que desecha quien la pidió.
    // Con reloj de prueba, la web no entrega eventos por su cuenta: las vueltas las da la prueba. Los envíos
    // programados no corren por su cuenta salvo en ArrancarWebConEnviosProgramados.
    private sealed class Web(
        string cadenaDeConexion,
        RelojDePrueba? reloj,
        TimeSpan? limitePorHandler = null,
        bool conClave = true,
        bool conEnviosProgramados = false) : WebApplicationFactory<Program>
    {
        private static void QuitarServicio(IServiceCollection servicios, Type tipo) =>
            servicios.Remove(servicios.Single(s =>
                s.ServiceType == typeof(IHostedService) && s.ImplementationType == tipo));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting($"ConnectionStrings:{BaseDeDatos.Nombre}", cadenaDeConexion);
            builder.UseSetting("EnlaceDeGestion:UrlBase", UrlBaseDeEnlace);
            if (conClave)
            {
                builder.UseSetting("EnlaceDeGestion:Clave", ClaveDeEnlace);
            }

            builder.ConfigureServices(servicios =>
            {
                // Los tipos de prueba entran a la web en memoria como entra un módulo: con su registro (AddPruebas)
                // y, como publican eventos, con el alta de su outbox, igual que Program.cs da de alta el de Agenda.
                servicios
                    .AddPruebas(cadenaDeConexion)
                    .AddOutbox<PruebasDbContext>();

                // El desplazamiento de un recorrido manual (appsettings.Development.local.json) no llega a las pruebas:
                // se aplica después de cada recarga del archivo.
                servicios.PostConfigure<OpcionesDeReloj>(opciones => opciones.Desplazamiento = null);

                // Ninguna web de las pruebas envía correos de verdad: el canal SMTP se reemplaza por uno que guarda los
                // envíos y puede fallar a pedido (Arnes.Canal).
                servicios.RemoveAll<INotificationChannel>();
                servicios
                    .AddSingleton<CanalDePrueba>()
                    .AddSingleton<INotificationChannel>(proveedor => proveedor.GetRequiredService<CanalDePrueba>());

                servicios
                    .AddSingleton<RegistrosDePrueba>()
                    .AddSingleton<ILoggerProvider>(proveedor => proveedor.GetRequiredService<RegistrosDePrueba>());

                if (reloj is not null)
                {
                    servicios.AddSingleton<IClock>(reloj);
                    QuitarServicio(servicios, typeof(ServicioDeEntregaDeEventos));
                }

                // Los envíos programados solo corren en la web que los pide: cada uno abre su propio pool de
                // conexiones y, en todas las webs de la corrida, agotan las del contenedor.
                if (!conEnviosProgramados)
                {
                    QuitarServicio(servicios, typeof(ServicioDeEnviosProgramados));
                }

                if (limitePorHandler is { } limite)
                {
                    servicios.AddSingleton(new OpcionesDeEntrega(limite));
                }
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
