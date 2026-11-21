using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;

[assembly: AssemblyFixture(typeof(DraSelina.Consultorio.E2ETests.Consultorio))]

namespace DraSelina.Consultorio.E2ETests;

/// <summary>
/// El consultorio completo, una vez por corrida: instala Chromium, arranca el AppHost con Aspire.Hosting.Testing
/// (PostgreSQL, migraciones, Mailpit y la web) y abre el navegador. Al terminar apaga todo. La base parte vacía en
/// cada arranque, como con <c>dotnet run</c>.
/// </summary>
public sealed class Consultorio : IAsyncLifetime
{
    // El primer arranque descarga imágenes de contenedor y compila; los siguientes tardan segundos.
    private static readonly TimeSpan LimiteDeArranque = TimeSpan.FromMinutes(5);

    private DistributedApplication? _aplicacion;
    private IPlaywright? _playwright;
    private IBrowser? _navegador;

    /// <summary>La dirección de la web.</summary>
    public Uri Web { get; private set; } = null!;

    /// <summary>Los correos que atrapó Mailpit.</summary>
    public Mailpit Mailpit { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // Sin PowerShell: el instalador de Playwright se llama desde .NET. Si Chromium ya está, no descarga nada.
        var instalacion = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (instalacion != 0)
        {
            throw new InvalidOperationException($"No se pudo instalar Chromium para Playwright (código {instalacion}).");
        }

        using var limite = new CancellationTokenSource(LimiteDeArranque);

        // La contraseña inicial del Administrador es un parámetro secreto sin valor por omisión: sin ella el AppHost
        // la pide y la web no arranca. Una temporal por corrida, que cumple la política de Identity.
        var claveTemporal = $"E2e#{Guid.NewGuid():N}";
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.DraSelina_Consultorio_AppHost>(
            [$"--Parameters:clave-administrador={claveTemporal}"], limite.Token);

        _aplicacion = await appHost.BuildAsync(limite.Token);
        await _aplicacion.StartAsync(limite.Token);
        // La web arranca solo cuando las migraciones terminaron.
        await _aplicacion.ResourceNotifications.WaitForResourceHealthyAsync("web", limite.Token);
        await _aplicacion.ResourceNotifications.WaitForResourceHealthyAsync("mailpit", limite.Token);

        Web = _aplicacion.GetEndpoint("web", "http");
        Mailpit = new Mailpit(_aplicacion.GetEndpoint("mailpit", "http"));

        // Las expectativas esperan hasta 15 segundos (5 por omisión): con la máquina cargada la web responde más lento.
        Assertions.SetDefaultExpectTimeout(15_000);
        _playwright = await Playwright.CreateAsync();
        // Con RECORRIDOS_VISIBLES=1 Chromium abre con ventana y hace una pausa entre acciones, para seguir las pruebas
        // a ojo. Sin la variable, corre sin ventana y a toda velocidad.
        var visibles = Environment.GetEnvironmentVariable("RECORRIDOS_VISIBLES") == "1";
        _navegador = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = !visibles,
            SlowMo = visibles ? 500 : 0,
        });
    }

    /// <summary>Un navegador nuevo en vista de celular, sin cookies de otra prueba.</summary>
    public Task<IBrowserContext> NuevoCelularAsync()
    {
        var celular = new BrowserNewContextOptions(_playwright!.Devices["Pixel 7"]) { BaseURL = Web.ToString() };
        return _navegador!.NewContextAsync(celular);
    }

    public async ValueTask DisposeAsync()
    {
        if (_navegador is not null)
        {
            await _navegador.DisposeAsync();
        }

        _playwright?.Dispose();
        Mailpit?.Dispose();

        if (_aplicacion is not null)
        {
            await _aplicacion.DisposeAsync();
        }
    }
}
