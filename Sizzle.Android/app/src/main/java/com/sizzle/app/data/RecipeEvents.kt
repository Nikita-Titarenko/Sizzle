package com.sizzle.app.data

import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow

/** Changes made on one screen that lists on other screens should reflect (a like on the detail page
 * updates the feed, a deleted recipe disappears from the grids). */
sealed interface RecipeEvent {
    data class Liked(val recipeId: String, val isLiked: Boolean) : RecipeEvent
    data class Followed(val userId: String, val isFollowed: Boolean) : RecipeEvent
    data class Deleted(val recipeId: String) : RecipeEvent
    /** A recipe was created or edited: lists should reload. */
    data object Saved : RecipeEvent
}

class RecipeEvents {
    private val _events = MutableSharedFlow<RecipeEvent>(extraBufferCapacity = 16)
    val events: SharedFlow<RecipeEvent> = _events

    fun emit(event: RecipeEvent) {
        _events.tryEmit(event)
    }
}
