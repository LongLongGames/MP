using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace MP.Auth.Providers;

/// <summary>
/// provider = steam：用 Steam Session Ticket 走 ISteamUserAuth/AuthenticateUserTicket 校验。
/// <para>
/// 客户端约定 auth_payload：
/// <list type="bullet">
///   <item>ticket（推荐）：GetAuthSessionTicket / GetAuthTicketForWebApi 得到的二进制 ticket 转成十六进制字符串</item>
///   <item>steam_id（可选）：64 位 SteamID，生产环境仅作交叉校验；联调模式下可单独使用</item>
///   <item>app_id（可选）：覆盖配置里的 Steam:AppId</item>
/// </list>
/// </para>
/// <para>
/// 配置：
/// <list type="bullet">
///   <item>Steam:WebApiKey —— Publisher / Web API Key（生产必填）</item>
///   <item>Steam:AppId —— 默认 AppID（可被 payload / 请求级 app_id 覆盖）</item>
///   <item>Steam:AllowDevLogin —— true 时允许无 ticket、仅 steam_id 登录，方便 Unity 客户端联调（默认 false）</item>
///   <item>Steam:Identity —— 可选，对应 GetAuthTicketForWebApi 的 identity 参数</item>
/// </list>
/// </para>
/// </summary>
public sealed class SteamAuthValidator(IConfiguration config, HttpClient http) : IAuthValidator
{
    private const string SteamAuthUrl = "https://api.steampowered.com/ISteamUserAuth/AuthenticateUserTicket/v1/";

    public async Task<AuthValidationResult> ValidateAsync(Dictionary<string, string>? payload)
    {
        if (payload is null)
            return AuthValidationResult.Fail("auth_payload 不能为空");

        var allowDev = config.GetValue("Steam:AllowDevLogin", false);
        var hasTicket = payload.TryGetValue("ticket", out var ticket) && !string.IsNullOrWhiteSpace(ticket);
        var hasSteamId = payload.TryGetValue("steam_id", out var steamId) && !string.IsNullOrWhiteSpace(steamId);

        // 联调模式：仅 steam_id，不走 Steam 服务器
        if (!hasTicket && allowDev)
        {
            if (!hasSteamId)
                return AuthValidationResult.Fail("联调模式需要 auth_payload.steam_id（或提供 ticket）");

            steamId = steamId!.Trim();
            if (!IsValidSteamId64(steamId))
                return AuthValidationResult.Fail("steam_id 格式无效，应为 64 位数字 SteamID");

            return AuthValidationResult.Ok(steamId, """{"dev_login":true}""");
        }

        if (!hasTicket)
            return AuthValidationResult.Fail("auth_payload.ticket 不能为空（十六进制 Session Ticket）");

        var apiKey = config["Steam:WebApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return AuthValidationResult.Fail("服务端未配置 Steam:WebApiKey，无法校验 ticket；联调可设 Steam:AllowDevLogin=true 并用 steam_id");

        // AppId：payload > 配置
        string? appIdStr = null;
        if (payload.TryGetValue("app_id", out var payloadAppId) && !string.IsNullOrWhiteSpace(payloadAppId))
            appIdStr = payloadAppId.Trim();
        else
            appIdStr = config["Steam:AppId"];

        if (string.IsNullOrWhiteSpace(appIdStr) || !uint.TryParse(appIdStr, out var appId) || appId == 0)
            return AuthValidationResult.Fail("缺少有效的 Steam AppId（auth_payload.app_id 或 Steam:AppId）");

        ticket = ticket!.Trim();
        // 允许客户端带 0x 前缀或空格
        if (ticket.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            ticket = ticket[2..];
        ticket = ticket.Replace(" ", "", StringComparison.Ordinal);

        if (ticket.Length < 16 || !IsHex(ticket))
            return AuthValidationResult.Fail("ticket 必须是有效的十六进制字符串");

        try
        {
            var query = new Dictionary<string, string?>
            {
                ["key"] = apiKey,
                ["appid"] = appId.ToString(),
                ["ticket"] = ticket,
            };

            var identity = config["Steam:Identity"];
            if (!string.IsNullOrWhiteSpace(identity))
                query["identity"] = identity;

            var url = SteamAuthUrl + "?" + string.Join("&",
                query.Where(kv => kv.Value is not null)
                     .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

            using var resp = await http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return AuthValidationResult.Fail($"Steam API HTTP {(int)resp.StatusCode}: {Truncate(body, 200)}");

            var parsed = System.Text.Json.JsonSerializer.Deserialize(body, SteamJsonContext.Default.SteamAuthTicketResponse);
            if (parsed?.Response is null)
                return AuthValidationResult.Fail("Steam API 返回无法解析");

            if (parsed.Response.Error is { } err)
                return AuthValidationResult.Fail($"Steam ticket 无效: [{err.ErrorCode}] {err.ErrorDesc ?? "unknown"}");

            var p = parsed.Response.Params;
            if (p is null || !string.Equals(p.Result, "OK", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(p.SteamId))
                return AuthValidationResult.Fail("Steam ticket 校验失败（无有效 steamid）");

            // 若客户端同时传了 steam_id，做交叉校验
            if (hasSteamId && !string.Equals(steamId!.Trim(), p.SteamId, StringComparison.Ordinal))
                return AuthValidationResult.Fail("auth_payload.steam_id 与 ticket 对应的 SteamID 不一致");

            var extra = System.Text.Json.JsonSerializer.Serialize(
                new SteamExtraData(p.OwnerSteamId, p.VacBanned, p.PublisherBanned),
                SteamJsonContext.Default.SteamExtraData);

            return AuthValidationResult.Ok(p.SteamId, extra);
        }
        catch (TaskCanceledException)
        {
            return AuthValidationResult.Fail("请求 Steam API 超时");
        }
        catch (HttpRequestException ex)
        {
            return AuthValidationResult.Fail($"请求 Steam API 失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AuthValidationResult.Fail($"Steam 校验异常: {ex.Message}");
        }
    }

    private static bool IsValidSteamId64(string s)
    {
        // 标准 64-bit SteamID 约 17 位，以 7656119 开头
        if (s.Length is < 15 or > 20) return false;
        foreach (var c in s)
            if (c is < '0' or > '9') return false;
        return true;
    }

    private static bool IsHex(string s)
    {
        foreach (var c in s)
        {
            if (c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'))
                continue;
            return false;
        }
        return true;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}

// ---------- Steam API JSON（AOT Source Generator） ----------

internal sealed record SteamAuthTicketResponse(
    [property: JsonPropertyName("response")] SteamAuthTicketBody? Response);

internal sealed record SteamAuthTicketBody(
    [property: JsonPropertyName("params")] SteamAuthTicketParams? Params,
    [property: JsonPropertyName("error")] SteamAuthTicketError? Error);

internal sealed record SteamAuthTicketParams(
    [property: JsonPropertyName("result")] string? Result,
    [property: JsonPropertyName("steamid")] string? SteamId,
    [property: JsonPropertyName("ownersteamid")] string? OwnerSteamId,
    [property: JsonPropertyName("vacbanned")] bool VacBanned,
    [property: JsonPropertyName("publisherbanned")] bool PublisherBanned);

internal sealed record SteamAuthTicketError(
    [property: JsonPropertyName("errorcode")] int ErrorCode,
    [property: JsonPropertyName("errordesc")] string? ErrorDesc);

internal sealed record SteamExtraData(
    [property: JsonPropertyName("owner_steamid")] string? OwnerSteamId,
    [property: JsonPropertyName("vac_banned")] bool VacBanned,
    [property: JsonPropertyName("publisher_banned")] bool PublisherBanned);

[JsonSerializable(typeof(SteamAuthTicketResponse))]
[JsonSerializable(typeof(SteamExtraData))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal partial class SteamJsonContext : JsonSerializerContext
{
}
