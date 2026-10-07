using DraSelina.Consultorio.Agenda.Infrastructure;
using DraSelina.Consultorio.Notificaciones.Infrastructure;
using DraSelina.Consultorio.Pacientes.Infrastructure;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>Lo que el <c>MigrationService</c> deja en la base de la corrida (AD-2, AD-25).</summary>
public sealed class BaseMigradaTests(Arnes arnes)
{
    private static CancellationToken Cancelacion => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(AgendaDbContext.Esquema)]
    [InlineData(PacientesDbContext.Esquema)]
    [InlineData(NotificacionesDbContext.Esquema)]
    public async Task Cada_modulo_lleva_su_historial_de_migraciones_en_su_esquema(string esquema)
    {
        var tablas = await Consultas.TablasAsync(arnes.CadenaDeConexion, esquema, Cancelacion);

        Assert.Contains(Consultas.Historial, tablas);
        Assert.True(await Consultas.MigracionesAplicadasAsync(arnes.CadenaDeConexion, esquema, Cancelacion) >= 1);
    }

    [Fact]
    public async Task No_hay_historial_de_migraciones_fuera_de_los_esquemas_de_los_modulos()
    {
        var esquemas = await Consultas.EsquemasConTablaAsync(arnes.CadenaDeConexion, Consultas.Historial, Cancelacion);

        Assert.DoesNotContain("public", esquemas);
        Assert.Equal(
            [AgendaDbContext.Esquema, NotificacionesDbContext.Esquema, PacientesDbContext.Esquema],
            esquemas);
    }

    [Fact]
    public async Task La_base_migrada_no_tiene_el_esquema_wolverine()
    {
        // El MigrationService ya no crea ningún almacenamiento de mensajes: ese esquema no existe.
        Assert.False(await Consultas.ExisteEsquemaAsync(arnes.CadenaDeConexion, "wolverine", Cancelacion));
    }
}
