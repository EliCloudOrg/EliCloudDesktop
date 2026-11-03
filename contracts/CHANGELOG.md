# 契约变更记录

版本号在 `contracts/VERSION`（语义化版本）。任何端点/字段/错误码/文案 key 的变化都必须先改这里，
再改 `openapi.yaml` / `errors.json` / `copy/zh-CN.json` / `fixtures/`，最后同步四端。

## 1.0.0 — 2026-11-01

首个版本。基线来自现有实现，不是设计草案：

| 来源 | 用途 |
|---|---|
| `EliCloud.Sdk/README.md` §1 端点表 | 端点/方法/路径 |
| `EliCloud.Sdk/src/EliCloud.Sdk/Sso/SsoModels.cs` | SSO DTO 与状态枚举 |
| `EliCloud.Sdk/src/EliCloud.Sdk/Sso/SsoClient.cs` | 请求编码、状态码、设备码/刷新语义 |
| `EliCloud.Sdk/src/EliCloud.Sdk/Mc/McModels.cs`、`McClient.cs` | 白名单/健康/幂等 |
| `EliCloudDesktop/src/EliCloudDesktop/Services/EliCloudGateway.cs` | 客户端实际调用面与超时 |
| `EliCloudDesktop/src/EliCloudDesktop/Services/UserInfoApi.cs` | 资料/图片端点、multipart |
| `EliCloudDesktop/src/EliCloudDesktop/Core/EliCloudErrorText.cs` | 错误码 → 中文文案 |
| `sso/app/routers/*.py`、`userinfo/app/routers/*.py`、`mc/app/routers/*.py` | 服务端真实状态码与字段（以代码为准） |

### 相对 `01-contract-openapi.md` §2 表格的校正（以服务端实现为准）

| 端点 | 文档写法 | 契约采用 | 依据 |
|---|---|---|---|
| `POST /auth/register/resend` | 200 | **202** | `sso/app/routers/registration.py` 的 `status_code=202` |
| `POST /auth/register/cancel` | 200 空/JSON | **204** | 同上，`status_code=204` 无 body |
| `POST /mc/v1/names` | 200 | **201**（首次）/ **200**（幂等） | `mc/app/routers/names.py` |
| `DELETE /userinfo/v1/profile/{kind}` | 204/200 | **200** + `{deleted, changed}` | `userinfo/app/routers/profiles.py` 返回 JSON 体 |
| `GET /userinfo/v1/profile/{kind}` | 200 binary | 200 binary **+ 304** | 图片接口带 `ETag` 与条件请求 |
| `POST /auth/login` 字段 `username` | 用户名 | 用户名**或邮箱**（服务端已支持） | `sso/app/authentication.py` |
| `RegistrationStatus.status` | pending/verified/expired/failed | 同上（`cancelled` 按不存在处理，不出现） | `sso/app/registration.py` |

### 明确**不**纳入客户端契约

- `/auth/v1/clients*`（`SsoAdminClient`）—— 管理端点，绝不生成到客户端 SDK。
- `/mc/v1/admin/*`（`McAdminClient`）—— 管理端点，`ADMIN_TOKEN` 未配置时整组 403。
- `/auth/internal/email/verification` —— 服务间回调。
- `/userinfo/healthz`、`/mc/healthz` 之外的探活端点 —— 客户端只探 `GET /mc/healthz`。

### 未决 / 已知缺口

1. `GET /userinfo/v1/me`、`GET /userinfo/healthz` 服务端已实现但客户端不用，未进契约；
   若哪天要用，先在这里加版本记录。
2. `GET /core/v1/services` 标为 `x-elicloud-status: planned`，服务未上线；客户端仍硬编码清单。
3. `POST /auth/authorize`（登录页表单）是 HTML 语义，客户端不实现，故不纳入契约。
4. `GET /auth/device` 是浏览器交互页，同上不纳入。
5. 错误信封没有机器可读的 `details` 字段；所有服务的 400（参数校验失败）都会额外带
   `details: [{loc,msg,type}]`，本版本按「非契约字段」处理，客户端不得依赖。
