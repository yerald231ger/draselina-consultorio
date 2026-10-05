using System.Runtime.InteropServices;
using Arranque.Contracts;
using Arranque.Web;
using Arranque.Web.Components;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.DataAnnotationsValidation;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

var cadena = builder.Configuration.GetConnectionString("arranque")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'arranque'; la entrega el AppHost.");

// "nativo": middleware de DataAnnotations de Wolverine. "propio": ValidacionPropia.
var modo = builder.Configuration["Validacion:Modo"] ?? "nativo";

var diagnostico = new Diagnostico();
builder.Services.AddSingleton(diagnostico);
builder.Logging.AddProvider(diagnostico);

builder.Services.AddRazorComponents();
builder.Services.AddDbContextWithWolverineIntegration<NotasDbContext>(o => o.UseNpgsql(cadena));

builder.Host.UseWolverine(opts =>
{
    opts.PersistMessagesWithPostgresql(cadena, "wolverine");
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.AutoApplyTransactions();

    if (modo == "propio")
    {
        opts.Policies.ForMessagesOfType<IComando>().AddMiddleware(typeof(ValidacionPropia));
    }
    else
    {
        opts.UseDataAnnotationsValidation();
    }
});

var app = builder.Build();

// SOLO EN ESTE PROTOTIPO. Contradice AD-25 a propósito: aquí no existe MigrationService
// (es la entrada 3). La aplicación real nunca crea ni migra su esquema al arrancar.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<NotasDbContext>().Database.EnsureCreatedAsync();
}

app.UseAntiforgery();
app.MapRazorComponents<App>();

app.MapGet("/diag", (Diagnostico d) => Results.Json(new
{
    runtime = RuntimeInformation.FrameworkDescription,
    wolverine = typeof(IMessageBus).Assembly.GetName().Version?.ToString(),
    efcore = typeof(DbContext).Assembly.GetName().Version?.ToString(),
    modo,
    ejecucionesDelHandler = d.EjecucionesDelHandler,
    erroresRegistrados = d.ErroresRegistrados,
    ultimosErrores = d.UltimosErrores
}));

app.Run();
