# C# SDK ↔ 契约映射（sdk-mapping.md）

> 验收要求：「现有 C# SDK 的行为映射表：每个 SDK 方法 → OpenAPI operationId（无遗漏）」。
> 版本：contracts 1.0.0 ／ `EliCloud.Sdk` 1.0.0-alpha.3 ／ 桌面客户端 0.1。
>
> **规则**：SDK 不得出现操作表里没有的端点。新增端点 → 先改 `openapi.yaml` 并升版本。

## 1. 客户端可见端点（进契约）

| operationId | 方法 | 路径 | C# SDK 方法 | 桌面封装 |
|---|---|---|---|---|
| `getOidcDiscovery` | GET | `/auth/.well-known/openid-configuration` | `SsoClient.GetDiscoveryDocumentAsync` | — |
| `getJwks` | GET | `/auth/.well-known/jwks.json` | `SsoClient.GetJsonWebKeySetJsonAsync` / `GetJsonWebKeySetAsync` | — |
| `registerStart` | POST | `/auth/register` | `SsoClient.StartRegistrationAsync` | `EliCloudGateway.StartRegistrationAsync` |
| `getRegistrationStatus` | POST | `/auth/register/status` | `SsoClient.GetRegistrationStatusAsync` | `EliCloudGateway.GetRegistrationStatusAsync` |
| `resendRegistrationEmail` | POST | `/auth/register/resend` | `SsoClient.ResendRegistrationEmailAsync` | `EliCloudGateway.ResendRegistrationEmailAsync` |
| `cancelRegistration` | POST | `/auth/register/cancel` | `SsoClient.CancelRegistrationAsync` | `EliCloudGateway.CancelRegistrationAsync` |
| `login` | POST | `/auth/login` | `SsoClient.LoginAsync` | `EliCloudGateway.LoginAsync` |
| `refreshToken` | POST | `/auth/refresh` | `SsoClient.RefreshAsync` | —（当前不刷新） |
| `getUserInfo` | GET | `/auth/userinfo` | `SsoClient.GetUserInfoAsync(usePost: false)` | `EliCloudGateway.RefreshUserInfoAsync` |
| `postUserInfo` | POST | `/auth/userinfo` | `SsoClient.GetUserInfoAsync(usePost: true)` | — |
| `authorize` | GET | `/auth/authorize` | `SsoClient.BuildAuthorizationUrl` | 未来 |
| `token` | POST | `/auth/token` | `SsoClient.ExchangeCodeAsync` / `RefreshTokenAsync` / `PollDeviceCodeAsync` | 未来 |
| `deviceAuthorization` | POST | `/auth/device_authorization` | `SsoClient.StartDeviceAuthorizationAsync`、`DeviceCodeFlow.RunAsync` | 未来 |
| `logout` | GET | `/auth/logout` | `SsoClient.BuildLogoutUrl` | 未来 |
| `getMyProfile` | GET | `/userinfo/v1/profile` | —（不在 SDK 内） | `UserInfoApi.GetMyProfileAsync` |
| `updateMyProfile` | PUT | `/userinfo/v1/profile` | — | `UserInfoApi.UpdateMyProfileAsync` |
| `getProfile` | GET | `/userinfo/v1/profiles/{sub}` | — | `UserInfoApi.GetProfileAsync` |
| `getMyProfileImage` | GET | `/userinfo/v1/profile/{kind}` | — | `UserInfoApi.GetMyImageAsync` |
| `uploadMyProfileImage` | PUT | `/userinfo/v1/profile/{kind}` | — | `UserInfoApi.UploadMyImageAsync` |
| `deleteMyProfileImage` | DELETE | `/userinfo/v1/profile/{kind}` | — | `UserInfoApi.DeleteMyImageAsync` |
| `getProfileImage` | GET | `/userinfo/v1/profiles/{sub}/{kind}` | — | `UserInfoApi.GetProfileImageAsync` |
| `mcHealth` | GET | `/mc/healthz` | `McClient.GetHealthAsync` | `EliCloudGateway.ProbeAsync` / `GetMcHealthAsync` |
| `listMyMcNames` | GET | `/mc/v1/names` | `McClient.ListMyNamesAsync` | `EliCloudGateway.ListMyMcNamesAsync` |
| `addMcName` | POST | `/mc/v1/names` | `McClient.AddNameAsync` | `EliCloudGateway.AddMcNameAsync` |
| `removeMcName` | DELETE | `/mc/v1/names/{id}` | `McClient.RemoveNameAsync` | `EliCloudGateway.RemoveMcNameAsync` |
| `mcIdentity` | GET | `/mc/v1/me` | `McClient.GetIdentityAsync` | — |
| `listCoreServices` | GET | `/core/v1/services` | `MainApiClient.ListServicesAsync` | —（未上线，清单硬编码） |

覆盖：27 个 operation，SDK 侧 21 个方法有对应（userinfo 的 7 个端点在桌面的 `UserInfoApi` 里，
刻意不进 SDK，理由是新增 SDK 包会卡住「没有并列 SDK 仓库时走 NuGet」的独立构建路径）。

### 1.1 端点 URI 属性（同样按 operationId 归类，不允许出现表外路径）

| SDK 属性 | 拼出的路径 | operationId |
|---|---|---|
| `SsoClient.DiscoveryEndpoint` | `/.well-known/openid-configuration` | `getOidcDiscovery` |
| `SsoClient.JwksEndpoint` | `/.well-known/jwks.json` | `getJwks` |
| `SsoClient.AuthorizationEndpoint` | `/authorize` | `authorize` |
| `SsoClient.TokenEndpoint` | `/token` | `token` |
| `SsoClient.UserInfoEndpoint` | `/userinfo` | `getUserInfo` / `postUserInfo` |
| `SsoClient.DeviceAuthorizationEndpoint` | `/device_authorization` | `deviceAuthorization` |
| `SsoClient.EndSessionEndpoint` | `/logout` | `logout` |
| `SsoClient.RegisterEndpoint` | `/register` | `registerStart` |
| `SsoClient.RegisterStatusEndpoint` | `/register/status` | `getRegistrationStatus` |
| `SsoClient.RegisterResendEndpoint` | `/register/resend` | `resendRegistrationEmail` |
| `SsoClient.RegisterCancelEndpoint` | `/register/cancel` | `cancelRegistration` |
| `SsoClient.LoginEndpoint` | `/login` | `login` |
| `SsoClient.RefreshEndpoint` | `/refresh` | `refreshToken` |
| `SsoClient.ClientsEndpoint` | `/v1/clients` | **不纳入**（管理端点，见 §2） |
| `McClient.HealthEndpoint` | `/healthz` | `mcHealth` |
| `MainApiClient.ServicesEndpoint` | `/v1/services` | `listCoreServices` |

> 路径前缀（`/auth`、`/mc`、`/core`）由 `EliCloudOptions` 派生，不在上表重复。

## 2. 明确**不**进契约的 SDK 方法

这些方法存在于 SDK 里，但只给运维工具用；不进客户端契约、不生成到平台 SDK。

| SDK 方法 | 端点 | 原因 |
|---|---|---|
| `SsoAdminClient.ListClientsAsync` | `GET /auth/v1/clients` | 管理端点，需要 admin token |
| `SsoAdminClient.GetClientAsync` | `GET /auth/v1/clients/{id}` | 同上 |
| `SsoAdminClient.CreateClientAsync` | `POST /auth/v1/clients` | 同上 |
| `SsoAdminClient.UpdateClientAsync` | `PATCH/PUT /auth/v1/clients/{id}` | 同上 |
| `SsoAdminClient.DeleteClientAsync` | `DELETE /auth/v1/clients/{id}` | 同上 |
| `SsoAdminClient.RotateClientSecretAsync` | `POST /auth/v1/clients/{id}/rotate-secret` | 同上 |
| `McAdminClient.ListNamesAsync` | `GET /mc/v1/admin/names` | 管理端点，`ADMIN_TOKEN` 未配置时 403 |
| `McAdminClient.ListAuditAsync` | `GET /mc/v1/admin/audit` | 同上 |
| `McAdminClient.RemoveNameAsync` | `DELETE /mc/v1/admin/names/{id}` | 同上 |
| `McAdminClient.ReconcileAsync` | `POST /mc/v1/admin/reconcile` | 同上 |

## 3. SDK 里的传输语义映射

| SDK 类型/成员 | 契约对应 |
|---|---|
| `EliCloudApiException.StatusCode` | 响应的 HTTP 状态码 |
| `EliCloudApiException.Error` | `EliCloudError.error` |
| `EliCloudApiException.ErrorDescription` | `EliCloudError.error_description` |
| `EliCloudApiException.RetryAfter` | `Retry-After` 响应头（秒或 HTTP-date） |
| `EliCloudApiException.ResponseBody` | 原始 body（仅诊断，禁止打日志） |
| `EliCloudMalformedResponseException` | 2xx 但 body 不是契约 JSON → `error.client.invalid_response` |
| `HttpRequestException` / 超时取消 | `error.client.network_error` / `error.client.timeout` |
| `McClient.GetHealthAsync` 的 503 分支 | `mcHealth` 的 503 仍解析 `McHealthStatus` |
| `DeviceCodePollResult.IsPending/IsSlowDown/IsTerminal` | `token` 的 `authorization_pending` / `slow_down` / `access_denied`+`expired_token`+`invalid_grant` |
| `SsoClient` 的 `RegisterRequest(...)` 私有 record | `RegisterRequest`（snake_case 由 `EliCloudJson` 策略映射） |
| `SsoClient` 的 `RegistrationRef(...)` | `RegistrationRef` |
| `SsoClient` 的 `LoginRequest(...)` | `LoginRequest` |
| `SsoClient` 的 `RefreshRequest(...)` | `RefreshRequest` |
| `McClient` 的 `CreateNameRequest(...)` | `CreateNameRequest` |

## 4. 映射表维护方式

- 新增/删除 SDK 客户端方法 → 必须同时改本表与 `openapi.yaml`，并在 `CHANGELOG.md` 加一条。
- `contracts/tools/lint_contract.py` 会校验：`openapi.yaml` 的 operationId 集合 = 本表第一节的
  operationId 集合（不多不少），漏更新会直接失败。
