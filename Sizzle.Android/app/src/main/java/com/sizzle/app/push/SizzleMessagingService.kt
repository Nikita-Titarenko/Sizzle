package com.sizzle.app.push

import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import com.sizzle.app.SizzleApp

/** Receives push messages from the API (see RecipeNotificationSender on the server). */
class SizzleMessagingService : FirebaseMessagingService() {

    override fun onNewToken(token: String) {
        (application as SizzleApp).container.push.onNewToken(token)
    }

    override fun onMessageReceived(message: RemoteMessage) {
        val data = message.data
        // Data messages carry no text, so the notification is written in the app's language.
        if (data["type"] == RECIPE_CREATED && (application as SizzleApp).container.session.session.value != null) {
            Notifications.showNewRecipe(
                context = this,
                recipeId = data["recipeId"] ?: return,
                recipeName = data["recipeName"].orEmpty(),
                authorName = data["authorName"].orEmpty(),
            )
        }
    }

    private companion object {
        const val RECIPE_CREATED = "recipe_created"
    }
}
