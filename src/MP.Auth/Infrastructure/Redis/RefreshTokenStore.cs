using StackExchange.Redis;

namespace MP.Auth.Infrastructure.Redis;

public sealed class RefreshTokenStore(IConnectionMultiplexer redis)
{
    private static string Key(string accountId, string deviceId) => $"ref_token:{accountId}:{deviceId}";

    public async Task SetAsync(string accountId, string deviceId, string refreshToken, TimeSpan ttl)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(Key(accountId, deviceId), refreshToken, ttl);
    }

    /// <summary>校验通过则原子替换为新 refreshToken（滚动刷新），否则返回 false。</summary>
    public async Task<bool> ValidateAndRotateAsync(string accountId, string deviceId, string presented, string next, TimeSpan ttl)
    {
        var db = redis.GetDatabase();
        var stored = await db.StringGetAsync(Key(accountId, deviceId));
        if (stored.IsNullOrEmpty || stored != presented) return false;
        await db.StringSetAsync(Key(accountId, deviceId), next, ttl);
        return true;
    }

    public async Task RevokeAsync(string accountId, string deviceId)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(Key(accountId, deviceId));
    }
}
