using System.Text.Json;
using System.Text.Json.Serialization;
using MP.Auth.Infrastructure.Db;

namespace MP.Auth.Providers;

internal sealed record OfficialCredential(string Salt, string Hash);

[JsonSerializable(typeof(OfficialCredential))]
internal partial class OfficialCredentialJsonContext : JsonSerializerContext
{
}

/// <summary>
/// provider = official：用户名+密码。第一次登录即自动注册（幂等，跟其他渠道"自动注册"语义一致）；
/// 用户名已存在则校验密码。方便自己写 App / 内网测试直接用，不依赖任何第三方平台。
/// </summary>
public sealed class OfficialAuthValidator(AccountRepository repo) : IAuthValidator
{
    public async Task<AuthValidationResult> ValidateAsync(Dictionary<string, string>? payload)
    {
        if (payload is null
            || !payload.TryGetValue("username", out var username) || string.IsNullOrWhiteSpace(username)
            || !payload.TryGetValue("password", out var password) || string.IsNullOrWhiteSpace(password))
            return AuthValidationResult.Fail("auth_payload.username / password 不能为空");

        if (password.Length < 6)
            return AuthValidationResult.Fail("密码至少 6 位");

        var existing = await repo.FindBindingAsync("official", username);
        if (existing is null)
        {
            // 新用户：本次即视为注册
            var (salt, hash) = PasswordHasher.Hash(password);
            var extraJson = JsonSerializer.Serialize(new OfficialCredential(salt, hash), OfficialCredentialJsonContext.Default.OfficialCredential);
            return AuthValidationResult.Ok(username, extraJson);
        }

        if (existing.ExtraData is null)
            return AuthValidationResult.Fail("账号数据异常，请联系管理员");

        var cred = JsonSerializer.Deserialize(existing.ExtraData, OfficialCredentialJsonContext.Default.OfficialCredential);
        if (cred is null || !PasswordHasher.Verify(password, cred.Salt, cred.Hash))
            return AuthValidationResult.Fail("用户名或密码错误");

        return AuthValidationResult.Ok(username);
    }
}
