# MP (MiddlePlatform) — 全平台统一账号 + 游戏注册中台 v0.2

## 简介

`MP` 是旗下所有游戏共用的**平台核心**：

1. **账号打通**：无论玩家从哪个渠道进入（Steam / 主机 / 官服 / 测试 App），最终都换取同一套 MP JWT。游戏后端只需本地验签即可信任身份，无需回调 MP。
2. **游戏注册表（Catalog）**：全局唯一的 `game_id` 治理 + 轻量状态（active / maintenance / offline）。大厅、GameHub、启动器统一从这里拉列表。

**不负责**：版本发布、资源包、热更 URL、游戏内业务。这些全部下放到各 `game-xxx` 独立服务。

- **服务**：`mp-gateway`（NGINX）+ `mp-auth`（.NET 10 Native AOT，同时承载 Auth + Catalog）
- **已实现渠道**：`official`（用户名+密码，首次登录即注册）、`guest`（设备号游客登录）
- **预留渠道**：`steam` `psn` `xbox` `nintendo` `apple` `google` `microsoft_store`（占位，调用返回 401）

## 环境依赖

- Docker & Docker Compose
- 本地开发可选装 .NET 10 SDK
- 表结构由 DbUp 在启动时自动迁移

## 使用

```bash
cp .env.example .env
# 编辑 .env，改掉 POSTGRES_PASSWORD 和 JWT_SECRET

docker compose up -d --build
```

网关：`http://localhost:8080`  
健康检查：`GET /health`

发布正式版本：

```bash
git tag v0.2.0
git push origin v0.2.0
```

## API

统一前缀经网关：`http://localhost:8080/api/v1/...`

### Auth（原有）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/auth/login` | 登录 / 自动注册 |
| POST | `/auth/refresh` | 刷新 Token |
| GET  | `/auth/me` | 校验登录态（需 Bearer） |

登录示例：

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

### Catalog（新增）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/catalog/games` | 游戏列表（可选 `?status=active`） |
| GET | `/catalog/games/{game_id}` | 单个游戏详情 |

示例：

```bash
# 拉全部
curl -s http://localhost:8080/api/v1/catalog/games | jq

# 只拉 active
curl -s 'http://localhost:8080/api/v1/catalog/games?status=active' | jq

# 单个
curl -s http://localhost:8080/api/v1/catalog/games/match3 | jq
```

响应示例：

```json
{
  "games": [
    {
      "game_id": "match3",
      "name": "三消",
      "status": "active",
      "sort_order": 10,
      "icon_url": null,
      "min_client_ver": null,
      "extra_json": null
    }
  ]
}
```

预置 `game_id`：`match3` / `bubble` / `flying-chess` / `sudoku`。  
新游戏由平台侧手动插入 `games` 表（不要让游戏后端自动注册）。

## 架构边界（重要）

```
MP（本仓库，总公司运维，唯一）
├── Auth        → 账号 / JWT / 渠道
└── Catalog     → game_id 注册 + 状态（轻量、基本固定）

GameTemplate（原 MS 改名，模板，非运行时）
└── 各游戏 clone 后独立部署、独立 DB

game-xxx（各项目组独立仓库 + 独立镜像 + 独立 DB）
└── 玩法 / 存档 / 版本更新 / 资源 / 邮件 / BugReport …
```

Catalog **只做治理**，不发版本、不管资源。版本检查与资源下载由各游戏自己的服务负责。

## 技术说明

- .NET 10 Native AOT
- Npgsql + DbUp（嵌入式 SQL 迁移）
- SimpleJwt（HS256，与游戏后端共用同一 Secret）
- 无 EF Core，手写 SQL，AOT 友好

## TODO

- [ ] 渠道接入：steam / psn / xbox …
- [ ] 账号绑定/解绑
- [ ] Catalog 管理接口（仅内部/GM，注册/改状态）
- [ ] 封禁、限流、Dashboard
- [ ] compose → swarm → k3s
