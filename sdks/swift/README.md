# EliCloudSDK（Swift）

`contracts/openapi.yaml` 的 Swift 实现：**DTO/端点由生成器产出，行为层手写**。

```text
sdks/swift/
├── Package.swift
├── Sources/EliCloudSDK/
│   ├── Generated/            # 生成物（入库，勿手改）
│   │   ├── Types.swift       # components.schemas → Sendable 类型 + CodingKeys
│   │   └── Client.swift      # 每个 operationId 一个 async throws 方法
│   ├── EliCloudClient.swift  # 门面：baseURL / UA / transport / session
│   ├── SsoClient.swift       # 登录、注册轮询、userinfo、refresh、退出
│   ├── UserInfoClient.swift  # 资料读写、头像/头图（multipart + 二进制）
│   ├── McClient.swift        # 健康、白名单增删查、身份自检
│   ├── Session/              # SessionState + actor SessionStore（只存内存）
│   ├── Transport/            # EliCloudTransport（URLSession）、分类超时
│   ├── Errors/               # EliCloudError（status/code/description/retryAfter）
│   ├── Validation/           # 昵称/简介/MC 名/备注/图片规则（契约 §6）
│   └── Support/              # 脱敏日志
└── Tests/EliCloudSDKTests/   # 单测 + contracts/fixtures 回放
```

## 1. 生成（不要手写 DTO）

```bash
python tools/gen-swift-sdk/gen_swift_sdk.py            # 生成到 Generated/
python tools/gen-swift-sdk/gen_swift_sdk.py --check    # CI：契约变了却没重新生成 → 失败
```

- 生成器：`apple/swift-openapi-generator 1.9.0`（与 `swift-openapi-runtime 1.9.0` 同版本线），
  版本 pin 在 `tools/gen-swift-sdk/gen_swift_sdk.py`。
- 字段名映射：生成器为 snake_case 字段生成显式 `CodingKeys`，**不要手写 DTO**、也不要改契约的命名。
- 没有本机 Swift 时：`--runner docker`（需要 Docker），或用
  `.github/workflows/swift-sdk-codegen.yml`（人工触发，产物作为 artifact）。

## 2. 用法

```swift
import EliCloudSDK

let client = EliCloudClient(
    baseURL: URL(string: "https://api.example.com")!,
    userAgent: "EliCloud/1.0.0 (macos)"
)

// 登录：成功后令牌进内存会话，再拉 /auth/userinfo 补身份
try await client.sso.login(username: "alice", password: "S3cret!pass")
let me = try await client.sso.userInfo()            // preferred_username / email / email_verified

// 资料
let profile = try await client.userInfo.myProfile() // 没有记录也是 200 + null 字段
try await client.userInfo.updateProfile(nickname: "秋风", bio: "白天写代码，晚上挖矿。")
let image = try await client.userInfo.myImage(kind: .avatar)

// MC
let health = try await client.mc.health()            // 200/503 都返回快照，rcon 不可达时 status == .degraded
let names = try await client.mc.names(includeRemoved: false)
try await client.mc.add(name: "Alice_Star", note: "主账号")
```

错误统一是 `EliCloudError`：

```swift
do {
    try await client.sso.login(username: "alice", password: "wrong")
} catch let error as EliCloudError {
    // error.code == "invalid_grant"（文案按 contracts/errors.json 的 copyKey 从 copy/zh-CN.json 取）
    // error.retryAfter 来自 Retry-After（秒数或 HTTP-date 都解析）
}
```

## 3. 约定（与 Stage 4 文档 §4.2 对齐）

| 主题 | 做法 |
|---|---|
| 令牌 | 只在 `actor SessionStore` 内存里，不落盘、不写 Keychain；退出即清空 |
| 刷新 | 默认**不自动刷新**；`refresh()` 串行（重放旧 refresh token 会撤销整链） |
| 超时 | 按 operationId：普通 20s、`mcHealth` 8s、`updateMyProfile` 30s、`uploadMyProfileImage` 60s |
| 认证头 | 契约 `security: []` 的端点不带 `Authorization`（集合见 `EliCloudTransport.anonymousOperations`） |
| 错误 | `{error,error_description}` → `code`/`description`；非 JSON 只保留 `status`；`Retry-After` → `retryAfter` |
| 探活 | `mcHealth` 的 503 是“降级但有 body”，照常解析，只有其它状态码才抛错 |
| 日志 | 只记 operationId/状态码；不记令牌、密码、URL/query、请求与响应体 |
| 依赖 | 只有官方 `swift-openapi-runtime` + `swift-http-types`；不含 SwiftUI/AppKit，可被 iOS 复用 |

## 4. 测试

```bash
cd sdks/swift && swift test
```

- 单测：`EliCloudError`（信封/非 JSON/Retry-After）、`SessionStore`（过期、合并身份）、
  `Validators`（昵称码点/NFC、MC 名、备注、图片上限）、传输策略（匿名端点、超时表）。
- 契约测试：直接回放 `contracts/fixtures/*.json`（同一套 fixture 与 C#/Kotlin/Python 共用），
  用 `StubTransport` 注入应答，断言状态与错误码，**不走网络、不需要 mock server**。
