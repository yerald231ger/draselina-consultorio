using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Contenido;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Infrastructure;
using DraSelina.Consultorio.UI;
using DraSelina.Consultorio.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Registros, métricas, trazas y salud: un solo camino de telemetría (AD-21).
builder.AddServiceDefaults();

var cadena = BaseDeDatos.CadenaDeConexion(builder.Configuration);

// Cada módulo registra lo suyo: su contexto, su puerto de transacción y sus handlers (AD-10). La web nunca crea
// ni migra nada al arrancar (AD-25): eso lo hace el MigrationService, que termina antes de que la web arranque.
// Contenido no tiene base: solo su API (AD-2).
builder.Services
    .AddAgenda(cadena)
    .AddPacientes(cadena)
    .AddNotificaciones(cadena)
    .AddContenido();

// El reloj de la aplicación, y la entrega de eventos entre módulos (AD-3): un servicio en segundo plano entrega
// los eventos de cada outbox dado de alta aquí. Hoy solo publica Agenda.
builder.Services
    .AddSingleton<IClock, RelojDelSistema>()
    .AddEntregaDeEventos()
    .AddOutbox<AgendaDbContext>();

// Quién actúa (AD-22): un servicio por ámbito, el mismo como sí mismo (para que quien crea el ámbito, desde el host,
// declare Sistema) y como IActorActual (lo que piden los handlers). Lee el AuthenticationStateProvider que registra
// AddRazorComponents.
builder.Services
    .AddScoped<ActorDelHost>()
    .AddScoped<IActorActual>(servicios => servicios.GetRequiredService<ActorDelHost>());

builder.Services.AddRazorComponents();

// Los decoradores de los handlers (la validación de forma de AD-12 y el rechazo sin Administrador de AD-22) se
// aplican al construir el contenedor, sobre todo lo registrado hasta entonces. La lista está en
// DecoradoresDeHandlers.cs.
builder.Host.UseServiceProviderFactory(contexto => DecoradoresDeHandlers.Fabrica(contexto.HostingEnvironment));

var app = builder.Build();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();
app.MapAgenda();
app.MapDefaultEndpoints();

app.Run();
