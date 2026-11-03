# clients/ — 四端客户端

Stage 2 在这里放四端 **hello-world**：最小工程，只验证共享生成物（tokens / copy / build-config）
能被各端编译并加载。真正的业务页面在各平台阶段展开。

| 平台 | 目录 | 工程 | 构建命令 |
|---|---|---|---|
| Windows | [`windows/`](windows/) | WinUI 3 主线仍在 `src/EliCloudDesktop`；本目录的 `hello/` 是共享生成物消费验证 | `dotnet build clients/windows/hello/EliCloud.Hello.csproj` |
| macOS | [`macos/`](macos/) | SwiftUI + XcodeGen | `cd clients/macos && xcodegen generate && xcodebuild test -scheme EliCloudHello -destination 'platform=macOS'` |
| Android | [`android/`](android/) | Compose M3 + Gradle | `cd clients/android && gradle assembleDebug testDebugUnitTest` |
| Linux | [`linux/`](linux/) | Python + GTK4/libadwaita + Meson | `cd clients/linux && meson setup build && meson compile -C build && meson test -C build` |

构建前先跑共享生成器（见 [`../tools/README.md`](../tools/README.md)）：

```powershell
python tools/gen-tokens/gen_tokens.py
python tools/gen-copy/gen_copy.py
python tools/gen-build-config/gen_build_config.py --environment local --platform <windows|macos|android|linux|all>
```

每个平台目录都有自己的 `AGENT.md`，写清命令、结构、坑和“不要做的事”。
