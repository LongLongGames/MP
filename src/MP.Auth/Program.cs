using System.Text.Json.Serialization.Metadata;
using MP.Auth.Endpoints;
using MP.Auth.Infrastructure.Db;
using MP.Auth.Infrastructure.Jwt;
using MP.Auth.Infrastructure.Redis;
using MP.Auth.Models;
using MP.Auth.Providers;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateSlimBuilder(args);

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
builder.Services.AddSingleton<AuthValidatorFactory>();
builder.Services.AddSingleton<RefreshTokenStore>();
builder.Services.AddSingleton(new SimpleJwt(jwtSecret, jwtIssuer));

var app = builder.Build();

// ---- 启动时自动跑数据库迁移（DbUp，幂等） ----
DbMigrator.Run(pgConnStr);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();

app.Run();
