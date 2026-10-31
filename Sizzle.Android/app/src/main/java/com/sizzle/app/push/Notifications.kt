package com.sizzle.app.push

import android.Manifest
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import com.sizzle.app.MainActivity
import com.sizzle.app.R

/** System notifications: the "New recipes" channel and the notification for one new recipe. */
object Notifications {

    fun createChannels(context: Context) {
        val channel = NotificationChannel(
            context.getString(R.string.notification_channel_new_recipes_id),
            context.getString(R.string.notification_channel_new_recipes),
            NotificationManager.IMPORTANCE_HIGH,
        ).apply {
            description = context.getString(R.string.notification_channel_new_recipes_description)
        }
        context.getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
    }

    fun canPost(context: Context): Boolean {
        val granted = Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU ||
            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED
        return granted && NotificationManagerCompat.from(context).areNotificationsEnabled()
    }

    /** Link that opens a recipe; MainActivity routes sizzle:// links to screens. */
    fun recipeLink(recipeId: String): Uri = Uri.parse("sizzle://recipes/$recipeId")

    fun showNewRecipe(context: Context, recipeId: String, recipeName: String, authorName: String) {
        if (!canPost(context)) {
            return
        }
        val intent = Intent(Intent.ACTION_VIEW, recipeLink(recipeId), context, MainActivity::class.java)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP)
        val pendingIntent = PendingIntent.getActivity(
            context,
            recipeId.hashCode(),
            intent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        val notification = NotificationCompat.Builder(context, context.getString(R.string.notification_channel_new_recipes_id))
            .setSmallIcon(R.drawable.ic_notification)
            .setColor(ContextCompat.getColor(context, R.color.accent))
            .setContentTitle(context.getString(R.string.notification_posted_title, authorName))
            .setContentText(recipeName)
            .setStyle(NotificationCompat.BigTextStyle().bigText(context.getString(R.string.notification_posted, authorName, recipeName)))
            .setCategory(NotificationCompat.CATEGORY_SOCIAL)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setAutoCancel(true)
            .setContentIntent(pendingIntent)
            .build()
        try {
            NotificationManagerCompat.from(context).notify(recipeId.hashCode(), notification)
        } catch (_: SecurityException) {
            // The permission was revoked between the check and the call.
        }
    }
}
