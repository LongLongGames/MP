using MP.Auth.Domain;
using Npgsql;

namespace MP.Auth.Infrastructure.Db;

public sealed class AccountRepository(NpgsqlDataSource dataSource)
{
    public async Task<AccountBinding?> FindBindingAsync(string provider, string thirdPartyId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            """
            SELECT id, account_id, provider, third_party_id, extra_data, created_at
            FROM mp_account_bindings
            WHERE provider = @provider AND third_party_id = @thirdPartyId
            """, conn);

        cmd.Parameters.AddWithValue("provider", provider);
        cmd.Parameters.AddWithValue("thirdPartyId", thirdPartyId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new AccountBinding(
            reader.GetInt64(0),                          // id → long
            reader.GetGuid(1),                           // account_id → Guid
            reader.GetString(2),                         // provider
            reader.GetString(3),                         // third_party_id
            reader.IsDBNull(4) ? null : reader.GetString(4), // extra_data
            reader.GetFieldValue<DateTimeOffset>(5));    // created_at
    }

    /// <summary>新建账号 + 首个绑定关系，返回新账号 id（事务）。</summary>
    public async Task<Guid> CreateAccountWithBindingAsync(string provider, string thirdPartyId, string? extraDataJson)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        Guid accountId;
        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO mp_accounts DEFAULT VALUES RETURNING id", conn, tx))
        {
            accountId = (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        await using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO mp_account_bindings (account_id, provider, third_party_id, extra_data)
            VALUES (@accountId, @provider, @thirdPartyId, CAST(@extraDataJson AS JSONB))
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("accountId", accountId);
            cmd.Parameters.AddWithValue("provider", provider);
            cmd.Parameters.AddWithValue("thirdPartyId", thirdPartyId);
            cmd.Parameters.AddWithValue("extraDataJson", (object?)extraDataJson ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return accountId;
    }

    public async Task<Account?> FindAccountAsync(Guid accountId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, status, created_at FROM mp_accounts WHERE id = @accountId", conn);

        cmd.Parameters.AddWithValue("accountId", accountId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new Account(
            reader.GetGuid(0),                           // id → Guid
            reader.GetInt16(1),                          // status → short
            reader.GetFieldValue<DateTimeOffset>(2));    // created_at
    }

    public async Task<string[]> GetBoundProvidersAsync(Guid accountId)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT provider FROM mp_account_bindings WHERE account_id = @accountId", conn);

        cmd.Parameters.AddWithValue("accountId", accountId);

        var list = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            list.Add(reader.GetString(0));

        return list.ToArray();
    }
}