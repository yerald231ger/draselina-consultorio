using System.Diagnostics;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Contenido;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Infrastructure;
using DraSelina.Consultorio.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

// Aplica todas las migraciones y termina. Sale con 0 si todo quedó aplicado y con 1 si algo falló:
// el AppHost solo arranca la web cuando este proceso termina con 0.
var builder = Host.CreateApplicationBuilder(args);

// Registros y trazas por el mismo camino que la web (AD-21).
builder.AddServiceDefaults();

// Cada paso lo registra este programa. De EF Core solo pasan los avisos, y sus comandos SQL no se registran:
// sobre una base vacía EF Core pregunta por la tabla de historial antes de crearla y registra ese fallo
// esperado como error. Un fallo real llega como excepción y se registra abajo, con su causa.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);

// Si falta la cadena no se registra nada de la base: el host se construye igual y la falta se reporta
// abajo, por el mismo camino que cualquier otro fallo (registro crítico y código 1).
if (builder.Configuration.GetConnectionString(BaseDeDatos.Nombre) is { } cadena)
{
    builder.Services
        .AddAgenda(cadena)
        .AddPacientes(cadena)
        .AddNotificaciones(cadena);

    // Lo que los handlers y las API de los módulos piden al host. Aquí no se usan, pero en Development el host valida
    // al construirse que todo lo registrado se pueda crear.
    builder.Services
        .AddContenido()
        .AddSingleton<IClock, RelojDelSistema>();
}

// El host se construye pero no se arranca: solo hacen falta sus servicios.
using var host = builder.Build();

var registro = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DraSelina.Consultorio.MigrationService");

// Sin arrancar el host nadie crea el exportador de trazas; pedirlo aquí lo crea.
host.Services.GetService<TracerProvider>();
using var fuente = new ActivitySource(builder.Environment.ApplicationName);

try
{
    using var actividad = fuente.StartActivity("Migraciones");

    // Lanza si falta la cadena de conexión.
    BaseDeDatos.CadenaDeConexion(host.Services.GetRequiredService<IConfiguration>());

    await using var ambito = host.Services.CreateAsyncScope();
    await Migrar<AgendaDbContext>(AgendaDbContext.Esquema);
    await Migrar<PacientesDbContext>(PacientesDbContext.Esquema);
    await Migrar<NotificacionesDbContext>(NotificacionesDbContext.Esquema);

    registro.LogInformation("Migraciones terminadas");
    return 0;

    async Task Migrar<TContexto>(string esquema) where TContexto : DbContext
    {
        var contexto = ambito.ServiceProvider.GetRequiredService<TContexto>();
        var pendientes = (await contexto.Database.GetPendingMigrationsAsync()).ToList();

        registro.LogInformation("Esquema {Esquema}: {Pendientes} migraciones pendientes", esquema, pendientes.Count);
        await contexto.Database.MigrateAsync();
        registro.LogInformation("Esquema {Esquema}: migraciones aplicadas", esquema);
    }
}
catch (Exception error)
{
    registro.LogCritical(error, "Las migraciones fallaron; la web no debe arrancar");
    return 1;
}
