package com.sizzle.app

import android.content.Context
import coil.ImageLoader
import coil.decode.VideoFrameDecoder
import com.sizzle.app.data.RecipeEvents
import com.sizzle.app.data.SessionStore
import com.sizzle.app.data.SettingsStore
import com.sizzle.app.data.api.ApiClient
import com.sizzle.app.data.api.SizzleApi
import com.sizzle.app.push.PushManager
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.Dispatchers

/** App-wide objects, created once in [SizzleApp]. Plain constructor wiring; no DI framework. */
class AppContainer(context: Context) {
    /** Application context, for content resolver access (uploads) outside composables. */
    val appContext: Context = context.applicationContext

    val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)

    val session = SessionStore(context, scope)
    val settings = SettingsStore(context, scope)
    val recipeEvents = RecipeEvents()

    private val okHttp = ApiClient.okHttp(session)
    val api: SizzleApi = ApiClient.api(okHttp)

    val push = PushManager(context, api, session, scope)

    /** Coil with the app's HTTP client; also decodes the first frame of videos for covers. */
    val imageLoader: ImageLoader = ImageLoader.Builder(context)
        .okHttpClient(okHttp)
        .components { add(VideoFrameDecoder.Factory()) }
        .crossfade(true)
        .build()

    /** Signs out: stop pushes to this device first, while the token still authenticates the request. */
    suspend fun signOut() {
        push.unregister()
        session.signOut()
    }
}
