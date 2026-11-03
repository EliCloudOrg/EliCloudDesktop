package cloud.elipese.hello

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource

/**
 * Stage 2 的 Android hello-world：只证明 Compose 工程能消费 tokens / copy / build-config。
 * 业务页面属于 Stage 5。
 */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme {
                Surface(color = Tokens.colorSurfaceBase) {
                    HelloContent()
                }
            }
        }
    }
}

@Composable
private fun HelloContent() {
    Column(modifier = Modifier.padding(Tokens.space6)) {
        Text(
            text = stringResource(R.string.app_title),
            style = Tokens.typeTitle,
            color = Tokens.colorTextPrimary,
        )
        Text(
            text = BuildConfig.USER_AGENT,
            style = Tokens.typeBody,
            color = Tokens.colorTextSecondary,
        )
        Text(
            text = stringResource(R.string.home_hero_description),
            style = Tokens.typeBody,
            color = Tokens.colorTextPrimary,
        )
        Text(
            text = stringResource(R.string.settings_about_version)
                .replace("{version}", BuildConfig.VERSION_NAME),
            style = Tokens.typeCaption,
            color = Tokens.colorTextSecondary,
        )
    }
}
