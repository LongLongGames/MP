# MP (MiddlePlatform) — 全平台统一账号中台 v0.1

## 简介

`MP` 是 BattleNet 旗下所有游戏共用的账号与认证中台。目标：无论玩家从哪个渠道进入（Steam / 主机 / 官服 /
自己写的测试 App），最终都换取同一套 MP JWT，MS 微服务只需本地验签即可信任身份，无需回调 MP。

v0.1 只做一件事：**账号打通**。运营向功能（封禁、限流、Dashboard 等）本版不做，后面按需再加。

- **2 个服务**：`mp-gateway`（NGINX 反代，纯配置无自定义镜像）+ `mp-auth`（.NET 10 Native AOT）
- **3 个 API**：`login`（统一登录/自动注册）+ `refresh`（无感刷新）+ `me`（校验登录态）
- **已实现渠道**：`official`（用户名+密码，首次登录即注册）、`guest`（设备号游客登录）—— 这两个不依赖任何第三方平台，可以直接测试、自己写 App 接入。
- **预留渠道**（协议里占好位置，校验逻辑还没接，调用会返回 401 + "暂未接入"）：`steam` `psn` `xbox` `nintendo` `apple` `google` `microsoft_store`。以后要接哪个，写一个 `Providers/XxxAuthValidator.cs` 实现 `IAuthValidator`，在 `AuthValidatorFactory` 里换掉对应占位即可，不影响其他代码。

MS（微服务层：游戏业务、排行榜、房间匹配等）不在这版范围内，后面单独讨论设计。

## 环境依赖

- Docker & Docker Compose（本地/内网跑起来唯一需要的东西）
- 本地开发调试可选装 .NET 10 SDK
- 数据库表结构由 DbUp 在 `mp-auth` 容器启动时自动建/迁移，不需要手动建库

## 使用

```bash
cp .env.example .env
# 编辑 .env，改掉 POSTGRES_PASSWORD 和 JWT_SECRET

docker compose up -d --build
```

网关监听 `http://localhost:8080`，健康检查 `GET /health`。

发布正式版本（触发 CI 构建镜像推 GHCR）：

```bash
git tag v0.1.0
git push origin v0.1.0
```

CI 只构建推送 `mp-auth` 镜像；`mp-gateway` 直接用官方 `nginx:alpine` + 仓库里的 `nginx/nginx.conf`
挂载运行，不额外出镜像，改配置就是改一个文件、重启容器，怎么简单怎么来。

## API

统一前缀：`/api/v1/auth`（经网关是 `http://localhost:8080/api/v1/auth`）

### 1. 登录 / 自动注册 — `POST /login`

```json
{
  "provider": "official",
  "app_id": "test_app",
  "device_id": "any-string",
  "auth_payload": { "username": "alice", "password": "at-least-6-chars" }
}
```

- `provider: guest` 时 `auth_payload` 换成 `{ "device_token": "任意唯一字符串" }`。
- 未接入渠道（steam 等）返回 `401` + 错误信息，不会崩。

响应：

```json
{
  "access_token": "...",
  "refresh_token": "...",
  "expires_in": 7200,
  "mp_account_id": "a1b2c3d4-..."
}
```

### 2. 刷新 — `POST /refresh`

```json
{ "mp_account_id": "a1b2c3d4-...", "refresh_token": "...", "device_id": "any-string" }
```

### 3. 校验登录态 — `GET /me`

Header: `Authorization: Bearer <access_token>`

```json
{ "mp_account_id": "a1b2c3d4-...", "created_at": "...", "bound_providers": ["official"] }
```

## 测试账号

不需要预置种子数据 —— `official` 渠道首次用某个用户名登录就是注册，直接拿以下命令自己造一个测试账号，
发给别人测试或者自己写 App 接的时候也用这套：

```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"provider":"official","device_id":"curl-test","auth_payload":{"username":"tester1","password":"test1234"}}'
```

或者用 `guest` 渠道，连密码都不用：

```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"provider":"guest","device_id":"curl-test","auth_payload":{"device_token":"guest-001"}}'
```

## TODO

- [ ] MS 微服务（另开会话细讨论：ms-gateway / ms-user / ms-leaderboard / ms-game / ms-bugreport / ms-dashboard）
- [ ] 渠道接入：steam / psn / xbox / nintendo / apple / google / microsoft_store（各写一个 Validator）
- [ ] 账号绑定/解绑 API（把 guest 或 official 账号升级绑定到某个第三方渠道）
- [ ] 运营机制：封禁、限流、Dashboard、Prometheus/Grafana 监控（本版不做，先攒着）
- [ ] docker-compose → docker swarm → k3s 的部署清单（当前只有 compose 一套）
