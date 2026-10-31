package com.sizzle.app.ui.navigation

import android.net.Uri
import kotlinx.serialization.Serializable

@Serializable data object FeedRoute
@Serializable data object ExploreRoute
@Serializable data object ProfileRoute
@Serializable data class RecipeRoute(val id: String)
@Serializable data class RecipeEditorRoute(val id: String? = null)
@Serializable data class UserRoute(val id: String)
@Serializable data object LoginRoute
@Serializable data object RegisterRoute
@Serializable data class ConfirmEmailRoute(val userId: String)

/** Maps sizzle://recipes/{id} and sizzle://users/{id} (notifications, shared links) to a screen. */
fun routeForLink(uri: Uri?): Any? {
    if (uri?.scheme != "sizzle") return null
    val id = uri.pathSegments.firstOrNull() ?: return null
    return when (uri.host) {
        "recipes" -> RecipeRoute(id)
        "users" -> UserRoute(id)
        else -> null
    }
}
