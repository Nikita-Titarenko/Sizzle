package com.sizzle.app.ui.screens.feed

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sizzle.app.AppContainer
import com.sizzle.app.data.RecipeEvent
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.failure
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.Category
import com.sizzle.app.data.model.IngredientChoice
import com.sizzle.app.data.model.Recipe
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.drop
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch

private const val PAGE_SIZE = 8

/** Start loading the next page when this many posts are left below the active one. */
private const val PREFETCH_DISTANCE = 3

class FeedViewModel(private val container: AppContainer) : ViewModel() {
    var categories by mutableStateOf<List<Category>>(emptyList())
        private set
    var categoryId by mutableStateOf<String?>(null)
        private set
    var ingredients by mutableStateOf<List<IngredientChoice>>(emptyList())
        private set
    var recipes by mutableStateOf<List<Recipe>>(emptyList())
        private set
    var loading by mutableStateOf(false)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set
    var muted by mutableStateOf(true)

    /** Shown briefly at the bottom (e.g. a failed like). */
    var toast by mutableStateOf<ApiFailure?>(null)

    private var total: Int? = null
    private var pageIndex = 0
    private var loadJob: Job? = null
    private val viewed = mutableSetOf<String>()

    val hasMore: Boolean get() = total == null || recipes.size < total!!
    val hasFilters: Boolean get() = categoryId != null || ingredients.isNotEmpty()
    val categoryNames: Map<String, String> get() = categories.associate { it.id to it.name }

    init {
        loadCategories()
        loadNextPage()
        // Likes and follows belong to the signed-in user, so a different user needs fresh data.
        viewModelScope.launch {
            container.session.session.map { it?.userId }.distinctUntilChanged().drop(1).collect { reload() }
        }
        viewModelScope.launch {
            container.recipeEvents.events.collect { event ->
                when (event) {
                    is RecipeEvent.Liked -> patch(event.recipeId) { it.copy(isLiked = event.isLiked) }
                    is RecipeEvent.Followed -> recipes = recipes.map {
                        if (it.userId == event.userId) it.copy(isFollowed = event.isFollowed) else it
                    }
                    is RecipeEvent.Deleted -> recipes = recipes.filter { it.id != event.recipeId }
                    RecipeEvent.Saved -> Unit
                }
            }
        }
    }

    fun onActive(index: Int) {
        if (index >= recipes.size - PREFETCH_DISTANCE) loadNextPage()
    }

    fun retry() {
        if (categories.isEmpty()) loadCategories()
        loadNextPage()
    }

    fun refresh() = reload()

    fun selectCategory(id: String?) {
        if (categoryId != id) {
            categoryId = id
            reload()
        }
    }

    fun addIngredient(choice: IngredientChoice) {
        ingredients = ingredients + choice
        reload()
    }

    fun removeIngredient(id: String) {
        ingredients = ingredients.filter { it.id != id }
        reload()
    }

    fun clearIngredients() {
        ingredients = emptyList()
        reload()
    }

    fun clearFilters() {
        categoryId = null
        ingredients = emptyList()
        reload()
    }

    /** Counts a view once the post stayed on screen for a moment (signed-in users only). */
    fun registerView(recipe: Recipe) {
        if (container.session.userId == null || !viewed.add(recipe.id)) return
        viewModelScope.launch { runApi { container.api.registerView(recipe.id) } }
    }

    fun toggleLike(recipe: Recipe) {
        viewModelScope.launch {
            runApi { container.api.toggleLike(recipe.id) }
                .onSuccess {
                    patch(recipe.id) { r -> r.copy(isLiked = it.isLiked) }
                    container.recipeEvents.emit(RecipeEvent.Liked(recipe.id, it.isLiked))
                }
                .onFailure { toast = it as ApiFailure }
        }
    }

    fun toggleFollow(recipe: Recipe) {
        viewModelScope.launch {
            val follow = !recipe.isFollowed
            runApi { if (follow) container.api.follow(recipe.userId) else container.api.unfollow(recipe.userId) }
                .onSuccess { container.recipeEvents.emit(RecipeEvent.Followed(recipe.userId, follow)) }
                .onFailure { toast = it as ApiFailure }
        }
    }

    private fun loadCategories() {
        viewModelScope.launch {
            runApi { container.api.getCategories() }.onSuccess { categories = it }
        }
    }

    private fun reload() {
        loadJob?.cancel()
        loading = false
        failure = null
        recipes = emptyList()
        total = null
        pageIndex = 0
        loadNextPage()
    }

    private fun loadNextPage() {
        if (loading || !hasMore) return
        loading = true
        failure = null
        val category = categoryId
        loadJob = viewModelScope.launch {
            val result = runApi {
                container.api.getRecipes(
                    pageIndex = pageIndex,
                    pageSize = PAGE_SIZE,
                    categoryIds = listOfNotNull(category),
                    ingredientIds = ingredients.map { it.id },
                )
            }
            result.onSuccess { page ->
                pageIndex++
                total = page.totalSize
                val known = recipes.map { it.id }.toSet()
                recipes = recipes + page.data.filter { it.id !in known }
            }
            failure = result.failure
            loading = false
        }
    }

    private fun patch(id: String, change: (Recipe) -> Recipe) {
        recipes = recipes.map { if (it.id == id) change(it) else it }
    }
}
