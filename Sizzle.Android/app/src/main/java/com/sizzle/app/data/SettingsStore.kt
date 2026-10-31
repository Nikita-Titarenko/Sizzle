package com.sizzle.app.data

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.runBlocking

val Context.settings: DataStore<Preferences> by preferencesDataStore(name = "settings")

/** App preferences. The language is not here: AppCompat stores the per-app language itself. */
class SettingsStore(private val context: Context, scope: CoroutineScope) {

    /** Dark by default, like the website; the light theme is opt-in. */
    val lightTheme: StateFlow<Boolean> = context.settings.data
        .map { it[LIGHT_THEME] ?: false }
        .stateIn(scope, SharingStarted.Eagerly, runBlocking { context.settings.data.first()[LIGHT_THEME] ?: false })

    /** Whether the app already asked for the notification permission (Android 13+); asked once, then
     * the user can turn notifications on from the profile settings. */
    suspend fun notificationPromptShown(): Boolean = context.settings.data.first()[NOTIFICATION_PROMPT] ?: false

    suspend fun setNotificationPromptShown() {
        context.settings.edit { it[NOTIFICATION_PROMPT] = true }
    }

    suspend fun setLightTheme(light: Boolean) {
        context.settings.edit { it[LIGHT_THEME] = light }
    }

    private companion object {
        val LIGHT_THEME = booleanPreferencesKey("light_theme")
        val NOTIFICATION_PROMPT = booleanPreferencesKey("notification_prompt_shown")
    }
}
