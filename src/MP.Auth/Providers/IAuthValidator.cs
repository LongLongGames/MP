namespace MP.Auth.Providers;

public sealed record AuthValidationResult(bool Success, string? ThirdPartyId, string? ExtraDataJson, string? Error)
{
    public static AuthValidationResult Ok(string thirdPartyId, string? extraDataJson = null) =>
        new(true, thirdPartyId, extraDataJson, null);

    public static AuthValidationResult Fail(string error) => new(false, null, null, error);
}

/// <summary>
/// 每个 provider 对应一个实现。职责：校验 auth_payload 合法性，返回该渠道下的唯一身份标识（third_party_id）。
/// 账号"查找已绑定 / 自动注册新绑定"的通用逻辑统一放在 AuthEndpoints 里，Validator 不关心账号表。
/// </summary>
public interface IAuthValidator
{
    Task<AuthValidationResult> ValidateAsync(Dictionary<string, string>? payload);
}
