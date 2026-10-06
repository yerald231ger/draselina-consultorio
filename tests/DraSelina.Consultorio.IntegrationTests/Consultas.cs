using Npgsql;

namespace DraSelina.Consultorio.IntegrationTests;

/// <summary>Consultas al catálogo de PostgreSQL que usan las afirmaciones.</summary>
internal static class Consultas
{
    public const string Historial = "__EFMigrationsHistory";

    /// <summary>Los esquemas de la base que tienen una tabla con ese nombre.</summary>
    public static Task<List<string>> EsquemasConTablaAsync(string cadena, string tabla, CancellationToken cancelacion) =>
        ListaAsync(cadena, "select table_schema from information_schema.tables where table_name = @valor order by 1", tabla, cancelacion);

    /// <summary>Las tablas de un esquema.</summary>
    public static Task<List<string>> TablasAsync(string cadena, string esquema, CancellationToken cancelacion) =>
        ListaAsync(cadena, "select table_name from information_schema.tables where table_schema = @valor order by 1", esquema, cancelacion);

    public static async Task<bool> ExisteEsquemaAsync(string cadena, string esquema, CancellationToken cancelacion) =>
        (await ListaAsync(cadena, "select schema_name from information_schema.schemata where schema_name = @valor", esquema, cancelacion)).Count == 1;

    /// <summary>Cuántas migraciones tiene aplicadas el historial de un esquema.</summary>
    public static async Task<long> MigracionesAplicadasAsync(string cadena, string esquema, CancellationToken cancelacion)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);
        await using var comando = new NpgsqlCommand($"select count(*) from \"{esquema}\".\"{Historial}\"", conexion);
        return (long)(await comando.ExecuteScalarAsync(cancelacion))!;
    }

    /// <summary>
    /// Cuántos mensajes guardados en el almacenamiento de Wolverine llevan ese texto en su cuerpo, entre los que
    /// entran y los que salen. Para encontrar el de una prueba, el texto es un valor que solo ella conoce.
    /// </summary>
    public static async Task<long> MensajesGuardadosAsync(string cadena, string textoDelCuerpo, CancellationToken cancelacion)
    {
        const string sql = """
            select (select count(*) from wolverine.wolverine_incoming_envelopes where position(@valor in encode(body, 'escape')) > 0)
                 + (select count(*) from wolverine.wolverine_outgoing_envelopes where position(@valor in encode(body, 'escape')) > 0)
            """;

        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("valor", textoDelCuerpo);
        return (long)(await comando.ExecuteScalarAsync(cancelacion))!;
    }

    private static async Task<List<string>> ListaAsync(string cadena, string sql, string valor, CancellationToken cancelacion)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("valor", valor);

        var filas = new List<string>();
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        while (await lector.ReadAsync(cancelacion))
        {
            filas.Add(lector.GetString(0));
        }

        return filas;
    }
}
