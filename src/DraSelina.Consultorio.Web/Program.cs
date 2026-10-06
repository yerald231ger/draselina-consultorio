using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Infrastructure;
using DraSelina.Consultorio.UI;
using DraSelina.Consultorio.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// Registros, métricas, trazas y salud: un solo camino de telemetría (AD-21).
builder.AddServiceDefaults();

var cadena = BaseDeDatos.CadenaDeConexion(builder.Configuration);

// Solo se registran los contextos. La web nunca crea ni migra nada al arrancar (AD-25):
// eso lo hace el MigrationService, que termina antes de que la web arranque.
builder.Services
    .AddAgenda(cadena)
    .AddPacientes(cadena)
    .AddNotificaciones(cadena);

// El único camino de la interfaz hacia el backend (AD-10).
builder.Services.AddScoped<ISender, SenderDeWolverine>();

// Dueño de la transacción de cada handler: el contexto de su módulo. Se fija aquí, una sola vez, como datos:
// namespace del handler -> contexto. Ningún handler ni comando declara nada.
builder.Services
    .AddTransaccionDeModulo<AgendaDbContext>("DraSelina.Consultorio.Agenda.Features")
    .AddTransaccionDeModulo<PacientesDbContext>("DraSelina.Consultorio.Pacientes.Features")
    .AddTransaccionDeModulo<NotificacionesDbContext>("DraSelina.Consultorio.Notificaciones.Features");

builder.Host.UseWolverine(opciones =>
{
    // Esquema `wolverine`, sin crear ni cambiar tablas al arrancar (AD-25). Si el esquema no existe, la web no
    // arranca: la base está sin migrar.
    AlmacenamientoDeWolverine.Configurar(opciones, cadena);

    // El monolito no tiene otro transporte: los mensajes van a colas locales, y durables para que se guarden en
    // el esquema `wolverine` dentro de la transacción del handler que los publica.
    opciones.Policies.UseDurableLocalQueues();

    // Un comando, una transacción, un módulo: el host la abre, el handler guarda y el host la cierra según el
    // Resultado. Las consultas corren sin transacción.
    opciones.Policies.Add(new UnaTransaccionPorModulo());
});

builder.Services.AddRazorComponents();

var app = builder.Build();

app.UseAntiforgery();

app.MapRazorComponents<App>();
app.MapDefaultEndpoints();

app.Run();
