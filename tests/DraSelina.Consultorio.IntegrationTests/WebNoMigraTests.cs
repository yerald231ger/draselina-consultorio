using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Infrastructure;
using DraSelina.Consultorio.Web;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La web nunca crea ni migra nada al arrancar, y sobre una base sin migrar se niega a arrancar (AD-25). Se prueba
/// contra una base vacía: contra la migrada no habría nada pendiente y la prueba pasaría aunque la web migrara.
/// </summary>
public sealed class WebNoMigraTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task La_web_no_arranca_en_una_base_sin_migrar_y_no_crea_nada()
    {
        var vacia = await arnes.CrearBaseVaciaAsync(Cancelacion);

        await using var web = arnes.ArrancarWeb(vacia);
        var error = Assert.ThrowsAny<Exception>(() => web.CreateClient());

        // Se niega por lo que falta, no por otra causa: el almacenamiento de mensajes que crea el MigrationService.
        Assert.Contains("message storage", error.ToString());
        Assert.Contains(AlmacenamientoDeWolverine.Esquema, error.ToString());

        string[] esquemas =
        [
            AgendaDbContext.Esquema, PacientesDbContext.Esquema, NotificacionesDbContext.Esquema, AlmacenamientoDeWolverine.Esquema,
        ];
        foreach (var esquema in esquemas)
        {
            Assert.False(await Consultas.ExisteEsquemaAsync(vacia, esquema, Cancelacion), $"La web creó el esquema '{esquema}'.");
        }

        Assert.Empty(await Consultas.EsquemasConTablaAsync(vacia, Consultas.Historial, Cancelacion));
    }
}
