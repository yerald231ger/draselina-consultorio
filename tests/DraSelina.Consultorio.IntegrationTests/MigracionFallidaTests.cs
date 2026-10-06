using Npgsql;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>Si el <c>MigrationService</c> falla, el arnés no sigue: lanza con el código de salida y la salida.</summary>
public sealed class MigracionFallidaTests(Arnes arnes)
{
    [Fact]
    public async Task Una_migracion_fallida_lanza_con_el_codigo_de_salida_y_la_salida_del_proceso()
    {
        // El mismo servidor, con una contraseña que no es: PostgreSQL rechaza la conexión.
        var cadenaQueNoSirve = new NpgsqlConnectionStringBuilder(arnes.CadenaDeConexion) { Password = "no-es-la-contraseña" }.ConnectionString;

        var error = await Assert.ThrowsAsync<MigracionFallidaException>(
            () => MigrationService.EjecutarAsync(cadenaQueNoSirve, TestContext.Current.CancellationToken));

        Assert.Equal(1, error.CodigoDeSalida);
        Assert.Contains("código de salida 1", error.Message);
        Assert.Contains("Las migraciones fallaron", error.Salida);
        Assert.Contains(error.Salida, error.Message);
    }
}
