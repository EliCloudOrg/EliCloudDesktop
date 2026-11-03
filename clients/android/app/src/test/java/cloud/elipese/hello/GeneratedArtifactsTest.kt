package cloud.elipese.hello

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File

/**
 * Stage 2 生成物自检：tokens / copy / build-config 必须能被 Android 端加载。
 *
 * 只碰纯 JVM 可用的部分（Dp / 常量 / 资源 XML），不启动 Compose 运行时。
 */
class GeneratedArtifactsTest {

    @Test
    fun androidBuildConfigIsInjected() {
        assertTrue(BuildConfig.BASE_URL.startsWith("http://") || BuildConfig.BASE_URL.startsWith("https://"))
        assertTrue(BuildConfig.BASE_URL.endsWith("/").not())
        assertEquals("EliCloud/${BuildConfig.VERSION_NAME} (android)", BuildConfig.USER_AGENT)
        assertFalse(BuildConfig.USER_AGENT.contains("://"))
        assertTrue(BuildConfig.APP_ENV == "local" || BuildConfig.APP_ENV == "prod")
    }

    @Test
    fun designTokensAreGenerated() {
        assertEquals(2, Tokens.tokensVersion)
        assertEquals(16f, Tokens.space4.value)
        assertEquals(48f, Tokens.densityRow.value)
        assertEquals(200, Tokens.motionFastMs)
    }

    @Test
    fun copyResourcesContainSharedKeys() {
        val candidates = listOf(
            File("src/main/res/values/strings.xml"),
            File("app/src/main/res/values/strings.xml"),
        )
        val xml = candidates.firstOrNull { it.exists() }
        assertTrue("找不到生成的 strings.xml（工作目录：${File(".").absolutePath}）", xml != null)
        val text = xml!!.readText(encoding = Charsets.UTF_8)
        listOf("app_title", "login_submit", "home_hero_description", "error_client_unknown").forEach { name ->
            assertTrue("strings.xml 缺少 $name", text.contains("name=\"$name\""))
        }
    }
}
