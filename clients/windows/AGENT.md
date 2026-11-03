# AGENT.md — Windows

> 主应用的 Reactor / MSIX 坑在根 [`AGENT.md`](../../AGENT.md)；本文写 Windows 客户端目录与 Stage 3 相关的约定。

## 命令

```powershell
dotnet build EliCloudDesktop.slnx -c Debug       -p:Platform=x64 -v:m
dotnet build EliCloudDesktop.slnx -c Debug-Prod  -p:Platform=x64 -v:m
dotnet build EliCloudDesktop.slnx -c Debug-Local -p:Platform=x64 -v:m
dotnet build EliCloudDesktop.slnx -c Release     -p:Platform=x64 -v:m

dotnet test tests\EliCloudDesktop.Tests\EliCloudDesktop.Tests.csproj -c Debug -v:m
dotnet run  --project contracts\tests\csharp\EliCloud.ContractTests.csproj

dotnet build clients/windows/hello/EliCloud.Hello.csproj
dotnet run   --project clients/windows/hello/EliCloud.Hello.csproj

python tools/audit-official-styles/audit.py --platform windows
tools/ui-windows-smoke.ps1 -Username <user> -Password <password>
```

非 `Debug-Local` 的构建需要入口地址：`-p:EliCloudPlatformBaseAddress=...`、
本机 `src/EliCloudDesktop/certs/local.props`（gitignored）或 `ELICLOUD_BASE_URL_PROD`。
仓库里不存真实地址；缺失/非法在 `EliCloudValidatePlatformBaseAddress` 目标里直接失败。

## 结构

- `src/EliCloudDesktop/`：WinUI 3 应用（仓库根；不是本目录）；
- `tests/EliCloudDesktop.Tests/`：xUnit，链接主工程 `Core/` + `Services/EliCloudGateway` 的**同一份源码**
  （不引 WinUI、不启动 UI）；断言契约常量、错误映射、校验、reducer，并回放 `contracts/fixtures`；
- `hello/`：Stage 2 纯 C# 生成物消费验证，不引 WinUI；
- `src/EliCloudDesktop/Generated/CopyKeys.g.cs`：生成物（勿手改），key 常量 + errors.json 映射；
- `src/EliCloudDesktop/Strings/{Resources.resw,zh-CN/Resources.resw}`：生成物（勿手改），
  中性 + zh-CN 两份，缺中性会触发 PRI257；
- `src/EliCloudDesktop/Services/MrtCopyResolver.cs`：MRT 读取实现（见下方坑）；
- `src/EliCloudDesktop/Ui/DesignTokens.cs`：官方文本样式 + 生成令牌（Display 例外）。

## 官方样式优先（硬约束）

- 官方已有 `ThemeResource` / `SettingsCard` / `NavigationView` / `ContentDialog` / `MicaBackdrop`，
  **绝不手写**等价物；
- 页面里不写裸 `Color.FromArgb` / `#RRGGBB` / `FontSize=` / `CornerRadius=`；
  从生成令牌 / 官方资源键来（`ThemeResource.Get<Style>("TitleTextBlockStyle")`、
  `ThemeResource.CornerRadius("ControlCornerRadius"|"OverlayCornerRadius")`）；
- 确需例外：先改 `tokens.json`（`source: custom` + reason/approvedBy/scope），
  再在 `design/EXCEPTIONS.md` §2 登记 `file/line/match/expiry`；
- 存量违规基线已收紧为 0（`tools/audit-official-styles/baseline/windows.json` 的 `entries` 为空）；
  新增裸值会让 `shared.yml` / `windows.yml` 失败。

## 坑（Stage 3 实测）

- **`ResourceLoader` 不可用**：在本项目的单项目 MSIX / 打包进程里，
  `new ResourceLoader()` 抛 `COMException`、`new ResourceLoader("Resources")` 抛
  `FileNotFoundException`。用 `new ResourceManager().MainResourceMap.GetSubtree("Resources")`
  的 **斜杠路径**（`home/hero/title`）读取；点号 key 由 `Copy`/`MrtCopyResolver` 转换。
- **开发模式 loose layout**（`Add-AppxPackage -Register` / `dotnet run` 的 AppX 目录）下
  `%LOCALAPPDATA%` 写入与 `ApplicationData.Current` 会被拒，`settings.json` 静默失败；
  真实安装的 `.msix` 正常。验证落盘用签名包。
- 清单必须是 `uap10:TrustLevel="mediumIL"` + `uap10:RuntimeBehavior="packagedClassicApp"`；
  缺了会跑进 AppContainer。
- **官方文本 Style 在挂载前写会被回落**：Title 会退回默认 14px（看起来像 Body）。
  `Ui/OfficialStyles.cs` 在 `Loaded` 后 `ClearValue(FontSize/LineHeight/FontWeight)` 再套一次
  `TitleTextBlockStyle` / `BodyStrongTextBlockStyle` / `BodyTextBlockStyle` / `CaptionTextBlockStyle`。
  改字号/字重时先确认 `PageTitle` 仍是 28px，不要只看构建通过。
- **TitleBar 副标题不会自动清空**：Reactor 对「上一帧有、这一帧不设置」的属性不做清除；
  退出登录后 `Internal` 会留在原生 TitleBar 上。始终显式给 `.Subtitle(value or "")`。
- `dotnet build` **不会刷新** `bin/.../AppX` loose layout；`dotnet run` 才会重新部署。
  改完 UI 后用 `dotnet run` 或重装签名 `.msix`，否则你测的是旧包。
- `TextBox.Description` 不渲染（用 `DesignTokens.Field`）；`ContentDialog` 主题要在 `OnOpened` 后写；
  同一时刻只能开一个 `ContentDialog`。

## 不要做

- 不要手改任何 `Generated/` 文件、`.resw` 或生成令牌；
- 不要把真实入口地址/证书/密码写进仓库、日志、截图或错误文案；
- 不要在 hello 工程里引 WinUI；不要在测试工程里启动 WinUI；
- 不要在 Stage 3 重写页面（XAML + MVVM 迁移是独立 ADR）。
