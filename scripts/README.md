# MP API Smoke（补丁包）

## JWT 要不要改？

**不用改。**

MP 已是平台标准：`SimpleJwt` HS256 签发/校验，**没有** IdentityModel。  
Mail 出问题是因为业务服务误用了 IdentityModel；**以 MP 为准对齐即可**（见 ADR-0002）。

可选小债（与 JWT 无关）：

- `Program.cs` 里 `GET /health` 使用了匿名类型 `new { status = "ok", service = "mp-auth" }`。  
  在 **直连 mp-auth** 且 AOT 序列化严格时可能 500；经 **nginx 网关** 的 `/health` 是纯文本 `ok`，不走这段。  
  建议改成登记过的 DTO（与 ADR-0002 一致），但不是 JWT 问题。

## 安装到 MP 仓库

把本包的 `scripts/smoke_test.py` 拷到 MP 根下：

```text
MP/
  scripts/
    smoke_test.py
```

## 运行

```bat
cd MP/scripts/
docker compose up -d --build
python smoke_test.py
```

可选校验 Token 签名与 compose 一致：

```bat
set JWT_SECRET=你的.env里JWT_SECRET
python scripts\smoke_test.py
```

报告：`test-results/smoke-report.json`、`smoke-junit.xml`。

## 覆盖

| 类型 | 用例 |
|------|------|
| Bad | login 缺字段 400、短密码/错密 401、me 无/坏 Token 401、refresh 坏 id 400、catalog 404 |
| Happy | official 注册登录、me、再登录、refresh、catalog list |
