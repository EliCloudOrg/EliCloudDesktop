# EliCloud 客户端契约（contracts/）

> Stage 1 交付物：OpenAPI 3.1、错误码目录、四端共用文案、可回放 fixtures、零依赖 mock server、
> 契约自检与 C# SDK 契约测试。
>
> **本目录是唯一协议源。** 四个平台与各语言 SDK 只能依赖这里的端点、字段、错误码；
> 任何变化先改这里，再升 `VERSION`，最后同步各端。

- 契约版本：`1.0.0`（见 `VERSION`）
- 基线来源与变更记录：`CHANGELOG.md`
- 端点 ↔ C# SDK 方法映射：`sdk-mapping.md`
- 认证状态机：`auth-flow.md`
- 校验/超时/图片规则：`validation.md`
- 错误码：`errors.json`；中文文案：`copy/zh-CN.json`

## 1. 目录

```text
contracts/
├── VERSION                  # 1.0.0
├── CHANGELOG.md             # 来源、校正、未决问题
├── openapi.yaml             # OpenAPI 3.1.0，27 个 operation
├── errors.json              # 49 个错误码（含分类/copyKey/retryable）
├── copy/zh-CN.json          # 49 条中文文案（只做 key → 字符串）
├── auth-flow.md             # 登录/注册/刷新/登出/未来 PKCE+设备码
├── validation.md            # 昵称/简介/MC/图片/超时/探活规则
├── sdk-mapping.md           # operationId ↔ SDK 方法（无遗漏）
├── mock_server.py           # 按 fixture 回放，仅标准库
├── tools/lint_contract.py   # 离线契约自检
├── tests/csharp/            # C# SDK 契约测试（fixture 驱动）
└── fixtures/                # 55 个 JSON fixture + 2 张图片 + 1 个非 JSON 错误体
    ├── auth/  register/  userinfo/  mc/  core/  errors/
```

## 2. 快速开始

```powershell
# 1) 契约自检（离线，零依赖；只要求 Python 3.10+ 与 PyYAML）
python contracts\tools\lint_contract.py

# 2) 起 mock server（默认 127.0.0.1:4010）
python contracts\mock_server.py
curl.exe -s http://127.0.0.1:4010/auth/login -X POST -H "Content-Type: application/json" -d "{\"username\":\"a\",\"password\":\"b\"}"

# 3) C# SDK 契约测试（会构建 contracts/tests/csharp）
dotnet run --project contracts\tests\csharp\EliCloud.ContractTests.csproj
```

当前结果（2026-11-01）：

```text
python contracts\tools\lint_contract.py
  · 端点覆盖：27 / 27
  · 错误码：49 个，文案：49 条
  · examples：206 个通过 schema 校验
  · fixtures：55 个，覆盖 27 / 27 个 operation
  · sdk 映射：27 个 operationId
  通过：2009 项检查，contracts 1.0.0

dotnet run --project contracts\tests\csharp\EliCloud.ContractTests.csproj
  契约测试：57/57 通过，0 失败
```

### 2.1 mock server 用法

```powershell
python contracts\mock_server.py                              # 4010
python contracts\mock_server.py --port 4010 --strict-auth     # 受保护端点必须带 Bearer
python contracts\mock_server.py --delay-ms 3000              # 全局变慢，测客户端超时
```

| 能力 | 用法 |
|---|---|
| 指定 fixture | `?__fixture=auth/login-invalid-grant` 或请求头 `X-Fixture: errors/retry-after` |
| 每次请求额外延迟 | `?__delay_ms=5000` |
| 列出全部 fixture | `GET /__fixtures` |
| 服务自身状态 | `GET /__healthz` |
| 原样取契约 | `GET /__openapi` |
| 严格鉴权 | `--strict-auth`（返回 401 `invalid_token`） |

回放完全来自 `fixtures/`：状态码、`Retry-After`、非 JSON 错误体、503 健康快照、
`304 + ETag`、二进制图片都是 fixture 内容，没有第二套 mock 逻辑。

> 也可以按 Stage 1 的原始建议用 Prism：
> `npx @stoplight/prism-cli mock contracts/openapi.yaml -p 4010`。
> 但 Prism 需要联网装包，且对 `503 + 完整 body`、`304`、非 JSON 错误体的回放不如 fixtures 精确，
> 所以本仓库的默认路径是自带 mock server。

### 2.2 fixture 格式

```json
{
  "name": "auth/login-invalid-grant.json",
  "operationId": "login",
  "contractVersion": "1.0.0",
  "request": {
    "method": "POST",
    "path": "/auth/login",
    "headers": { "Content-Type": "application/json" },
    "query": {},
    "json": { "username": "alice", "password": "wrong-password" }
  },
  "response": {
    "status": 401,
    "headers": { "Content-Type": "application/json" },
    "json": { "error": "invalid_grant", "error_description": "账号或密码错误" }
  },
  "expected": {
    "errorCode": "invalid_grant",
    "copyKey": "error.auth.invalid_grant"
  }
}
```

- 请求体三选一：`json` / `form` / `multipart`。
- 响应体四选一：`json` / `raw`（内联文本）/ `rawFile`（引用同目录文件）/ `file`（二进制，配 `expected.binary`）。
- `expected` 可带：`errorCode`、`copyKey`、`retryAfterSeconds`、`malformed`（2xx 但 body 不是合法 JSON）、
  `nonJson`（错误体不是 JSON）、`binary`（二进制文件名）。
- **禁止**写真实入口地址：只用 `api.example.com` / `desktop.example.com`（linter 会拦）。

## 3. 版本与变更流程

1. 改契约的人先开 issue/ADR，说明动机与影响面（端点、字段、错误码、文案、fixtures）。
2. 改 `openapi.yaml` / `errors.json` / `copy/zh-CN.json` / `fixtures/`，跑通 §2 的三条命令。
3. 按语义化版本升 `VERSION`，并在 `CHANGELOG.md` 记录：
   - 破坏了什么、影响哪些端；
   - 基线来自哪个服务端版本/commit（字段以真实实现为准）。
4. 平台 owner + 契约 owner 双人 review 后合并；合并后四端必须同时更新契约测试。
5. **破坏性变更升 major**；新增可选字段/新端点升 minor；文案/示例修正升 patch。

约定：

- `contracts/openapi.yaml` 是唯一协议源；SDK 里出现契约之外的端点就是 bug。
- 错误码 → 文案只在 `errors.json` + `copy/zh-CN.json` 维护，四端不得各自维护一份。
- fixtures 是四端共同输入：改 fixture 必须四端同时跑契约测试。

## 4. CI 建议（工具版本必须 pin）

本机与 CI 的离线自检用 `lint_contract.py`；联网环境再跑官方 lint：

```yaml
- run: python contracts/tools/lint_contract.py
- run: npx --yes @redocly/cli@1.34.2 lint contracts/openapi.yaml
- run: npx --yes @stoplight/spectral-cli@6.15.0 lint contracts/openapi.yaml
- run: npx --yes @openapitools/openapi-generator-cli@2.20.0 validate -i contracts/openapi.yaml
```

> 不要用 `latest`：契约检查会因为工具升级而漂移。
> `@redocly/cli` 对 OpenAPI 3.1 的 `type: [string, "null"]` 支持需要 ≥ 1.20；本仓库按 1.34.x 固定。

## 5. Stage 1 验收对照

| 验收项 | 结果 | 证据 |
|---|---|---|
| `openapi.yaml` 覆盖 §2 所有客户端端点（不含 admin） | ✅ 27/27 | `lint_contract.py` 的 `EXPECTED_OPERATIONS` 与 `paths` 双向比对 |
| 每个端点有 request/response schema、错误响应、至少一个 example | ✅ 206 个 example | linter 逐 operation 检查 `requestBody.content` / `responses` / `example(s)`，并把每个 example 反验 schema |
| `components.schemas` 覆盖 §3 所有 DTO，字段与现有 JSON 一致 | ✅ 32 个 schema | 字段名 snake_case 校验 + fixture 按 schema 校验 |
| `errors.json` 覆盖客户端可见错误码，copyKey 都在 `copy/zh-CN.json` | ✅ 49/49 | linter 双向检查，且文案不含错误码/路径/RCON |
| `fixtures/` 覆盖 §7，mock server 能回放 | ✅ 55 个 | fixture schema 校验 + mock server 冒烟 |
| `VERSION` = `1.0.0`；`CHANGELOG.md` 记录来源/变更 | ✅ | `VERSION`、`CHANGELOG.md` |
| C# SDK 行为映射表，每个方法 → operationId（无遗漏） | ✅ 27 个 operation | `sdk-mapping.md`；linter 比对 operationId 集合 |
| 契约测试骨架可运行；至少 C# SDK 对 fixtures 通过 | ✅ 57/57 | `dotnet run --project contracts/tests/csharp` |
| 文档：`auth-flow.md`、`validation.md`、`errors.json`、`copy/zh-CN.json` | ✅ | 同名文件 |
| 没有把 admin 端点/token 写进客户端契约 | ✅ | linter 拦 `/admin`、`/v1/clients`、`/internal`、`rotate-secret` |
| 没有把入口地址写死进 fixtures/示例 | ✅ | linter 只允许 `*.example.com` |

## 6. 交接包（Stage 2/平台）

- **契约版本**：`1.0.0`，路径 `contracts/`（本目录）。
- **operationId ↔ SDK 方法映射**：`sdk-mapping.md` §1。
- **fixtures 清单 + mock 启动命令**：本文件 §2.1，或 `GET http://127.0.0.1:4010/__fixtures`。
- **错误码/文案覆盖报告**：`python contracts/tools/lint_contract.py` 的输出（49 个码 / 49 条文案）。
- **未决问题**：
  1. `GET /core/v1/services` 标为 planned，服务未上线；Stage 2 不要把它当稳定依赖。
  2. `/core/v1/services` 目前无鉴权；SDK 支持可选 Bearer，契约按匿名声明。
  3. userinfo 的 `/v1/me` 与 `/healthz` 不在客户端契约内（客户端不用）。
  4. 所有服务的 400（参数校验失败）会额外带 `details: [{loc,msg,type}]`，本版本按非契约字段处理。
  5. 错误信封没有机器可读的子错误码；需要更细的字段级错误时，走新 major。
- **契约变更流程**：本文件 §3。

## 7. 安全与边界

- 凭据（密码、access/refresh token）只在内存；fixtures 里的 `access_token` 全是不可用的假串。
- 契约/示例/fixtures 里只出现 `*.example.com`，不含任何真实入口。
- 错误文案不出现错误码、接口路径、`scope`、`RCON`、主机名/端口；`rcon.endpoint` 明确标了「不展示给用户」。
- 不生成完整客户端：只生成 DTO/端点，行为层（轮询、轮换、图片裁切、重试）仍需各端手写。
