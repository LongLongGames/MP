namespace MP.Auth.Providers;

/// <summary>provider = guest：device_token 直接作为 third_party_id，下载即玩、无需注册。</summary>
public sealed class GuestAuthValidator : IAuthValidator
{
    public Task<AuthValidationResult> ValidateAsync(Dictionary<string, string>? payload)
    {
        if (payload is null || !payload.TryGetValue("device_token", out var deviceToken) || string.IsNullOrWhiteSpace(deviceToken))
            return Task.FromResult(AuthValidationResult.Fail("auth_payload.device_token 不能为空"));

        return Task.FromResult(AuthValidationResult.Ok(deviceToken));
    }
}
