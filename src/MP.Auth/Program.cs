using MP.Auth.Endpoints;
using MP.Auth.Infrastructure.Db;
using MP.Auth.Infrastructure.Jwt;
using MP.Auth.Infrastructure.Redis;
using MP.Auth.Models;
using MP.Auth.Providers;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateSlimBuilder(args);

// =================== 【迁移入口：--migrate 或 RUN_MIGRATION_ONLY=true】 ===================
var runMigrationOnly = args.Contains("--migrate")
    || string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATION_ONLY"), "true", StringComparison.OrdinalIgnoreCase);

if (runMigrationOnly)
{
    var connStr = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("缺少 ConnectionStrings__Postgres");

    Console.WriteLine("Executing database migrations (MP.Auth)...");
    try
    {
        DbMigrator.Run(connStr);
        Console.WriteLine("Migration completed successfully.");
        return; // 只跑迁移，不启动 Web
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"DB migration failed: {ex.Message}");
        Environment.Exit(1);
    }
}
// ========================================================================================

// ---- JSON：Native AOT 走 Source Generator，零反射 ----
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

// ---- 配置读取（环境变量优先，兼容 docker-compose） ----
var pgConnStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("缺少 ConnectionStrings__Postgres");
var redisConnStr = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("缺少 ConnectionStrings__Redis");
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("缺少 Jwt__Secret");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "battle-net-mp";

// ---- DI 装配 ----
builder.Services.AddSingleton(NpgsqlDataSource.Create(pgConnStr));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnStr));
builder.Services.AddSingleton<AccountRepository>();
builder.Services.AddSingleton<CatalogRepository>();
builder.Services.AddSingleton<AuthValidatorFactory>();
builder.Services.AddSingleton<RefreshTokenStore>();
builder.Services.AddSingleton(new SimpleJwt(jwtSecret, jwtIssuer));

var app = builder.Build();

// 注意：正常启动路径不再执行迁移。迁移由 compose 的 mp-auth-migrate Job 或手动 --migrate 完成。

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "mp-auth" }));
app.MapAuthEndpoints();
app.MapCatalogEndpoints();

app.Run();
