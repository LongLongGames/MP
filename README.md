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

渠道：`official`、`guest` 已可用；Steam 等占位。

## 启动

```bash
cp .env.example .env
# 改 POSTGRES_PASSWORD、JWT_SECRET
docker compose up -d --build
```

- 网关：http://localhost:8080
- 健康检查：`GET /health`

## API

前缀：`/api/v1`

### Auth

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | /auth/login | 登录 / 自动注册 |
| POST | /auth/refresh | 刷新 Token |
| GET | /auth/me | 当前用户 |

```bash
curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{
    "provider": "official",
    "app_id": "test_app",
    "device_id": "curl-test",
    "auth_payload": { "username": "tester1", "password": "test1234" }
  }' | jq
```

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
