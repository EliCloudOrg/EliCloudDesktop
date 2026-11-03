# Android 客户端（Stage 2 hello-world）

Compose M3 最小工程，验证 Android 端能消费 tokens / copy / build-config。业务页面在 Stage 5 展开。

## 结构

```text
clients/android/
├── settings.gradle.kts / build.gradle.kts   # 版本 pin 在 build.gradle.kts
├── gradle.properties
├── app/build.gradle.kts                     # buildConfigField 注入 + prod 必须 HTTPS
├── app/src/main/AndroidManifest.xml
├── app/src/main/java/cloud/elipese/hello/MainActivity.kt
├── app/src/main/java/cloud/elipese/hello/Tokens.kt   # 生成物（入库）
├── app/src/main/res/values/strings.xml                     # 生成物（入库）
├── app/src/test/java/.../GeneratedArtifactsTest.kt
└── build-config.properties                  # 构建产物（不入库）
```

## 构建 / 测试

需要 JDK 21 + Gradle 8.13 + Android SDK（compileSdk 35）。

```bash
python3 tools/gen-tokens/gen_tokens.py
python3 tools/gen-copy/gen_copy.py
python3 tools/gen-build-config/gen_build_config.py \
    --environment local --platform android

cd clients/android
gradle assembleDebug testDebugUnitTest
```

## 与共享层的关系

- tokens：`Tokens` 的 official 值是 `MaterialTheme.colorScheme.*` / `typography.*` / `shapes.*`，
  在 `@Composable` 里读；不要在页面里写 `Color(0xFF…)` 或 `14.sp`；
- copy：`strings.xml` 的资源名由 key 确定性转换（`app.title` → `app_title`），完整映射见
  `design/generated/copy-keys.json`；占位符仍是 `{name}`，由应用替换；
- build config：Gradle 读 `build-config.properties` → `buildConfigField`；缺值/非法/prod 非 HTTPS
  会让配置阶段失败。

## 已知缺口

- 没有 Gradle wrapper（CI 用 setup-gradle 固定 8.13）；本地需自备 Gradle 8.13；
- Compose 版本由 BOM 固定；升级要同步改 `.github/workflows/android.yml` 与本文；
- Stage 2 不做 adaptive icon 的 vector drawable（见 `design/ASSETS.md` 缺口）。
