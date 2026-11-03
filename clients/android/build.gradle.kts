// Stage 2 的 Android hello-world：版本全部 pin 住（与 .github/workflows/android.yml 一致）。
// 真正的客户端在 Stage 5 展开；这里只验证 Compose 工程能消费 tokens / copy / build-config。

plugins {
    id("com.android.application") version "8.9.1" apply false
    id("org.jetbrains.kotlin.android") version "2.1.10" apply false
    id("org.jetbrains.kotlin.plugin.compose") version "2.1.10" apply false
}
