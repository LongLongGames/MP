> 组织总览与进度：[LongLongGames](https://github.com/LongLongGames) · [Platform Roadmap](https://github.com/orgs/LongLongGames/projects/1)

# MP

LongLongGames 平台核心：统一账号 + 游戏 Catalog。全公司只部署一份。

**负责：** 登录 / JWT / 渠道、game_id 注册与状态  
**不负责：** 版本、资源、热更、游戏内业务

## 服务

| 服务 | 说明 |
|------|------|
| mp-gateway | Nginx（8080） |
| mp-auth | Auth + Catalog（.NET 10 AOT） |
| mp-auth-migrate | 一次性 DbUp 迁移 Job（同一镜像，`--migrate`） |

渠道：`official`、`guest`、`steam` 已可用；其余（psn/xbox/...）占位。

## 启动

```bash
cp .env.example .env
# 改 POSTGRES_PASSWORD、JWT_SECRET
docker compose up -d --build
```

启动顺序：postgres healthy → **mp-auth-migrate 成功退出** → mp-auth → gateway。

- 网关：http://localhost:11080（以 compose 端口为准）
- 健康检查：`GET /health`

### 数据库迁移

迁移已从 API 启动路径拆出，避免多副本竞态。

| 场景 | 命令 |
|------|------|
| 只跑迁移 | `docker compose run --rm mp-auth-migrate` |
| 本地调试（无 Docker） | `dotnet run --project src/MP.Auth -- --migrate` |
| 正常启动 | `docker compose up -d`（自动先 migrate） |

规范详见 [GameTemplate](https://github.com/LongLongGames/GameTemplate) 与公司 profile README。

## API

前缀：`/api/v1`

### Auth

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | /auth/login | 登录 / 自动注册 |
| POST | /auth/refresh | 刷新 Token |
| GET | /auth/me | 当前用户 |

```bash
curl -s -X POST http://localhost:11080/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{
    "provider": "official",
    "app_id": "test_app",
    "device_id": "curl-test",
    "auth_payload": { "username": "tester1", "password": "test1234" }
  }' | jq
```

#### Steam 登录

**联调（AllowDevLogin=true，默认开发环境开启）**：客户端只需传 `steam_id`，无需真实 ticket。

```bash
curl -s -X POST http://localhost:11080/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{
    "provider": "steam",
    "app_id": "480",
    "device_id": "unity-editor-1",
    "auth_payload": { "steam_id": "76561198000000000" }
  }' | jq
```

**生产**：配置 `Steam:WebApiKey` + `Steam:AppId`，关闭 `AllowDevLogin`。客户端用 Steamworks 取 Session Ticket（hex），放入 `auth_payload.ticket`：

```json
{
  "provider": "steam",
  "app_id": "你的SteamAppId",
  "device_id": "device-xxx",
  "auth_payload": {
    "ticket": "HEX_FROM_GetAuthSessionTicket_OR_GetAuthTicketForWebApi",
    "steam_id": "7656119...（可选交叉校验）",
    "app_id": "可选覆盖"
  }
}
```

服务端调用 `ISteamUserAuth/AuthenticateUserTicket`，以返回的 64-bit SteamID 作为 `third_party_id` 绑定账号。

环境变量（见 `.env.example`）：`STEAM_WEB_API_KEY` / `STEAM_APP_ID` / `STEAM_ALLOW_DEV_LOGIN`。

### Catalog

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | /catalog/games | 列表（可选 `?status=active`） |
| GET | /catalog/games/{game_id} | 详情 |

新游戏在平台侧写入 `games` 表，游戏后端不要自注册。

## 发布

```bash
git tag v0.2.1
git push origin v0.2.1
```

镜像以 CI 推送至 `ghcr.io/longlonggames/mp/...` 为准。

## 技术

.NET 10 Native AOT · Npgsql · DbUp · JWT HS256（与游戏共用 Secret）
