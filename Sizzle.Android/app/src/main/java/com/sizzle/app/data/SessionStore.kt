package com.sizzle.app.data

import android.content.Context
import android.util.Base64
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.longOrNull

/** The signed-in user: the API's JWT and the user ID read from it. */
data class Session(val token: String, val userId: String)

/** Keeps the JWT in DataStore and exposes the current session. */
class SessionStore(private val context: Context, private val scope: CoroutineScope) {

    // Read once at startup, so the first screen already knows whether the user is signed in.
    private val _session = MutableStateFlow(runBlocking { load() })
    val session: StateFlow<Session?> = _session.asStateFlow()

    private val _expired = MutableSharedFlow<Unit>(extraBufferCapacity = 1)

    /** Emits when the API rejected the saved token (expired or revoked) and the user was signed out. */
    val expired: SharedFlow<Unit> = _expired

    val userId: String? get() = _session.value?.userId

    suspend fun signIn(token: String) {
        val session = parse(token) ?: return
        context.settings.edit { it[TOKEN_KEY] = token }
        _session.value = session
    }

    suspend fun signOut() {
        context.settings.edit { it.remove(TOKEN_KEY) }
        _session.value = null
    }

    /** Called from the HTTP layer on a 401 for a request that carried [token]. */
    fun onUnauthorized(token: String) {
        if (_session.value?.token != token) {
            return
        }
        _session.value = null
        _expired.tryEmit(Unit)
        scope.launch { context.settings.edit { it.remove(TOKEN_KEY) } }
    }

    private suspend fun load(): Session? =
        context.settings.data.map { it[TOKEN_KEY] }.first()?.let(::parse)

    private companion object {
        val TOKEN_KEY = stringPreferencesKey("token")

        val NAME_ID_CLAIMS = listOf(
            "nameid",
            "sub",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
        )

        /** Reads the user ID from the token; returns null for a malformed or expired token. */
        fun parse(token: String): Session? {
            val payload = runCatching {
                val part = token.split('.')[1]
                val bytes = Base64.decode(part, Base64.URL_SAFE or Base64.NO_PADDING or Base64.NO_WRAP)
                Json.parseToJsonElement(bytes.decodeToString()).jsonObject
            }.getOrNull() ?: return null

            val expiresAt = payload.long("exp")
            if (expiresAt != null && expiresAt * 1000 < System.currentTimeMillis()) {
                return null
            }
            val userId = NAME_ID_CLAIMS.firstNotNullOfOrNull { payload.string(it) } ?: return null
            return Session(token, userId)
        }

        fun JsonObject.string(name: String) = this[name]?.jsonPrimitive?.contentOrNull
        fun JsonObject.long(name: String) = this[name]?.jsonPrimitive?.longOrNull
    }
}
