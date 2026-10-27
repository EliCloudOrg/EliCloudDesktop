# AGENT.md

EliCloud 桌面客户端：WinUI 3 应用，UI 用 **WinUI-Reactor** 纯 C# 声明式写法（工程内无 `.xaml`），业务统一走官方 NuGet 包 **`EliCloud.Sdk`**。设计稿为 `EliCloudDesktopUI.fig`，实现见 `docs/`。

## 技术栈与目录

- .NET 10 / WinUI 3，单工程 `src/EliCloudDesktop/EliCloudDesktop.csproj`，单项目 MSIX（packaged）。
- UI：`Microsoft.UI.Reactor`；`SettingsCard`、`ImageCropper` 借 CommunityToolkit，经 `[GenerateReactorWrapper]` 包成声明式元素。
- 分层：
  - `App.cs`：入口、全局 `AppState`、网关、主题、登录/主框架切换、窗口标题栏。
  - `Core/`：路由、会话、状态 reducer、服务清单与状态、设置、错误文案、平台入口。
  - `Services/EliCloudGateway.cs`：SDK 唯一封装；`UserInfoApi.cs`：userinfo 薄 HTTP 客户端；图片处理与缓存。
  - `Shell/`：`MainShell`（NavigationView + 页面宿主）、`NavFooterProfileItem`（原生逃生舱）。
  - `Pages/`：`LoginPage`、`VerifyEmailPage`、`HomePage`、`ProfilePage`、`McServicePage`、`SettingsPage`。
  - `Ui/`：`DesignTokens`（设计标尺）、`Widgets`（纯函数视觉组件）、两个 Toolkit 包装。
- `local-dev/` 是独立仓库 `EliCloudOrg/devtools` 的 submodule：本地五服务（sso/email/file/userinfo/mc）+ 网关 + 假 RCON。
- `docs/`：设计规格、Reactor 选型与踩坑、文案规范、实机截图。`tools/`：本地起服兼容壳、账号管理、UI 冒烟、截图、.fig 反解。

## 常用命令

```powershell
# 构建（四套配置都应通过）
dotnet build EliCloudDesktop.slnx -c Debug-Prod  -p:Platform=x64 -v:m
dotnet build EliCloudDesktop.slnx -c Debug-Local -p:Platform=x64 -v:m
dotnet build EliCloudDesktop.slnx -c Debug       -p:Platform=x64 -v:m
dotnet build EliCloudDesktop.slnx -c Release     -p:Platform=x64 -v:m

# 运行：连远程 / 连本地
dotnet run --project src\EliCloudDesktop\EliCloudDesktop.csproj -c Debug-Prod -p:Platform=x64
local-dev\dev.ps1 up -Background
dotnet run --project src\EliCloudDesktop\EliCloudDesktop.csproj -c Debug-Local -p:Platform=x64

# 本地服务端
local-dev\dev.ps1 status | logs -f | down | smoke
local-dev\dev.ps1 account list

# 端到端 / 截图（应用需已启动）
tools\ui-login-smoke.ps1
tools\ui-register-smoke.ps1
```

- 平台只能显式传 `-p:Platform=x64`（或 ARM64/x86）；解决方案默认 Any CPU 会报 MSB4126。
- 并列存在 `EliCloud.Sdk` 仓库时自动走 ProjectReference，否则走 NuGet；也可 `-p:EliCloudSdkProject=<csproj>`。
- 本机无 `certs\local.props` 时，Release 构建需加 `-p:MsixDevCertPassword=<Password>`。

## 架构与数据流

```text
App（UseReducer，单一事实来源）
 ├─ PlatformConfig.BaseAddress（构建时常量）
 ├─ UseEffect → 探活全部服务 → dispatch(ServicesRefreshed)
 ├─ UseEffect → 登录后拉 /userinfo 资料 → dispatch(ProfileRefreshed)
 ├─ UseEffect → 用户名/主题写入 %LOCALAPPDATA%\EliCloud\settings.json
 └─ Provide(State / Dispatch / Gateway)
     ├─ 未登录：LoginPage → VerifyEmailPage
     └─ 已登录：MainShell（此层创建 UseNavigation<Route>）
          ├─ 标题栏由 App 统一托管，登录前后同一条
          ├─ NavigationView.MenuItems = f(服务可用性)，不可用即自动消失
          └─ 页面：Home / Profile / Settings / Service("mc")
```

- 页面不写 MVVM：只用 `UseContext` 读状态、`dispatch` 改状态；Reactor 对组件上的 `INotifyPropertyChanged` 无效。
- 路由用 `Route` record 层次 + `RouteTags` 双向映射；服务路由 tag 为 `service:<id>`，不维护第二份页面清单。
- 当前路由的服务被**确认**不可用时 `Replace(Home)`；`Unknown`（尚未探测完）不得赶人。
- 服务状态只有「可用 / 不可用」来自真实探测（MC 走 `GetHealthAsync`，8 秒超时）；`ComingSoon/Maintenance/Unknown` 是预留。分组无服务时整组不渲染。

## 关键工程约定

1. **入口地址构建时注入、运行时只读**：`EliCloudPlatformBaseAddress` 经 `GeneratePlatformConfig` 生成编译期常量；不读环境变量/配置文件、无设置项、界面不显示。值非法直接抛异常。改环境只能用 `-p:` 重新构建。
2. **不落盘任何凭据**：`settings.json` 只存上次用户名与主题；密码、access/refresh token 只在内存，重启需重新登录。
3. **登录后必拉 `/userinfo`**：`/login` 的 `user` 只是便利字段，身份以 `/userinfo` 为准；补全失败不影响已到手的令牌。
4. **登录标识可为用户名或邮箱，注册只能是用户名**（服务端校验字符集不含 `@`），界面文案需据此收窄。
5. **不泄露基础设施信息**：错误统一说「连不上服务器」；不把异常消息（常含主机名/端口）、接口路径、RCON、scope 展示给用户。错误码到中文文案统一走 `Core/EliCloudErrorText.cs`。
6. **能用 Reactor 就声明式**；官方控件成熟就包装；只有声明式表达不了才 `.Set(native => …)` 逃生舱（目前仅 Nav 页脚个人资料入口）。
7. **设计令牌集中**：尺寸/字号/间距进 `Ui/DesignTokens.cs`，颜色一律 `Theme.*`，以自动支持浅色/深色/高对比度；默认跟随系统，设置页可覆盖。
8. **图片流程**：`userinfo` 代理 file，客户端不直连 file、不落盘图片字节。头像/头图先 `ImageCropper` 裁切，再缩到 512 / 1600 px 内重编码 PNG；上传成功后按 sha256 缓存位图。
9. **资料保存顺序为图片先、文字后**；部分成功要刷新服务端快照并只提交已成功的那部分，取消则丢弃所有未保存预览。
10. **昵称默认为用户名**：新账号没有昵称时，展示与编辑都用 SSO 用户名兜底；资料弹窗的
    「保存」按钮只在该昵称合法时可用，不再用「昵称不能为空」的提交后提示。
11. **UI 文案**中文、面向用户、简洁；不出现错误码/接口名等术语。

## Reactor 已知坑（勿重复踩）

- `UseNavigation` 句柄必须在 `NavigationHost` 的父组件（`MainShell`）创建，不能在 `App`。
- `TextBox.Description(...)` 实机不渲染：用 `DesignTokens.Field(input, hint)` 自绘提示行。
- `SettingsCard.HeaderIcon` 只能放 `IconElement`，用 `Icon(BitmapIcon(uri, showAsMonochrome: false))`，不能塞 `Image`。
- `ContentDialog` 在 XamlRoot 浮层上，不继承根部主题：须在 `OnOpened` 后写 `RequestedTheme`。
- 同一时刻只能开一个 `ContentDialog`；`IsOpen` 按值 diff，`true→true` 不会重新显示，需先落一次 `false`（见 `ProfilePage` 的 reopen 计数器）。
- `BitmapImage` 有 UI 线程亲和性；图片解码/编码留在 UI 线程。
- 生成器包装 Toolkit 控件时，元素槽里的类型仍受原生属性类型约束。

## 打包与分发

- `Msixpkg.Build` 仅在 Release 生效：一次 `dotnet build -c Release --no-restore` 产出已签名 `.msixpkg`（`artifacts/msix/`）。
- **Restore 与 Build 必须分两次调用**，否则 NuGet 生成的 targets 来不及导入，表现为「构建成功但没有 .msixpkg」。
- 证书 Subject 必须与清单 `Identity/@Publisher` 逐字符一致（`CN=Elipese`）；证书与密码不入库，密码来源优先级：`-p:MsixDevCertPassword` → `certs\local.props` → `MSIXPKG_DEV_CERT_PASSWORD`。
- `WindowsAppSDKSelfContained=true`；`PublishTrimmed=false`（SDK 反射式 JSON 反序列化，裁剪不安全）。

## 验证现状与边界

- 无单元测试工程；主要验证手段是四套配置构建 + `local-dev\dev.ps1 smoke` + UI 自动化冒烟。
- 构建应为 0 error / 0 warning（Reactor 分析器 `REACTOR_*` 全开）；Release 可能多 3 条打包侧非致命告警（`APPX0105`/`APPX0107`、`mspdbcmf.exe`）。
- 未验证：真实管理员安装/卸载闭环、真实 MC RCON 白名单写操作（会改服务器数据，勿随意执行）。
- `local-dev` 依赖五个服务模块与仓库同级，可用 `ELICLOUD_WORKSPACE` 覆盖；只绑 `127.0.0.1`，使用固定开发令牌/假 RCON，不能当共享测试环境。
- `bin/`、`obj/`、`artifacts/`、`AppPackages/`、`certs/`、`.local/`、`local-dev/.data*` 均为本地产物，不入库。
