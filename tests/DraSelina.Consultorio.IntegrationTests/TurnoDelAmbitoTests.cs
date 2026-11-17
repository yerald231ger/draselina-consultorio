using System.Security.Claims;
using DraSelina.Consultorio.Agenda.Contracts;
using DraSelina.Consultorio.BuildingBlocks.Contracts;
using Microsoft.Extensions.DependencyInjection;
using static DraSelina.Consultorio.IntegrationTests.ReservaEnLineaTests;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>
/// Un handler a la vez por ámbito (<c>TurnoDelAmbito</c>, recorrido 6.8) contra PostgreSQL real: en Interactive Server
/// las secciones de Configuración consultan a la vez en un mismo circuito, con el mismo <c>AgendaDbContext</c>. Sin el
/// turno, EF lanzaba "A second operation was started…" y solo una sección cargaba.
/// </summary>
public sealed class TurnoDelAmbitoTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Las_tres_consultas_de_Configuracion_a_la_vez_en_un_ambito_cargan_todas()
    {
        await using var escenario = await Escenario.CrearAsync(arnes);
        await using var ambito = escenario.Web.Services.CreateAsyncScope();
        Arnes.FijarUsuario(ambito, new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "cuenta-del-administrador"), new Claim(ClaimTypes.Name, "administradora")],
            authenticationType: "Prueba")));
        var servicios = ambito.ServiceProvider;

        for (var vuelta = 0; vuelta < 10; vuelta++)
        {
            var semana = servicios.GetRequiredService<IQueryHandler<SemanaTipo, IReadOnlyList<DiaDeLaBase>>>()
                .HandleAsync(new SemanaTipo(), Cancelacion);
            var reglas = servicios.GetRequiredService<IQueryHandler<ReglasDeReservaVigentes, ReglasDeReservaValores>>()
                .HandleAsync(new ReglasDeReservaVigentes(), Cancelacion);
            var tipos = servicios.GetRequiredService<IQueryHandler<TiposDeVisitaEnConfiguracion, IReadOnlyList<TipoDeVisitaEditable>>>()
                .HandleAsync(new TiposDeVisitaEnConfiguracion(), Cancelacion);

            await Task.WhenAll(semana, reglas, tipos);

            Assert.Equal(7, (await semana).Valor.Count);
            Assert.Equal(new ReglasDeReservaValores(1, 2, 120, 3), (await reglas).Valor);
            Assert.NotEmpty((await tipos).Valor);
        }
    }
}
