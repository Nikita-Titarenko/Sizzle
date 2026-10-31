package com.sizzle.app.ui.screens.profile

import android.app.Activity
import android.content.Intent
import android.provider.Settings
import androidx.appcompat.app.AppCompatDelegate
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.DarkMode
import androidx.compose.material.icons.outlined.Language
import androidx.compose.material.icons.outlined.LightMode
import androidx.compose.material.icons.outlined.Notifications
import androidx.compose.material.icons.outlined.NotificationsOff
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.core.os.LocaleListCompat
import androidx.lifecycle.compose.LifecycleResumeEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.sizzle.app.MainActivity
import com.sizzle.app.R
import com.sizzle.app.push.Notifications
import com.sizzle.app.ui.LocalAppContainer
import com.sizzle.app.ui.components.SectionCard
import com.sizzle.app.ui.theme.Sizzle
import kotlinx.coroutines.launch

/** Theme, language and notification settings (the website's header controls). */
@Composable
fun SettingsCard(showNotifications: Boolean) {
    val container = LocalAppContainer.current
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val light by container.settings.lightTheme.collectAsStateWithLifecycle()
    val language = AppCompatDelegate.getApplicationLocales()[0]?.language ?: LocalConfiguration.current.locales[0].language

    SectionCard {
        Text(stringResource(R.string.settings_title), style = MaterialTheme.typography.titleLarge)

        SettingRow(if (light) Icons.Outlined.LightMode else Icons.Outlined.DarkMode, stringResource(R.string.settings_theme)) {
            Segment(stringResource(R.string.settings_theme_dark), !light) { scope.launch { container.settings.setLightTheme(false) } }
            Segment(stringResource(R.string.settings_theme_light), light) { scope.launch { container.settings.setLightTheme(true) } }
        }

        SettingRow(Icons.Outlined.Language, stringResource(R.string.settings_language)) {
            Segment("EN", language == "en") { changeLanguage(context as Activity, "en") }
            Segment("UK", language == "uk") { changeLanguage(context as Activity, "uk") }
        }

        if (showNotifications && container.push.available) {
            var enabled by remember { mutableStateOf(Notifications.canPost(context)) }
            LifecycleResumeEffect(Unit) {
                enabled = Notifications.canPost(context)
                onPauseOrDispose { }
            }
            Row(
                Modifier.clip(RoundedCornerShape(10.dp)).clickable {
                    context.startActivity(
                        Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, context.packageName),
                    )
                }.padding(vertical = 4.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Icon(if (enabled) Icons.Outlined.Notifications else Icons.Outlined.NotificationsOff, null, tint = Sizzle.colors.muted)
                Column(Modifier.padding(start = 12.dp)) {
                    Text(stringResource(R.string.settings_notifications), fontWeight = FontWeight.SemiBold)
                    Text(
                        stringResource(if (enabled) R.string.settings_notifications_on else R.string.settings_notifications_off),
                        color = Sizzle.colors.muted,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
            }
        }
    }
}

@Composable
private fun SettingRow(icon: ImageVector, label: String, options: @Composable () -> Unit) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, null, tint = Sizzle.colors.muted)
        Text(label, fontWeight = FontWeight.SemiBold, modifier = Modifier.padding(start = 12.dp).weight(1f))
        Row(
            Modifier.clip(CircleShape).background(Sizzle.colors.surface2).padding(3.dp),
            horizontalArrangement = Arrangement.spacedBy(2.dp),
        ) { options() }
    }
}

@Composable
private fun Segment(text: String, selected: Boolean, onClick: () -> Unit) {
    Text(
        text,
        color = if (selected) Sizzle.colors.onAccent else Sizzle.colors.text,
        fontWeight = FontWeight.SemiBold,
        style = MaterialTheme.typography.bodySmall,
        modifier = Modifier.clip(CircleShape).background(if (selected) Sizzle.colors.accent else Sizzle.colors.surface2)
            .clickable(onClick = onClick).padding(horizontal = 14.dp, vertical = 7.dp),
    )
}

/**
 * Switches the per-app language. Data on screen was fetched in the old language (category and
 * ingredient names), so the app restarts its screens, like the website reloads.
 */
private fun changeLanguage(activity: Activity, tag: String) {
    val current = AppCompatDelegate.getApplicationLocales()[0]?.language ?: activity.resources.configuration.locales[0].language
    if (current == tag) return
    AppCompatDelegate.setApplicationLocales(LocaleListCompat.forLanguageTags(tag))
    activity.startActivity(
        Intent(activity, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK),
    )
    activity.finish()
}
