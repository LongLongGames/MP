using MP.Auth.Infrastructure.Db;
using MP.Auth.Infrastructure.Jwt;
using MP.Auth.Infrastructure.Redis;
using MP.Auth.Models;
using MP.Auth.Providers;

namespace MP.Auth.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapGet("/me", MeAsync);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest req,
        AccountRepository repo,
        AuthValidatorFactory validatorFactory,
        SimpleJwt jwt,
        RefreshTokenStore refreshStore,
        IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(req.Provider))
            return Results.BadRequest(new ErrorResponse("provider 不能为空"));
        if (string.IsNullOrWhiteSpace(req.DeviceId))
            return Results.BadRequest(new ErrorResponse("device_id 不能为空"));

        var validator = validatorFactory.Get(req.Provider);
        var validation = await validator.ValidateAsync(req.AuthPayload);
        if (!validation.Success)
            return Results.Json(new ErrorResponse(validation.Error ?? "登录校验失败"), statusCode: StatusCodes.Status401Unauthorized);

        var binding = await repo.FindBindingAsync(req.Provider, validation.ThirdPartyId!);
        var accountId = binding?.AccountId
            ?? await repo.CreateAccountWithBindingAsync(req.Provider, validation.ThirdPartyId!, validation.ExtraDataJson);

        var (accessToken, refreshToken, expiresIn) = await IssueTokensAsync(
            accountId, req.Provider, req.AppId, req.DeviceId!, jwt, refreshStore, config);

        return Results.Ok(new LoginResponse(accessToken, refreshToken, expiresIn, accountId.ToString()));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest req,
        AccountRepository repo,
        SimpleJwt jwt,
        RefreshTokenStore refreshStore,
        IConfiguration config)
    {
        if (!Guid.TryParse(req.MpAccountId, out var accountId))
            return Results.BadRequest(new ErrorResponse("mp_account_id 格式错误"));

        var account = await repo.FindAccountAsync(accountId);
        if (account is null)
            return Results.Json(new ErrorResponse("账号不存在"), statusCode: StatusCodes.Status401Unauthorized);

        var refreshDays = config.GetValue("Jwt:RefreshTokenDays", 14);
        var newRefreshToken = Guid.NewGuid().ToString("N");
        var rotated = await refreshStore.ValidateAndRotateAsync(
            req.MpAccountId, req.DeviceId, req.RefreshToken, newRefreshToken, TimeSpan.FromDays(refreshDays));

        if (!rotated)
            return Results.Json(new ErrorResponse("refresh_token 无效或已过期，请重新登录"), statusCode: StatusCodes.Status401Unauthorized);

        var accessMinutes = config.GetValue("Jwt:AccessTokenMinutes", 120);
        var accessToken = jwt.Issue(req.MpAccountId, "refresh", null, TimeSpan.FromMinutes(accessMinutes));

        return Results.Ok(new RefreshResponse(accessToken, newRefreshToken, accessMinutes * 60));
    }

    private static async Task<IResult> MeAsync(HttpContext ctx, AccountRepository repo, SimpleJwt jwt)
    {
        var authHeader = ctx.Request.Headers["Authorization"].ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Results.Unauthorized();

        var token = authHeader["Bearer ".Length..];
        if (!jwt.TryValidate(token, out var claims) || claims is null || !Guid.TryParse(claims.Sub, out var accountId))
            return Results.Unauthorized();

        var account = await repo.FindAccountAsync(accountId);
        if (account is null)
            return Results.Unauthorized();

        var providers = await repo.GetBoundProvidersAsync(accountId);
        return Results.Ok(new MeResponse(account.Id.ToString(), account.CreatedAt, providers));
    }

    private static async Task<(string AccessToken, string RefreshToken, int ExpiresIn)> IssueTokensAsync(
        Guid accountId, string provider, string? appId, string deviceId,
        SimpleJwt jwt, RefreshTokenStore refreshStore, IConfiguration config)
    {
        var accessMinutes = config.GetValue("Jwt:AccessTokenMinutes", 120);
        var refreshDays = config.GetValue("Jwt:RefreshTokenDays", 14);

        var accessToken = jwt.Issue(accountId.ToString(), provider, appId, TimeSpan.FromMinutes(accessMinutes));
        var refreshToken = Guid.NewGuid().ToString("N");
        await refreshStore.SetAsync(accountId.ToString(), deviceId, refreshToken, TimeSpan.FromDays(refreshDays));

        return (accessToken, refreshToken, accessMinutes * 60);
    }
}
