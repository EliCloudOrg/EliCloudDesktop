# macOS 客户端（Stage 4）

SwiftUI + `EliCloudSDK`（`sdks/swift`）的参考客户端。原生窗口 / 侧栏 / 工具栏 / `Settings` scene，
不套 Windows 风格标题栏。

## 结构

```text
clients/macos/
├── project.yml                  # XcodeGen 工程定义（.xcodeproj 不入库）
├── EliCloud/
│   ├── App/                     # EliCloudApp（窗口/菜单/Settings）、AppState（会话/主题/探活）
│   ├── DesignSystem/            # 卡片、状态徽标、头像/Hero 等组件
│   ├── Features/
│   │   ├── Auth/                # 登录 / 注册 / 验证邮箱（轮询 + 重发倒计时）
│   │   ├── Home/                # Hero + 服务分组
│   │   ├── Shell/               # NavigationSplitView、侧栏、详情路由
│   │   ├── Profile/             # 资料页、编辑 sheet、裁切器
│   │   ├── Mc/                  # 状态卡、名额、白名单增删
│   │   └── Settings/            # Settings scene
│   ├── Platform/                # Glass（官方材质 + 降低透明度回退）
│   ├── Support/                 # Copy、错误码映射、图片管线、缓存、格式化
│   └── EliCloud.entitlements    # App Sandbox + network.client
├── EliCloudTests/               # 单测（生成物、文案映射、图片管线）
├── Generated/DesignTokens.swift # 生成物（入库）
├── Generated/BuildConfig.swift  # 构建产物（不入库）
└── Resources/zh-Hans.lproj/Localizable.strings  # 生成物（入库）
```

## 构建 / 测试

需要 macOS + Xcode 16.4 + XcodeGen 2.44.1（CI 固定这两个版本）。

```bash
python3 tools/gen-tokens/gen_tokens.py
python3 tools/gen-copy/gen_copy.py
python3 tools/gen-build-config/gen_build_config.py --environment local --platform macos

cd clients/macos
xcodegen generate
xcodebuild test -project EliCloud.xcodeproj -scheme EliCloud \
    -destination 'platform=macOS' CODE_SIGNING_ALLOWED=NO
```

## 约定（Stage 4 §4.3–§4.5）

- **官方样式优先**：文本用 `DesignTokens.type*` / `.font(.title/.body/.caption)`；
  颜色只取 `DesignTokens.color*`（生成物已经是 `.primary` / `Material.*` / 系统语义色）；
  不写 `Color(red:)`、`.system(size:)`、裸圆角。
- **窗口**：1252×800（`.defaultSize`），最小 1000×640；红绿灯与工具栏交给系统。
- **玻璃**：当前工具链是 Xcode 16.4（macOS 15 SDK），没有 macOS 26 的 `.glassEffect` /
  `NSGlassEffectView`；`Platform/Glass.swift` 用官方 `NSVisualEffectView`，
  并在“降低透明度”时退回实底。升级到 Xcode 26 后再接 Liquid Glass（见 §4.5）。
- **图片**：`fileImporter`（security-scoped）→ 类型/大小校验 → `ImagePipeline`（EXIF 方向、
  裁切 1:1 / 5:1、缩到 512/1600、PNG）→ multipart 上传；图片字节不落盘、不写日志。
- **令牌**：只在 `EliCloudSDK.SessionStore` 内存里；界面不显示入口地址/令牌/scope。

## 已知缺口

- 未做公证/DMG（Stage 4 §4.8 + Stage 8）；
- 无 XCUITest（单测已覆盖生成物/文案/图片管线；UI 自动化与截图待补）；
- Liquid Glass 待 Xcode 26 工具链（当前有官方材质回退）。
