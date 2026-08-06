using MP.Auth.Infrastructure.Db;

namespace MP.Auth.Providers;

public sealed class AuthValidatorFactory(AccountRepository repo)
{
    // 已实现：official、guest。其余先占位保留，等真正要接某个渠道再实现替换。
    private static readonly HashSet<string> Reserved =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "steam", "psn", "xbox", "nintendo", "apple", "google", "microsoft_store", "official_oauth",
        };

    public IAuthValidator Get(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "guest" => new GuestAuthValidator(),
            "official" => new OfficialAuthValidator(repo),
            var p when Reserved.Contains(p) => new NotImplementedAuthValidator(p),
            _ => new NotImplementedAuthValidator(provider),
        };
    }
}
