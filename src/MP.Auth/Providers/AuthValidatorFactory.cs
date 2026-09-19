using MP.Auth.Infrastructure.Db;
using Microsoft.Extensions.Configuration;

namespace MP.Auth.Providers;

public sealed class AuthValidatorFactory(
    AccountRepository repo,
    IConfiguration config,
    IHttpClientFactory httpClientFactory)
{
    // 其余渠道先占位，真正要接时再实现替换。
    private static readonly HashSet<string> Reserved =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "psn", "xbox", "nintendo", "apple", "google", "microsoft_store", "official_oauth",
        };

    public IAuthValidator Get(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "guest" => new GuestAuthValidator(),
            "official" => new OfficialAuthValidator(repo),
            "steam" => new SteamAuthValidator(config, httpClientFactory.CreateClient("steam")),
            var p when Reserved.Contains(p) => new NotImplementedAuthValidator(p),
            _ => new NotImplementedAuthValidator(provider),
        };
    }
}
