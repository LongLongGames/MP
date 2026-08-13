using Npgsql;

namespace MP.Auth.Infrastructure.Db;

public sealed class CatalogRepository(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<GameRecord>> ListAsync(string? statusFilter = null)
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        var sql = """
            SELECT game_id, name, status, sort_order, icon_url, min_client_ver, extra_json, created_at, updated_at
            FROM games
            """;

        if (!string.IsNullOrWhiteSpace(statusFilter))
            sql += " WHERE status = @status";

        sql += " ORDER BY sort_order ASC, game_id ASC";

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (!string.IsNullOrWhiteSpace(statusFilter))
            cmd.Parameters.AddWithValue("status", statusFilter);

        var list = new List<GameRecord>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(ReadGame(reader));

        return list;
    }

    public async Task<GameRecord?> FindAsync(string gameId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT game_id, name, status, sort_order, icon_url, min_client_ver, extra_json, created_at, updated_at
            FROM games
            WHERE game_id = @gameId
            """, conn);

        cmd.Parameters.AddWithValue("gameId", gameId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return ReadGame(reader);
    }

    private static GameRecord ReadGame(NpgsqlDataReader reader) => new(
        reader.GetString(0),                                    // game_id
        reader.GetString(1),                                    // name
        reader.GetString(2),                                    // status
        reader.GetInt32(3),                                     // sort_order
        reader.IsDBNull(4) ? null : reader.GetString(4),        // icon_url
        reader.IsDBNull(5) ? null : reader.GetString(5),        // min_client_ver
        reader.IsDBNull(6) ? null : reader.GetString(6),        // extra_json
        reader.GetFieldValue<DateTimeOffset>(7),                // created_at
        reader.GetFieldValue<DateTimeOffset>(8));               // updated_at
}

public sealed record GameRecord(
    string GameId,
    string Name,
    string Status,
    int SortOrder,
    string? IconUrl,
    string? MinClientVer,
    string? ExtraJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
