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

    /// <summary>Las columnas de una tabla, en su orden: nombre, tipo y si admite nulo.</summary>
    public static async Task<List<(string Nombre, string Tipo, bool Nulo)>> ColumnasAsync(
        string cadena, string esquema, string tabla, CancellationToken cancelacion)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);
        await using var comando = new NpgsqlCommand(
            "select column_name, data_type, is_nullable from information_schema.columns "
            + "where table_schema = @esquema and table_name = @tabla order by ordinal_position", conexion);
        comando.Parameters.AddWithValue("esquema", esquema);
        comando.Parameters.AddWithValue("tabla", tabla);

        var columnas = new List<(string, string, bool)>();
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        while (await lector.ReadAsync(cancelacion))
        {
            columnas.Add((lector.GetString(0), lector.GetString(1), lector.GetString(2) == "YES"));
        }

        return columnas;
    }

    /// <summary>La fila del outbox de un esquema con ese id (el <c>EventoId</c>), o nulo si no está.</summary>
    public static async Task<FilaLeidaDeOutbox?> EventoAsync(string cadena, string esquema, Guid id, CancellationToken cancelacion) =>
        (await OutboxAsync(cadena, esquema, "id", id, cancelacion)).SingleOrDefault();

    /// <summary>Las filas del outbox de un esquema con esa clave de orden, en el orden en que ocurrieron.</summary>
    public static Task<List<FilaLeidaDeOutbox>> EventosDeLaClaveAsync(string cadena, string esquema, Guid clave, CancellationToken cancelacion) =>
        OutboxAsync(cadena, esquema, "clave_de_orden", clave, cancelacion);

    private static async Task<List<FilaLeidaDeOutbox>> OutboxAsync(string cadena, string esquema, string columna, Guid valor, CancellationToken cancelacion)
    {
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(cancelacion);
        await using var comando = new NpgsqlCommand(
            "select id, tipo, contenido::text, ocurrido_en, clave_de_orden, estado, intentos, proximo_intento_en, entregado_en "
            + $"from \"{esquema}\".outbox where {columna} = @valor order by ocurrido_en, id", conexion);
        comando.Parameters.AddWithValue("valor", valor);

        var filas = new List<FilaLeidaDeOutbox>();
        await using var lector = await comando.ExecuteReaderAsync(cancelacion);
        while (await lector.ReadAsync(cancelacion))
        {
            filas.Add(new FilaLeidaDeOutbox(
                lector.GetGuid(0),
                lector.GetString(1),
                lector.GetString(2),
                lector.GetFieldValue<DateTimeOffset>(3),
                lector.GetGuid(4),
                lector.GetString(5),
                lector.GetInt32(6),
                lector.GetFieldValue<DateTimeOffset>(7),
                lector.IsDBNull(8) ? null : lector.GetFieldValue<DateTimeOffset>(8)));
        }

        return filas;
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

/// <summary>Una fila de un outbox tal como está en la base, con sus nueve columnas.</summary>
internal sealed record FilaLeidaDeOutbox(
    Guid Id,
    string Tipo,
    string Contenido,
    DateTimeOffset OcurridoEn,
    Guid ClaveDeOrden,
    string Estado,
    int Intentos,
    DateTimeOffset ProximoIntentoEn,
    DateTimeOffset? EntregadoEn);
