package com.sizzle.app.push

import android.content.Context
import android.util.Log
import com.google.firebase.FirebaseApp
import com.google.firebase.messaging.FirebaseMessaging
import com.sizzle.app.data.SessionStore
import com.sizzle.app.data.api.SizzleApi
import com.sizzle.app.data.await
import com.sizzle.app.data.model.DeviceTokenRequest
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch

/**
 * Keeps this device's Firebase token registered with the API while a user is signed in, so the API
 * can push "new recipe" notifications. Does nothing when the app was built without google-services.json.
 */
class PushManager(
    private val context: Context,
    private val api: SizzleApi,
    private val session: SessionStore,
    private val scope: CoroutineScope,
) {
    val available: Boolean get() = FirebaseApp.getApps(context).isNotEmpty()

    /** Registers the token whenever a user signs in (and on every app start while signed in). */
    fun start() {
        scope.launch {
            session.session.map { it?.userId }.distinctUntilChanged().collect { userId ->
                if (userId != null) register()
            }
        }
    }

    /** Firebase rotated the token. */
    fun onNewToken(token: String) {
        if (session.session.value != null) {
            scope.launch { send(token) }
        }
    }

    /** Stops pushes to this device for the current user. Call before clearing the session. */
    suspend fun unregister() {
        val token = currentToken() ?: return
        runCatching { api.unregisterDevice(DeviceTokenRequest(token)) }
            .onFailure { Log.w(TAG, "Could not unregister the device token", it) }
    }

    private suspend fun register() {
        currentToken()?.let { send(it) }
    }

    private suspend fun send(token: String) {
        runCatching { api.registerDevice(DeviceTokenRequest(token)) }
            .onFailure { Log.w(TAG, "Could not register the device token", it) }
    }

    private suspend fun currentToken(): String? {
        if (!available) return null
        return runCatching { FirebaseMessaging.getInstance().token.await() }
            .onFailure { Log.w(TAG, "Firebase token is unavailable", it) }
            .getOrNull()
    }

    private companion object {
        const val TAG = "PushManager"
    }
}
