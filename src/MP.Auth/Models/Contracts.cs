using System.Text.Json.Serialization;

namespace MP.Auth.Models;

// provider: steam | psn | xbox | nintendo | apple | google | microsoft_store | official | guest
// 除 official / guest 已实现外，其余渠道先占位保留（NotImplementedAuthValidator），
// 后续接入哪个渠道就补一个 Validator 实现即可，不影响其他代码。
public sealed record LoginRequest(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("app_id")] string? AppId,
    [property: JsonPropertyName("device_id")] string? DeviceId,
    [property: JsonPropertyName("auth_payload")] Dictionary<string, string>? AuthPayload);

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

// ---------- Catalog ----------
public sealed record GameItem(
    [property: JsonPropertyName("game_id")] string GameId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("icon_url")] string? IconUrl,
    [property: JsonPropertyName("min_client_ver")] string? MinClientVer,
    [property: JsonPropertyName("extra_json")] string? ExtraJson);

public sealed record GameListResponse(
    [property: JsonPropertyName("games")] GameItem[] Games);

[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(RefreshRequest))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(MeResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(GameItem))]
[JsonSerializable(typeof(GameListResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
public partial class AppJsonContext : JsonSerializerContext
{
}
