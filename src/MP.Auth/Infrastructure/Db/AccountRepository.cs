using Dapper;
using MP.Auth.Domain;
using Npgsql;

namespace MP.Auth.Infrastructure.Db;

public sealed class AccountRepository(NpgsqlDataSource dataSource)
{
    public async Task<AccountBinding?> FindBindingAsync(string provider, string thirdPartyId)
    {
        await using var conn = dataSource.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AccountBinding>(
            """
            SELECT id AS Id, account_id AS AccountId, provider AS Provider,
                   third_party_id AS ThirdPartyId, extra_data AS ExtraData, created_at AS CreatedAt
            FROM mp_account_bindings
            WHERE provider = @provider AND third_party_id = @thirdPartyId
            """,
            new { provider, thirdPartyId });
    }

    /// <summary>新建账号 + 首个绑定关系，返回新账号 id（事务）。</summary>
    public async Task<Guid> CreateAccountWithBindingAsync(string provider, string thirdPartyId, string? extraDataJson)
    {
        await using var conn = dataSource.CreateConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var accountId = await conn.QuerySingleAsync<Guid>(
            "INSERT INTO mp_accounts DEFAULT VALUES RETURNING id", transaction: tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO mp_account_bindings (account_id, provider, third_party_id, extra_data)
            VALUES (@accountId, @provider, @thirdPartyId, CAST(@extraDataJson AS JSONB))
            """,
            new { accountId, provider, thirdPartyId, extraDataJson }, transaction: tx);

        await tx.CommitAsync();
        return accountId;
    }

    public async Task<Account?> FindAccountAsync(Guid accountId)
    {
        await using var conn = dataSource.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Account>(
            "SELECT id AS Id, status AS Status, created_at AS CreatedAt FROM mp_accounts WHERE id = @accountId",
            new { accountId });
    }

    public async Task<string[]> GetBoundProvidersAsync(Guid accountId)
    {
        await using var conn = dataSource.CreateConnection();
        var providers = await conn.QueryAsync<string>(
            "SELECT provider FROM mp_account_bindings WHERE account_id = @accountId",
            new { accountId });
        return providers.ToArray();
    }
}
