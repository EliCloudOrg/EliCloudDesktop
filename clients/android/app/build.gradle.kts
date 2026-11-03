plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
    id("org.jetbrains.kotlin.plugin.compose")
}

// ── 构建时配置注入 ────────────────────────────────────────────────────
// 值来自 tools/gen-build-config 生成的 build-config.properties（构建产物，不入库）。
// 缺少文件、缺值或 prod 非 HTTPS 时直接让 Gradle 配置阶段失败——不拖到运行时。
val buildConfigFile = rootProject.file("build-config.properties")
require(buildConfigFile.exists()) {
    "缺少 clients/android/build-config.properties；先运行 python tools/gen-build-config/gen_build_config.py"
}
val injected = java.util.Properties().apply {
    buildConfigFile.inputStream().use { load(it) }
}
val injectedEnvironment = injected.getProperty("environment")
    ?: error("build-config.properties 缺少 environment")
val injectedBaseUrl = injected.getProperty("baseUrl")
    ?: error("build-config.properties 缺少 baseUrl")
val injectedAppVersion = injected.getProperty("appVersion")
    ?: error("build-config.properties 缺少 appVersion")
val injectedUserAgent = injected.getProperty("userAgent")
    ?: error("build-config.properties 缺少 userAgent")

require(injectedEnvironment in setOf("local", "prod")) {
    "未知环境：$injectedEnvironment"
}
require(injectedBaseUrl.startsWith("http://") || injectedBaseUrl.startsWith("https://")) {
    "入口地址必须是绝对 http(s) 地址：$injectedBaseUrl"
}
if (injectedEnvironment == "prod") {
    require(injectedBaseUrl.startsWith("https://")) { "prod 入口地址必须使用 HTTPS：$injectedBaseUrl" }
}
require(!injectedUserAgent.contains("://")) { "UA 不允许包含入口地址" }
require(injectedUserAgent == "EliCloud/$injectedAppVersion (android)") {
    "UA 必须是 EliCloud/{version} (android)，实际是 $injectedUserAgent"
}

android {
    namespace = "cloud.elipese.hello"
    compileSdk = 35

    defaultConfig {
        applicationId = "cloud.elipese.hello"
        minSdk = 26
        targetSdk = 35
        versionCode = 1
        versionName = injectedAppVersion

        buildConfigField("String", "BASE_URL", "\"$injectedBaseUrl\"")
        buildConfigField("String", "USER_AGENT", "\"$injectedUserAgent\"")
        buildConfigField("String", "APP_ENV", "\"$injectedEnvironment\"")
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
        }
    }
}

dependencies {
    implementation(platform("androidx.compose:compose-bom:2025.02.00"))
    implementation("androidx.activity:activity-compose:1.10.0")
    implementation("androidx.compose.material3:material3")
    implementation("androidx.compose.ui:ui")

    testImplementation("junit:junit:4.13.2")
}
