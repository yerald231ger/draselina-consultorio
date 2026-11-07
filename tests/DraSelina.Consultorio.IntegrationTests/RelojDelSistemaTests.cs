using DraSelina.Consultorio.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>El reloj desplazado de Development (épica 3, entrada 5).</summary>
public sealed class RelojDelSistemaTests
{
    [Fact]
    public void Sin_desplazamiento_da_la_hora_real()
    {
        var antes = DateTimeOffset.UtcNow;
        var ahora = new RelojDelSistema().UtcNow;

        Assert.InRange(ahora, antes, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Suma_el_desplazamiento_y_lo_vuelve_a_leer_en_cada_llamada()
    {
        var opciones = new Monitor(new OpcionesDeReloj { Desplazamiento = TimeSpan.FromHours(23) });
        var reloj = new RelojDelSistema(opciones);

        var antes = DateTimeOffset.UtcNow;
        Assert.InRange(reloj.UtcNow, antes + TimeSpan.FromHours(23), DateTimeOffset.UtcNow + TimeSpan.FromHours(23));

        // Como al guardar appsettings.Development.json: la siguiente lectura ya trae el nuevo valor.
        opciones.CurrentValue = new OpcionesDeReloj { Desplazamiento = null };
        antes = DateTimeOffset.UtcNow;
        Assert.InRange(reloj.UtcNow, antes, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Un_desplazamiento_que_no_es_duracion_dice_donde_esta_el_error()
    {
        var reloj = new RelojDelSistema(new MonitorRoto());

        var error = Assert.Throws<InvalidOperationException>(() => reloj.UtcNow);

        Assert.Contains(OpcionesDeReloj.ArchivoLocal, error.Message, StringComparison.Ordinal);
        Assert.Contains("23:00:00", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production", "01:00:00", true)]
    [InlineData("Staging", "-00:30:00", true)]
    [InlineData("Production", "00:00:00", false)]
    [InlineData("Production", null, false)]
    [InlineData("Development", "23:00:00", false)]
    public void Fuera_de_Development_un_desplazamiento_distinto_de_cero_impide_arrancar(
        string entorno, string? desplazamiento, bool falla)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Reloj:Desplazamiento"] = desplazamiento })
            .Build();

        var error = Record.Exception(() => OpcionesDeReloj.ExigirSoloEnDevelopment(configuracion, new Entorno(entorno)));

        if (falla)
        {
            Assert.IsType<InvalidOperationException>(error);
            Assert.Contains("Reloj:Desplazamiento", error.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Null(error);
        }
    }

    private sealed class Monitor(OpcionesDeReloj valor) : IOptionsMonitor<OpcionesDeReloj>
    {
        public OpcionesDeReloj CurrentValue { get; set; } = valor;

        public OpcionesDeReloj Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<OpcionesDeReloj, string?> listener) => null;
    }

    // Como el enlazador de opciones con "23h": cada lectura lanza.
    private sealed class MonitorRoto : IOptionsMonitor<OpcionesDeReloj>
    {
        public OpcionesDeReloj CurrentValue => throw new InvalidOperationException("Failed to convert '23h' to type 'System.TimeSpan'.");

        public OpcionesDeReloj Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<OpcionesDeReloj, string?> listener) => null;
    }

    private sealed class Entorno(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;

        public string ApplicationName { get; set; } = "DraSelina.Consultorio.Web";

        public string ContentRootPath { get; set; } = "";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
