using System.Net;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Infrastructure;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// La web nunca crea ni migra nada al arrancar (AD-25). Se prueba contra una base vacía: contra la migrada no
/// habría nada pendiente y la prueba pasaría aunque la web migrara.
/// </summary>
public sealed class WebNoMigraTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task La_web_arranca_y_atiende_sin_crear_nada_en_una_base_vacia()
    {
        var vacia = await arnes.CrearBaseVaciaAsync(Cancelacion);

        await using var web = arnes.ArrancarWeb(vacia);
        using var cliente = web.CreateClient();
        using var respuesta = await cliente.GetAsync("/", Cancelacion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        string[] esquemas = [AgendaDbContext.Esquema, PacientesDbContext.Esquema, NotificacionesDbContext.Esquema];
        foreach (var esquema in esquemas)
        {
            Assert.False(await Consultas.ExisteEsquemaAsync(vacia, esquema, Cancelacion), $"La web creó el esquema '{esquema}'.");
        }

        Assert.Empty(await Consultas.EsquemasConTablaAsync(vacia, Consultas.Historial, Cancelacion));
    }
}
