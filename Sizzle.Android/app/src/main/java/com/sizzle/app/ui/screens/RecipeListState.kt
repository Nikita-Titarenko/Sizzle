package com.sizzle.app.ui.screens

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import com.sizzle.app.data.RecipeEvent
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.failure
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.PageResponse
import com.sizzle.app.data.model.Recipe
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

/**
 * An endlessly scrolling list of recipes from a paged endpoint (explore, profile, user page).
 * The app scrolls instead of using the website's page buttons.
 */
class RecipeListState(
    private val scope: CoroutineScope,
    private val pageSize: Int = 12,
    private val load: suspend (pageIndex: Int, pageSize: Int) -> PageResponse<Recipe>,
) {
    var recipes by mutableStateOf<List<Recipe>>(emptyList())
        private set
    var loading by mutableStateOf(false)
        private set
    var refreshing by mutableStateOf(false)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set

    /** False until the first page arrived, so screens can show a loader instead of "nothing found". */
    var loaded by mutableStateOf(false)
        private set

    private var total: Int? = null
    private var pageIndex = 0
    private var job: Job? = null

    val hasMore: Boolean get() = total == null || recipes.size < total!!

    fun loadMore() {
        if (loading || !hasMore || failure != null) return
        fetch()
    }

    fun retry() {
        failure = null
        fetch()
    }

    /** Starts over from the first page (new filters or pull to refresh). */
    fun reset(pullToRefresh: Boolean = false) {
        job?.cancel()
        loading = false
        refreshing = pullToRefresh
        failure = null
        total = null
        pageIndex = 0
        if (!pullToRefresh) {
            recipes = emptyList()
            loaded = false
        }
        fetch(replace = true)
    }

    fun apply(event: RecipeEvent) {
        recipes = when (event) {
            is RecipeEvent.Liked -> recipes.map { if (it.id == event.recipeId) it.copy(isLiked = event.isLiked) else it }
            is RecipeEvent.Followed -> recipes.map { if (it.userId == event.userId) it.copy(isFollowed = event.isFollowed) else it }
            is RecipeEvent.Deleted -> recipes.filter { it.id != event.recipeId }
            RecipeEvent.Saved -> recipes
        }
    }

    private fun fetch(replace: Boolean = false) {
        loading = true
        job = scope.launch {
            val result = runApi { load(pageIndex, pageSize) }
            result.onSuccess { page ->
                pageIndex++
                total = page.totalSize
                val existing = if (replace) emptyList() else recipes
                val known = existing.map { it.id }.toSet()
                recipes = existing + page.data.filter { it.id !in known }
                loaded = true
            }
            failure = result.failure
            loading = false
            refreshing = false
        }
    }
}
