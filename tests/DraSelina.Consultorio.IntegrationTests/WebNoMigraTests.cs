using System.Net;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Infrastructure;
using Microsoft.Extensions.Logging;

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

    [Fact]
    public async Task La_web_atiende_aunque_la_entrega_de_eventos_no_pueda_leer_ningun_outbox()
    {
        var vacia = await arnes.CrearBaseVaciaAsync(Cancelacion);

        // La web real, con su servicio de entrega corriendo contra una base donde no existe ningún outbox.
        await using var web = arnes.ArrancarWeb(vacia);
        using var cliente = web.CreateClient();

        // Además de las vueltas del servicio, una dada aquí: no lanza, registra el error y no entrega nada.
        Assert.Equal(0, await Arnes.DarUnaVueltaDeEntregaAsync(web, Cancelacion));
        Assert.Contains(Arnes.Registros(web), r => r.Nivel == LogLevel.Error && r.Mensaje.Contains("outbox"));

        using var respuesta = await cliente.GetAsync("/", Cancelacion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        Assert.False(await Consultas.ExisteEsquemaAsync(vacia, AgendaDbContext.Esquema, Cancelacion), "La web creó el esquema 'agenda'.");
        Assert.Empty(await Consultas.EsquemasConTablaAsync(vacia, Consultas.Historial, Cancelacion));
    }
}
