using MP.Auth.Infrastructure.Db;
using MP.Auth.Models;

namespace MP.Auth.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/catalog");

        // 公开：大厅 / GameHub / 启动器 拉游戏列表
        group.MapGet("/games", ListGamesAsync);

        // 公开：单个游戏详情
        group.MapGet("/games/{gameId}", GetGameAsync);
    }

    private static async Task<IResult> ListGamesAsync(
        CatalogRepository repo,
        string? status = null)   // 可选过滤：active / maintenance / offline
    {
        var games = await repo.ListAsync(status);
        var items = games.Select(g => new GameItem(
            g.GameId, g.Name, g.Status, g.SortOrder,
            g.IconUrl, g.MinClientVer, g.ExtraJson)).ToArray();

        return Results.Ok(new GameListResponse(items));
    }

    private static async Task<IResult> GetGameAsync(string gameId, CatalogRepository repo)
    {
        var g = await repo.FindAsync(gameId);
        if (g is null)
            return Results.Json(new ErrorResponse("game_id 不存在"), statusCode: StatusCodes.Status404NotFound);

        return Results.Ok(new GameItem(
            g.GameId, g.Name, g.Status, g.SortOrder,
            g.IconUrl, g.MinClientVer, g.ExtraJson));
    }
}
