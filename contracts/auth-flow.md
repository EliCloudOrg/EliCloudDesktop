# 认证与注册流程（auth-flow.md）

> 适用：四端客户端。协议细节以 `openapi.yaml` 为准，本文只规定**状态机与调用顺序**。
> 版本：contracts 1.0.0。

## 1. 会话状态

客户端只需要三种状态，不要引入第四种：

| 状态 | 含义 | 判据 |
|---|---|---|
| `SignedOut` | 没有 access token | 进程启动、登出、令牌链被撤销 |
| `SignedIn` | 有 access token（可能尚未完成 `/auth/userinfo` 补全） | `access_token` 非空 |
| `Expired` | 有令牌但已过 `expires_in`（留 60 秒余量） | 本地时钟推算 |

`SignedIn` 的补全字段（用户名、邮箱、`email_verified`、`scope`、`sub`）只来自 `/auth/userinfo`；
`/auth/login` 响应里的 `user` 是**便利字段**，只用于首屏占位。

## 2. 状态机

```text
SignedOut
  ├─ 登录 ──> POST /auth/login
  │             ├─ 200  ──> 保存 access/refresh token（仅内存）
  │             │            └─> GET /auth/userinfo ──> SignedIn
  │             │                 └─ 失败：**不**踢回登录页，保持 SignedIn（令牌已到手）
  │             ├─ 401 invalid_grant ──> 提示「账号或密码不对」，留在 SignedOut
  │             ├─ 403 email_not_verified ──> 引导去「创建账号」重新提交，留在 SignedOut
  │             └─ 429 too_many_requests ──> 按 Retry-After 倒计时
  │
  └─ 注册 ──> POST /auth/register
               ├─ 202 pending（保存 registration_id、用户输入的用户名/密码/邮箱到内存）
               │    └─ 每 2 秒 POST /auth/register/status 轮询
               │         ├─ pending ──> 继续轮询（同样的 registration_id，指数退避兜底 2s→5s→10s，上限 30s）
               │         ├─ verified ──> 立刻用内存密码 POST /auth/login ──> GET /auth/userinfo ──> SignedIn
               │         ├─ expired ──> 显示 reason + 「重新发送」「取消」两个动作
               │         └─ failed  ──> 同上
               ├─ 用户点「重新发送」──> POST /auth/register/resend
               │         ├─ 202 ──> 用响应里的 resend_after 重新计时
               │         └─ 429 ──> 按 Retry-After 倒计时（服务端 1 小时窗口）
               └─ 用户点「取消」──> POST /auth/register/cancel
                        ├─ 204 ──> 回登录页
                        └─ 409 already_verified ──> 账号其实已经建好，直接去登录
```

### 2.1 必须遵守的顺序

1. **登录成功后必须拉 `/auth/userinfo`**。它失败不能撤销登录：令牌已经有效，页面最多显示「未绑定邮箱」。
2. **注册成功后不能直接登录**：必须等 `status=verified`。服务端在邮箱确认后才创建账号。
3. **`registration_id` 是 capability，不是用户 ID**：只能用于 status/resend/cancel 三个端点，不得落库、不得展示。
4. **密码只在内存**：注册期间用内存变量持有，`verified` 后立刻使用并清掉。
5. **任何凭据都不落盘**：access token、refresh token、密码均只在内存。重启后回到 `SignedOut`。

## 3. 轮询参数

| 项 | 值 | 依据 |
|---|---|---|
| 首次轮询延迟 | 2 秒 | 本契约（`01-contract-openapi.md` §5） |
| 常规间隔 | 2 秒 | 同上 |
| 退避上限 | 30 秒（2→5→10→30） | 网络不稳时的兜底，避免刷爆 120 次/分的服务端限流 |
| `resend_after > 0` | 等 `resend_after` 秒再允许重发 | 响应字段 |
| 轮询终止 | `verified` / `expired` / `failed` / 404 | 见 §4 |
| 单次请求超时 | 20 秒（普通） | `validation.md` §5 |

不允许并发轮询：同一时刻最多一个 status 请求在飞。

## 4. 错误 → 用户动作

| HTTP / error | copyKey | 客户端动作 |
|---|---|---|
| 401 `invalid_grant` | `error.auth.invalid_grant` | 留在登录页，提示；不区分账号/密码 |
| 403 `email_not_verified` | `error.auth.email_not_verified` | 提示去「创建账号」重新提交 |
| 401 `invalid_token` | `error.transport.invalid_token` | 走「重新登录」（不要反复重试） |
| 403 `insufficient_scope` | `error.transport.insufficient_scope` | 提示重新登录以重新授权 |
| 404 `registration_not_found` | `error.registration.registration_not_found` | 停止轮询，回注册页 |
| 410 `registration_expired` | `error.registration.registration_expired` | 停止轮询，允许重发/取消 |
| 409 `already_verified` | `error.registration.already_verified` | 直接去登录 |
| 409 `user_exists` / `email_exists` / `registration_pending` | 对应 key | 留在注册页，按提示修改或继续 |
| 429 | `error.transport.too_many_requests` | 读 `Retry-After`，倒计时期间禁用按钮 |
| 非 JSON 错误体 | `error.client.unknown` | 按状态码分类；不展示响应体 |

## 5. 令牌刷新

- 密码直连登录会拿到 refresh token；它**没有会话绑定**，无法主动撤销（标准 `/auth/logout` 找不到它）。
- `POST /auth/refresh` 会**轮换** refresh token：每次成功都必须用响应里的新值覆盖旧值。
- **重放旧 refresh token 会被判为窃取，整条链立刻撤销**。因此：
  - 同一时刻只允许一个刷新请求在飞（用一个共享的 `Task` 去重）；
  - 刷新失败不要再拿旧 token 重试；
  - 刷新得到 401 `invalid_grant` 时直接回 `SignedOut`。
- 建议提前 60 秒刷新（`expires_in` 折成本地绝对时间）。
- 绑定了 `client_id` 的 OIDC 令牌**不能**在 `/auth/refresh` 兑换，要走 `POST /auth/token`（`grant_type=refresh_token`）。

## 6. 登出

当前客户端是「丢弃内存里的令牌」语义：不调用服务端，也不需要登出接口。
代价：密码直连签发的 refresh 链会一直有效到过期。

```text
SignedIn --(用户点退出)--> 清空内存会话 --(取消所有在飞请求)--> SignedOut
```

未来接入 OIDC 授权码流程后，登出必须走 RP-Initiated Logout：

```text
GET /auth/logout?post_logout_redirect_uri=<已注册值>&id_token_hint=<id_token>&state=<state>
  → 302 回跳：清会话 cookie、撤销该会话派生的 refresh 链
  → 200（未注册回跳地址）：服务端渲染登出页，不跳转
```

## 7. 未来扩展（本阶段**不要求**实现）

### 7.1 授权码 + PKCE（桌面/移动端推荐路径）

```text
client 生成 state、nonce、code_verifier(43–128)
  → GET /auth/authorize?response_type=code&client_id=…&redirect_uri=…&scope=openid…&state=…&nonce=…&code_challenge=S256(verifier)&code_challenge_method=S256
      → 302 redirect_uri?code=…&state=…
  → POST /auth/token  grant_type=authorization_code&code=…&redirect_uri=…&code_verifier=…&client_id=…
      → 200 TokenResponse（含 id_token；请求 offline_access 才给 refresh_token）
```

约束：

- 公开客户端必须用 `S256`，`plain` 被拒绝。
- `redirect_uri` 必须与注册值**逐字**一致。
- `id_token.aud` 是 `client_id`，**不能**拿去访问业务 API。
- `state` 必须逐字比对；`nonce` 必须与 `id_token.nonce` 一致。

### 7.2 设备码（CLI）

```text
POST /auth/device_authorization (form: client_id, scope)
  → {device_code, user_code, verification_uri_complete, expires_in, interval}
  → 显示 verification_uri_complete，并每 interval 秒轮询：
POST /auth/token (form: grant_type=urn:ietf:params:oauth:grant-type:device_code, device_code, client_id)
  → 400 authorization_pending 继续轮询
  → 400 slow_down            interval += 5（上限 60）后继续
  → 400 access_denied / expired_token / invalid_grant → 终止
  → 200 TokenResponse
```

`authorization_pending` / `slow_down` 是**正常状态**，不要当异常弹窗。

### 7.3 `prompt=none` 与静默续期

`/auth/authorize?prompt=none` 在没有有效会话时以 `error=login_required` 回跳，客户端应改走交互式登录，
不要循环重试（会触发限流）。
