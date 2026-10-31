package com.sizzle.app.ui

import android.content.Context
import android.content.Intent
import com.sizzle.app.BuildConfig
import com.sizzle.app.R

/** Opens the system share sheet with a link to the recipe on the website. */
fun shareRecipe(context: Context, recipeId: String, name: String) {
    val link = BuildConfig.WEB_URL.trimEnd('/') + "/recipes/" + recipeId
    val send = Intent(Intent.ACTION_SEND).apply {
        type = "text/plain"
        putExtra(Intent.EXTRA_SUBJECT, name)
        putExtra(Intent.EXTRA_TEXT, "$name\n$link")
    }
    context.startActivity(Intent.createChooser(send, context.getString(R.string.share_chooser)))
}
