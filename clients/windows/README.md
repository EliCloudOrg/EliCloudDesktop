# Windows 客户端（Stage 3 现状）

> 主应用在仓库根 `src/EliCloudDesktop/`（WinUI 3 + WinUI-Reactor，单项目 MSIX）。
> 本目录的 `hello/` 是 Stage 2 的纯 C# 生成物消费验证；单测在仓库根的 `tests/EliCloudDesktop.Tests/`。
>
> 契约与文案的唯一源在 `contracts/`，设计令牌在 `design/` —— Windows 工程里**不写第二份**。

## 构建

```powershell
# 1) 共享生成物（tokens / copy / .resw / C# key 常量）
python tools/gen-tokens/gen_tokens.py
python tools/gen-copy/gen_copy.py

# 2) 四套配置（都应 0 error / 0 warning；Release 允许 3 条已知打包告警）
dotnet build EliCloudDesktop.slnx -c Debug       -p:Platform=x64 -v:m
dotnet build EliCloudDesktop.slnx -c Debug-Prod  -p:Platform=x64 -v:m
dotnet build EliCloudDesktop.slnx -c Debug-Local -p:Platform=x64 -v:m
dotnet build EliCloudDesktop.slnx -c Release     -p:Platform=x64 -v:m

# 3) hello-world：纯 C# 消费 tokens / copy / build-config
dotnet run --project clients/windows/hello/EliCloud.Hello.csproj
```

**平台参数必须显式给**（`-p:Platform=x64` 或 ARM64/x86）；解决方案默认 Any CPU 会报 MSB4126。

### 平台入口地址（仓库里不存真实值）

`Debug-Local` 固定 `http://127.0.0.1:8080`；其它配置按优先级从可替换来源注入：

1. `-p:EliCloudPlatformBaseAddress=https://your-entry`
2. 本机 `src/EliCloudDesktop/certs/local.props` 里的 `<EliCloudPlatformBaseAddress>`
   （`certs/` 在 `.gitignore`；Visual Studio 里构建只能靠这条）
3. 环境变量 `ELICLOUD_BASE_URL_PROD`

三者都没有时构建在 `EliCloudValidatePlatformBaseAddress` 目标里失败；值不是
`http(s)://host[:port]` 形式也会在构建阶段报错。

## 运行

```powershell
# 本地五服务（SSO/email/file/userinfo/mc + 网关 + 假 RCON）
local-dev\dev.ps1 up -Background

# 两种运行方式：
dotnet run --project src\EliCloudDesktop\EliCloudDesktop.csproj -c Debug-Local -p:Platform=x64
# 或安装已签名的本机包（推荐：真实包身份下 %LOCALAPPDATA% 写入与 MRT 才和发布一致）
```

> 开发模式（`Add-AppxPackage -Register` / `dotnet run` 的 loose layout）下，
> `%LOCALAPPDATA%` 写入与部分 MRT 路径不可用；验证「设置落盘 / 文案资源」请用
> 已签名的 `Release` 或本机 `Debug-Local` `.msix`（见下方打包）。

## 测试

```powershell
# 单元 + 契约回放（74 项；不启动 WinUI，进程里没有 package identity）
dotnet test EliCloudDesktop.slnx -c Debug -v:m
dotnet test tests\EliCloudDesktop.Tests\EliCloudDesktop.Tests.csproj -c Debug -v:m

# Stage 1 契约测试（57 项；SDK ↔ fixtures）
dotnet run --project contracts\tests\csharp\EliCloud.ContractTests.csproj

# 官方样式审计（Windows）
python tools\audit-official-styles\audit.py --platform windows

# UI 冒烟：登录 → 主页/资料/MC/设置，亮/暗截图 + UIA 无障碍树
tools\ui-windows-smoke.ps1 -Username stage3 -Password 'Stage3#2026'
tools\ui-register-smoke.ps1 -Out docs\screenshots\stage-3\register-verified.png

# 截图基线（单页、按窗口标题）
tools\screenshots\windows.ps1 -Page home -Theme light -WindowTitle 'EliCloud Desktop'
```

截图输出：

- `design-preview/windows/<page>/<theme>.png`（`home|profile|mc|settings` × `light|dark`，本轮另有
  `home/high-contrast.png`、`settings/high-contrast.png`）；
- `design-preview/windows/a11y-<theme>.txt`；
- 证据副本在 `docs/screenshots/stage-3/`。

## 打包（`.msixpkg`）

```powershell
dotnet restore src\EliCloudDesktop\EliCloudDesktop.csproj -p:Platform=x64
dotnet build   src\EliCloudDesktop\EliCloudDesktop.csproj -c Release -p:Platform=x64 --no-restore `
  -p:MsixDevCertPassword=<Password>
```

- Restore 与 Build **必须分两次调用**（否则 NuGet targets 来不及导入，表现为「构建成功但没有 .msixpkg」）。
- 产物：`src\EliCloudDesktop\artifacts\msix\EliCloudDesktop_1.0.0.0_x64.msixpkg`，
  内层 `.msix` 在 `...\EliCloudDesktop_1.0.0.0_x64_Test\`。
- 证书 Subject 必须与清单 `Identity/@Publisher` 逐字符一致（`CN=Elipese`）；证书/密码/入口地址都不入库。
- 本机验证安装（无需管理员）：

```powershell
Import-Certificate -FilePath src\EliCloudDesktop\artifacts\msix\EliCloudDesktop_1.0.0.0_x64.cer `
  -CertStoreLocation Cert:\CurrentUser\TrustedPeople
Add-AppxPackage -Path src\EliCloudDesktop\artifacts\msix\EliCloudDesktop_1.0.0.0_x64_Test\EliCloudDesktop_1.0.0.0_x64.msix
```

`msixpkg` CLI 不在 NuGet、需要从 MsixpkgInstaller 源码构建；本仓库当前用
`Add-AppxPackage` + `signtool verify` 做等价验收（见 Stage 3 交接包）。

## 文案 / 令牌（生成物，勿手改）

| 生成物 | 源 | 用途 |
|---|---|---|
| `src/EliCloudDesktop/Strings/Resources.resw` + `Strings/zh-CN/Resources.resw` | `contracts/copy/zh-CN.json` | MRT 文案（中性 + zh-CN；缺中性会 PRI257） |
| `src/EliCloudDesktop/Generated/CopyKeys.g.cs` | copy + `contracts/errors.json` | key 常量 + 错误码 → copyKey 映射 |
| `clients/windows/hello/Generated/DesignTokens.g.cs` | `design/tokens.json` | 官方主题资源键 + 登记过的品牌值；主工程用 `Compile Link` 复用 |
| `clients/windows/hello/Strings/zh-CN/Resources.resw` | copy | hello-world 消费验证 |

读取链：页面只写 `CopyKeys.*` → `Core/Copy.cs`（Core 不依赖 MRT）→ `Services/MrtCopyResolver.cs`
（`ResourceManager.MainResourceMap.GetSubtree("Resources")`，斜杠路径）→ `.resw`。

## 已知缺口

- `msixpkg` CLI 不在本机 / NuGet：用 `Add-AppxPackage` + `signtool verify` 等价验收；
- 高对比度截图需要在会话内临时打开系统高对比度（`tools/ui-windows-smoke.ps1` 只覆盖亮/暗，
  HC 证据见 `docs/screenshots/stage-3/*-high-contrast.png`）；
- 开发模式 loose layout 下 `%LOCALAPPDATA%` 写入被拒、`ResourceLoader` 不可用；真实包正常
  （manifest 已声明 `uap10:TrustLevel="mediumIL"` + `packagedClassicApp`）；
- Reactor 为 preview 固定 `0.1.0-preview.16`，升级前先跑四套构建 + UI 冒烟。
