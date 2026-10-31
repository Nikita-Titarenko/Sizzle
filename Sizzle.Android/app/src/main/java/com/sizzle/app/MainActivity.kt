package com.sizzle.app

import android.Manifest
import android.content.Intent
import android.os.Build
import android.os.Bundle
import androidx.activity.SystemBarStyle
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sizzle.app.push.Notifications
import com.sizzle.app.ui.LocalAppContainer
import com.sizzle.app.ui.navigation.SizzleNavHost
import com.sizzle.app.ui.navigation.routeForLink
import com.sizzle.app.ui.theme.SizzleTheme

/** Single activity. An AppCompatActivity so the per-app language (AppCompatDelegate) works on all versions. */
class MainActivity : AppCompatActivity() {

    /** Screen to open from a notification or a sizzle:// link. */
    private var pendingLink by mutableStateOf<Any?>(null)

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        val container = (application as SizzleApp).container
        if (savedInstanceState == null) {
            pendingLink = routeForLink(intent?.data)
        }

        setContent {
            val light by container.settings.lightTheme.collectAsStateWithLifecycle()
            val session by container.session.session.collectAsStateWithLifecycle()

            LaunchedEffect(light) {
                val transparent = android.graphics.Color.TRANSPARENT
                val style = if (light) SystemBarStyle.light(transparent, transparent) else SystemBarStyle.dark(transparent)
                enableEdgeToEdge(statusBarStyle = style, navigationBarStyle = style)
            }

            // Ask for the notification permission once the user is signed in, when notifications start to matter.
            val permission = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { }
            LaunchedEffect(session?.userId) {
                if (session != null && Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                    !Notifications.canPost(this@MainActivity) && !container.settings.notificationPromptShown()
                ) {
                    container.settings.setNotificationPromptShown()
                    permission.launch(Manifest.permission.POST_NOTIFICATIONS)
                }
            }

            CompositionLocalProvider(LocalAppContainer provides container) {
                SizzleTheme(light = light) {
                    SizzleNavHost(pendingLink = pendingLink, onLinkHandled = { pendingLink = null })
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        routeForLink(intent.data)?.let { pendingLink = it }
    }
}
