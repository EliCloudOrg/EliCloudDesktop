# AGENT.md — macOS

## 命令

```bash
python3 tools/gen-tokens/gen_tokens.py
python3 tools/gen-copy/gen_copy.py
python3 tools/gen-build-config/gen_build_config.py --environment local --platform macos
cd clients/macos && xcodegen generate
xcodebuild test -project EliCloudHello.xcodeproj -scheme EliCloudHello -destination 'platform=macOS' CODE_SIGNING_ALLOWED=NO
python3 tools/audit-official-styles/audit.py --platform macos
```

## 结构 / 约定

- `.xcodeproj` 是 `project.yml` 的产物，**不入库**；改工程先改 `project.yml`；
- `Generated/DesignTokens.swift` 与 `Resources/zh-Hans.lproj/Localizable.strings` 是生成物，勿手改；
- `Generated/BuildConfig.swift` 是构建产物，不入库。

## 官方样式优先（硬约束）

- 文本用 `.font(.title/.body/.caption)`；颜色用 `.primary/.secondary/.tertiary`、`Material.*`、
  `Color(nsColor:)` 系统语义色；
- **不要**写 `Color(red:…)`、`NSColor(…)`、`.system(size:)`、`RoundedRectangle(cornerRadius: 10)`；
- 网格/密度/圆角没有官方命名令牌时，从 `design/tokens.json` 的登记例外取，不要就地写数字；
- 官方控件能做的（按钮、卡片、sheet、标题栏、Form）不手绘；Liquid Glass 用官方 API，不做“假玻璃”。

## 不要做

- 不要在 macOS 上套 Windows 风格标题栏/导航；
- 不要绕过系统材质/主题（暗色、高对比度必须跟着系统走）；
- 不要手改 `Generated/` 或 `.lproj` 生成物；
- 不要把入口地址/令牌写进界面或日志。

## Swift SDK（sdks/swift）

```bash
# 契约 → Swift API（生成的 Types/Client 入库，勿手改）
python tools/gen-swift-sdk/gen_swift_sdk.py            # 或 --check 只校验
cd sdks/swift && swift test                            # 单测 + contracts/fixtures 回放
```

- 生成器版本 pin 在 `tools/gen-swift-sdk/gen_swift_sdk.py`（与 `Package.swift` 的
  `swift-openapi-runtime` 同版本线）；没有本机 Swift 时用 `--runner docker`，
  或推分支让 `.github/workflows/swift-sdk-codegen.yml` 生成并产出 artifact。
- `sdks/swift/README.md` 有用法与“为什么这段手写”。

## 已知缺口

- Stage 2 只有 hello-world；业务页面、DMG 公证在 Stage 4；
- Swift SDK 已完成（生成物 + 传输/会话/错误/校验 + 契约测试，CI 绿）；
- 截图/无障碍脚本见 `tools/screenshots/`（XCUITest 骨架在 Stage 4 补）。

## CI
- push 到任意分支会跑 `.github/workflows/macos.yml`（sdk 作业 + app 作业）；
  app 作业需要 `design/`、`tools/`、`contracts/`、`clients/` 的生成物都已入库；
  契约或生成器改动后先让 `swift-sdk-codegen` 刷新 Generated 再提交。
