using System.Text.Json.Serialization;

namespace MP.Auth.Models;

// provider: steam | psn | xbox | nintendo | apple | google | microsoft_store | official | guest
// 除 official / guest 已实现外，其余渠道先占位保留（NotImplementedAuthValidator），
// 后续接入哪个渠道就补一个 Validator 实现即可，不影响其他代码。
public sealed record LoginRequest(
    string Provider,
    string? AppId,
    string? DeviceId,
    Dictionary<string, string>? AuthPayload);

public sealed record LoginResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("mp_account_id")] string MpAccountId);

public sealed record RefreshRequest(
    [property: JsonPropertyName("mp_account_id")] string MpAccountId,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("device_id")] string DeviceId);

public sealed record RefreshResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public sealed record MeResponse(
    [property: JsonPropertyName("mp_account_id")] string MpAccountId,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("bound_providers")] string[] BoundProviders);

public sealed record ErrorResponse(string Error);

[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(RefreshRequest))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(MeResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
public partial class AppJsonContext : JsonSerializerContext
{
}
