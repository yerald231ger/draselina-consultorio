using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Contenido;
using DraSelina.Consultorio.Kernel;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Infrastructure;
using DraSelina.Consultorio.UI;
using DraSelina.Consultorio.UI.Pages;
using DraSelina.Consultorio.UI.Pages.Reserva;
using DraSelina.Consultorio.Web;
using DraSelina.Consultorio.Web.Identidad;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

// La cuenta del Administrador y su sesión (AD-22): Identity en el host, con su esquema identidad, segundo factor por
// app autenticadora y sin registro público (Identidad/Registro.cs). La cuenta la siembra el MigrationService.
builder.Services
    .AddIdentidad(cadena)
    .AddCascadingAuthenticationState();

// El reloj de la aplicación, y la entrega de eventos entre módulos (AD-3): un servicio en segundo plano entrega
// los eventos de cada outbox dado de alta aquí. Hoy solo publica Agenda.
// El reloj solo se desplaza en Development: Reloj:Desplazamiento en appsettings.Development.local.json, que no va al
// repositorio (.gitignore) y se recarga al guardarlo. En otro entorno un desplazamiento distinto de cero impide arrancar.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(OpcionesDeReloj.ArchivoLocal, optional: true, reloadOnChange: true);
}

OpcionesDeReloj.ExigirSoloEnDevelopment(builder.Configuration, builder.Environment);
builder.Services.Configure<OpcionesDeReloj>(builder.Configuration.GetSection(OpcionesDeReloj.Seccion));
var relojDesplazable = builder.Environment.IsDevelopment();

builder.Services
    .AddSingleton<IClock>(servicios => new RelojDelSistema(
        relojDesplazable ? servicios.GetRequiredService<IOptionsMonitor<OpcionesDeReloj>>() : null))
    .AddEntregaDeEventos()
    .AddOutbox<AgendaDbContext>();

// Quién actúa (AD-22): un servicio por ámbito, el mismo como sí mismo (para que quien crea el ámbito, desde el host,
// declare Sistema) y como IActorActual (lo que piden los handlers). Lee el AuthenticationStateProvider que registra
// AddRazorComponents.
builder.Services
    .AddScoped<ActorDelHost>()
    .AddScoped<IActorActual>(servicios => servicios.GetRequiredService<ActorDelHost>());

// El sitio y la reserva en SSR estático; la administración, en Interactive Server, salvo inicio de sesión y segundo
// factor (AD-11). Solo las páginas que lo piden con @rendermode abren un circuito.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// La Agenda vuelve a consultar cada 30 segundos mientras su circuito está abierto (entrada 5.2).
builder.Services.AddSingleton(DraSelina.Consultorio.UI.Pages.Administracion.RefrescoDeLaAgenda.PorOmision);

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
// dirección: ningún buscador lo indexa y ni el navegador ni un intermediario lo guardan (CAP-21, AD-19). La
// administración (/admin y sus subrutas, inicio de sesión incluido) tampoco se indexa ni se guarda (CAP-21). Va antes
// del límite de frecuencia y de la autorización para que también la respuesta 429 y la redirección a /admin/entrar lo
// lleven.
app.Use((contexto, siguiente) =>
{
    if (contexto.Request.Path.StartsWithSegments("/c") || contexto.Request.Path.StartsWithSegments("/admin"))
    {
        contexto.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        contexto.Response.Headers.CacheControl = "no-store";
    }

    return siguiente(contexto);
});

// Solo lo aplican los endpoints que piden su política: las páginas de reserva, Tu cita y el archivo de calendario.
app.UseRateLimiter();

// La sesión del Administrador: una página con [Authorize] sin sesión lleva a /admin/entrar (AD-22).
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapAgenda(DemasiadosIntentos.Politica);
app.MapIdentidad();
app.MapDefaultEndpoints();

app.Run();
