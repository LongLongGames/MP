using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MP.Auth.Infrastructure.Jwt;

public sealed record JwtClaims(
    [property: JsonPropertyName("iss")] string Iss,
    [property: JsonPropertyName("sub")] string Sub,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("app_id")] string? AppId,
    [property: JsonPropertyName("iat")] long Iat,
    [property: JsonPropertyName("exp")] long Exp);

[JsonSerializable(typeof(JwtClaims))]
internal partial class JwtJsonContext : JsonSerializerContext
{
}

/// <summary>
/// 极简 HS256 JWT 实现：header.payload.signature，全平台共享 Secret 对称密钥验签。
/// MS 微服务侧只需同样的 Secret + 这套 ~30 行逻辑即可本地验签，无需依赖 MP 或引入重库。
/// </summary>
public sealed class SimpleJwt(string secret, string issuer)
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(secret);

    public string Issue(string mpAccountId, string provider, string? appId, TimeSpan lifetime)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new JwtClaims(issuer, mpAccountId, provider, appId,
            now.ToUnixTimeSeconds(), now.Add(lifetime).ToUnixTimeSeconds());

        var header = Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}"""u8.ToArray());
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims, JwtJsonContext.Default.JwtClaims));
        var signingInput = $"{header}.{payload}";
        var signature = Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(signingInput)));
        return $"{signingInput}.{signature}";
    }

    public bool TryValidate(string token, out JwtClaims? claims)
    {
        claims = null;
        var parts = token.Split('.');
        if (parts.Length != 3) return false;

        var signingInput = $"{parts[0]}.{parts[1]}";
        var expectedSig = Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(signingInput)));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSig), Encoding.UTF8.GetBytes(parts[2])))
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize(Base64UrlDecode(parts[1]), JwtJsonContext.Default.JwtClaims);
            if (parsed is null) return false;
            if (parsed.Exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
            claims = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
