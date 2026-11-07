using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Contenido;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Infrastructure;
using DraSelina.Consultorio.UI;
using DraSelina.Consultorio.UI.Pages;
using DraSelina.Consultorio.UI.Pages.Reserva;
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

// Protección de las páginas de reserva (FR-7, CAP-5), sin scripts ni servicios de terceros: el límite de frecuencia
// por IP (LimiteDeFrecuencia.cs, valores en configuración) y la verificación anti-bots de Tus datos.
builder.Services
    .AddLimiteDeFrecuencia(builder.Configuration)
    .AddSingleton<VerificacionAntiBots>();

// Los decoradores de los handlers (la validación de forma de AD-12 y el rechazo sin Administrador de AD-22) se
// aplican al construir el contenedor, sobre todo lo registrado hasta entonces. La lista está en
// DecoradoresDeHandlers.cs.
builder.Host.UseServiceProviderFactory(contexto => DecoradoresDeHandlers.Fabrica(contexto.HostingEnvironment));

var app = builder.Build();

// Todo lo que cuelga del Enlace de gestión (/c/<token> y sus subrutas, el .ics incluido) lleva el token en la
// dirección: ningún buscador lo indexa y ni el navegador ni un intermediario lo guardan (CAP-21, AD-19). Va antes del
// límite de frecuencia para que también la respuesta 429 lo lleve.
app.Use((contexto, siguiente) =>
{
    if (contexto.Request.Path.StartsWithSegments("/c"))
    {
        contexto.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        contexto.Response.Headers.CacheControl = "no-store";
    }

    return siguiente(contexto);
});

// Solo lo aplican los endpoints que piden su política: las páginas de reserva, Tu cita y el archivo de calendario.
app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();
app.MapAgenda(DemasiadosIntentos.Politica);
app.MapDefaultEndpoints();

app.Run();
