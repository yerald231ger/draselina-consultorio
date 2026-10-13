using System.Text.Json;
using DraSelina.Consultorio.Agenda.Features;
using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using DraSelina.Consultorio.IntegrationTests.Pruebas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Guardar un evento: se publica por el puerto de outbox del módulo, dentro de la transacción del handler, y queda
/// con el cambio o no queda (AD-3). Aquí no se afirma nada de la entrega: eso está en <c>EntregaDeEventosTests</c>
/// y <c>ReintentosYOrdenTests</c>.
/// </summary>
public sealed class OutboxTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task El_cambio_y_su_evento_se_confirman_juntos()
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando(Desenlace.Exito);

        Assert.True((await EjecutarAsync(web.Services, comando)).EsExito);

        Assert.True(await ExisteAnotacionAsync(comando.Id));

        // Solo se afirma lo que no cambia: en la base de la corrida, cualquier web puede entregarlo en cualquier momento.
        var fila = Assert.Single(await Consultas.EventosDeLaClaveAsync(arnes.CadenaDeConexion, PruebasDbContext.Esquema, comando.Id, Cancelacion));
        var evento = JsonSerializer.Deserialize<AnotacionHecha>(fila.Contenido)!;
        Assert.Equal(nameof(AnotacionHecha), fila.Tipo);
        Assert.Equal(evento.EventoId, fila.Id);
        Assert.Equal(comando.Id, evento.AnotacionId);
    }

    [Theory]
    [InlineData(Desenlace.FalloAlFinal)]
    [InlineData(Desenlace.LanzarAlFinal)]
    [InlineData(Desenlace.ExitoSinConfirmar)]
    public async Task Si_el_comando_no_confirma_despues_de_publicar_no_queda_ni_el_cambio_ni_el_evento(Desenlace desenlace)
    {
        await using var web = arnes.ArrancarWeb();
        var comando = NuevoComando(desenlace);

        try
        {
            await EjecutarAsync(web.Services, comando);
        }
        catch (FalloInesperadoDePrueba)
        {
            // El desenlace que lanza.
        }

        Assert.False(await ExisteAnotacionAsync(comando.Id));
        Assert.Empty(await Consultas.EventosDeLaClaveAsync(arnes.CadenaDeConexion, PruebasDbContext.Esquema, comando.Id, Cancelacion));
    }

    [Fact]
    public async Task El_outbox_de_Agenda_guarda_el_evento_si_la_transaccion_se_confirma_y_no_si_se_deshace()
    {
        // En una base propia y sin entrega automática: así la fila sigue como la dejó quien publicó.
        await using var escenario = await EscenarioDeEntrega.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        var transaccion = ambito.ServiceProvider.GetRequiredService<ITransaccionDeAgenda>();
        var outbox = ambito.ServiceProvider.GetRequiredService<IOutboxDeAgenda>();

        var confirmado = new EventoDePrueba(Guid.CreateVersion7(), FallasAntesDeEntregar: 3);
        var deshecho = new EventoDePrueba(Guid.CreateVersion7());
        var clave = Guid.CreateVersion7();
        var ocurridoEn = escenario.Reloj.UtcNow;

        await using (var tx = await transaccion.AbrirAsync(Cancelacion))
        {
            // Publicado como object: se guarda con el tipo real del evento, no con el de la variable.
            await outbox.PublicarAsync((object)confirmado, confirmado.EventoId, clave, ocurridoEn, Cancelacion);
            await tx.ConfirmarAsync(Cancelacion);
        }

        await using (var tx = await transaccion.AbrirAsync(Cancelacion))
        {
            await outbox.PublicarAsync(deshecho, deshecho.EventoId, clave, ocurridoEn, Cancelacion);
            await tx.DeshacerAsync(Cancelacion);
        }

        var fila = await Consultas.EventoAsync(escenario.Cadena, AgendaDbContext.Esquema, confirmado.EventoId, Cancelacion);
        Assert.NotNull(fila);
        Assert.Equal(nameof(EventoDePrueba), fila.Tipo);
        Assert.Equal(confirmado, JsonSerializer.Deserialize<EventoDePrueba>(fila.Contenido));
        Assert.Equal(ocurridoEn, fila.OcurridoEn);
        Assert.Equal(clave, fila.ClaveDeOrden);
        Assert.Equal("Pendiente", fila.Estado);
        Assert.Equal(0, fila.Intentos);
        Assert.Equal(ocurridoEn, fila.ProximoIntentoEn);
        Assert.Null(fila.EntregadoEn);

        Assert.Null(await Consultas.EventoAsync(escenario.Cadena, AgendaDbContext.Esquema, deshecho.EventoId, Cancelacion));
    }

    [Fact]
    public async Task Publicar_sin_una_transaccion_abierta_lanza_y_no_guarda_nada()
    {
        await using var web = arnes.ArrancarWeb();
        await using var ambito = web.Services.CreateAsyncScope();
        var outbox = ambito.ServiceProvider.GetRequiredService<IOutboxDeAgenda>();
        var evento = new EventoDePrueba(Guid.CreateVersion7());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => outbox.PublicarAsync(evento, evento.EventoId, Guid.CreateVersion7(), DateTimeOffset.UtcNow, Cancelacion));

        Assert.Null(await Consultas.EventoAsync(arnes.CadenaDeConexion, AgendaDbContext.Esquema, evento.EventoId, Cancelacion));
    }

    [Fact]
    public async Task La_base_migrada_tiene_agenda_outbox_con_las_columnas_del_modelo_de_datos()
    {
        var columnas = await Consultas.ColumnasAsync(arnes.CadenaDeConexion, AgendaDbContext.Esquema, "outbox", Cancelacion);

        Assert.Equal(
            [
                ("id", "uuid", false),
                ("tipo", "text", false),
                ("contenido", "jsonb", false),
                ("ocurrido_en", "timestamp with time zone", false),
                ("clave_de_orden", "uuid", false),
                ("estado", "text", false),
                ("intentos", "integer", false),
                ("proximo_intento_en", "timestamp with time zone", false),
                ("entregado_en", "timestamp with time zone", true),
            ],
            columnas);
    }

    private static Anotar NuevoComando(Desenlace desenlace) =>
        new() { Id = Guid.CreateVersion7(), Texto = $"anotación {Guid.NewGuid():N}", Desenlace = desenlace };

    private static async Task<Kernel.Resultado<Guid>> EjecutarAsync(IServiceProvider servicios, Anotar comando)
    {
        await using var ambito = servicios.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<ICommandHandler<Anotar, Guid>>().HandleAsync(comando, Cancelacion);
    }

    private async Task<bool> ExisteAnotacionAsync(Guid id)
    {
        await using var contexto = arnes.CrearContextoDePruebas();
        return await contexto.Anotaciones.AsNoTracking().AnyAsync(a => a.Id == id, Cancelacion);
    }
}
