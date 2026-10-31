package com.sizzle.app.ui

import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewmodel.compose.viewModel
import com.sizzle.app.AppContainer

val LocalAppContainer = staticCompositionLocalOf<AppContainer> { error("AppContainer is not provided") }

/** ID of the signed-in user, or null; recomposes on sign-in and sign-out. */
@Composable
fun currentUserId(): String? {
    val session by LocalAppContainer.current.session.session.collectAsStateWithLifecycle()
    return session?.userId
}

/** A ViewModel created from the app container, scoped to the current navigation destination. */
@Composable
inline fun <reified VM : ViewModel> appViewModel(key: String? = null, crossinline create: (AppContainer) -> VM): VM {
    val container = LocalAppContainer.current
    return viewModel(key = key) { create(container) }
}
