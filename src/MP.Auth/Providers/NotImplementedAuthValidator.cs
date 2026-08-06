namespace MP.Auth.Providers;

/// <summary>
/// 渠道预留占位：provider 名字已经在协议/枚举里占好坑，但校验逻辑还没接。
/// 以后要接哪个渠道，新写一个 XxxAuthValidator 实现 IAuthValidator，
/// 在 AuthValidatorFactory 里把这里的占位换成新实现即可，不影响其他代码。
/// </summary>
public sealed class NotImplementedAuthValidator(string provider) : IAuthValidator
{
    public Task<AuthValidationResult> ValidateAsync(Dictionary<string, string>? payload) =>
        Task.FromResult(AuthValidationResult.Fail($"provider '{provider}' 暂未接入，敬请期待"));
}
