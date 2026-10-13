using DraSelina.Consultorio.Agenda.Infrastructure;
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
builder.Services
    .AddAgenda(cadena)
    .AddPacientes(cadena)
    .AddNotificaciones(cadena);

// El reloj de la aplicación, y la entrega de eventos entre módulos (AD-3): un servicio en segundo plano entrega
// los eventos de cada outbox dado de alta aquí. Hoy solo publica Agenda.
builder.Services
    .AddSingleton<IClock, RelojDelSistema>()
    .AddEntregaDeEventos()
    .AddOutbox<AgendaDbContext>();

builder.Services.AddRazorComponents();

var app = builder.Build();

app.UseAntiforgery();

app.MapRazorComponents<App>();
app.MapDefaultEndpoints();

app.Run();
