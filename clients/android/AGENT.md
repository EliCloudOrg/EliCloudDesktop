# AGENT.md — Android

## 命令

```bash
python3 tools/gen-tokens/gen_tokens.py
python3 tools/gen-copy/gen_copy.py
python3 tools/gen-build-config/gen_build_config.py --environment local --platform android
cd clients/android && gradle assembleDebug testDebugUnitTest
python3 tools/audit-official-styles/audit.py --platform android
```

## 结构 / 约定

- `Tokens.kt`、`res/values/strings.xml` 是生成物（勿手改）；
- `build-config.properties` 是构建产物（不入库）；`buildConfigField` 由 `app/build.gradle.kts` 注入；
- 新增依赖写进 `app/build.gradle.kts`，并同步 `.github/workflows/android.yml` 的 pin。

## 官方样式优先（硬约束）

- 颜色一律 `MaterialTheme.colorScheme.*`；排版 `MaterialTheme.typography.*`；圆角 `MaterialTheme.shapes.*`；
- **不要**写 `Color(0xFF…)`、`fontSize = 14.sp`、`RoundedCornerShape(12.dp)`；
- 官方 M3 组件（Card/Button/NavigationBar/NavigationRail/ModalBottomSheet）能做的，不自绘；
- 触摸目标 ≥ 48dp；不要把桌面密度压到 Android 上；
- 主题跟随系统（动态取色可选），高对比度/暗色必须成立。

## 不要做

- 不要在 Stage 2 写业务页面（Stage 5 才做）；
- 不要手改生成物或把 key 改成 `%1$s` 再回写（占位符统一 `{name}`）；
- 不要把入口地址/令牌打进日志或界面；
- 不要锁横屏/锁比例。

## 已知缺口

- 只有 hello-world + 一个 JVM 单测；Compose 预览/截图（Paparazzi/Roborazzi）在 Stage 5；
- 没有 Gradle wrapper；CI 固定 Gradle 8.13，本地需一致。
