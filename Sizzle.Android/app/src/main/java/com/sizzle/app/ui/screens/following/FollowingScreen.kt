package com.sizzle.app.ui.screens.following

import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.FavoriteBorder
import androidx.compose.material.icons.outlined.LocalFireDepartment
import androidx.compose.material.icons.outlined.Restaurant
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sizzle.app.AppContainer
import com.sizzle.app.R
import com.sizzle.app.data.RecipeEvent
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.failure
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.FeedRecipe
import com.sizzle.app.data.model.User
import com.sizzle.app.ui.appViewModel
import com.sizzle.app.ui.components.ErrorView
import com.sizzle.app.ui.components.Loader
import com.sizzle.app.ui.components.MediaThumbnail
import com.sizzle.app.ui.components.MetaItem
import com.sizzle.app.ui.components.PrimaryButton
import com.sizzle.app.ui.components.StatusKind
import com.sizzle.app.ui.components.StatusView
import com.sizzle.app.ui.components.UserAvatar
import com.sizzle.app.ui.formatDay
import com.sizzle.app.ui.formatTime
import com.sizzle.app.ui.kcal
import com.sizzle.app.ui.localDate
import com.sizzle.app.ui.navigation.ExploreRoute
import com.sizzle.app.ui.navigation.Navigator
import com.sizzle.app.ui.theme.Sizzle
import com.sizzle.app.ui.withLike
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlin.math.roundToInt

private const val PAGE_SIZE = 15

class FollowingViewModel(private val container: AppContainer) : ViewModel() {
    var recipes by mutableStateOf<List<FeedRecipe>>(emptyList())
        private set
    var cooks by mutableStateOf<List<User>>(emptyList())
        private set
    var loading by mutableStateOf(false)
        private set
    var loaded by mutableStateOf(false)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set

    private var total: Int? = null
    private var pageIndex = 0
    private var job: Job? = null

    val hasMore: Boolean get() = total == null || recipes.size < total!!

    init {
        reload()
        viewModelScope.launch {
            container.recipeEvents.events.collect { event ->
                when (event) {
                    is RecipeEvent.Liked -> recipes = recipes.map {
                        if (it.id == event.recipeId) it.copy(isLiked = event.isLiked, likeCount = it.likeCount.withLike(event.isLiked)) else it
                    }
                    is RecipeEvent.Deleted -> recipes = recipes.filter { it.id != event.recipeId }
                    // Following or unfollowing someone changes whose recipes belong here.
                    is RecipeEvent.Followed -> reload()
                    else -> Unit
                }
            }
        }
    }

    fun reload() {
        job?.cancel()
        loading = false
        failure = null
        total = null
        pageIndex = 0
        viewModelScope.launch { runApi { container.api.getFollowing() }.onSuccess { cooks = it } }
        fetch(replace = true)
    }

    fun loadMore() {
        if (loading || !hasMore || failure != null) return
        fetch()
    }

    fun retry() {
        failure = null
        fetch(replace = recipes.isEmpty())
    }

    private fun fetch(replace: Boolean = false) {
        loading = true
        job = viewModelScope.launch {
            val result = runApi { container.api.getFollowingRecipes(pageIndex, PAGE_SIZE) }
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
        }
    }
}

/**
 * What the cooks you follow posted, newest first, as a timeline grouped by day: a quiet
 * catch-up list, unlike the full-screen "For you" feed and the Explore grid.
 */
@OptIn(ExperimentalFoundationApi::class)
@Composable
fun FollowingScreen(navigator: Navigator) {
    val vm = appViewModel { FollowingViewModel(it) }
    val c = Sizzle.colors
    val list = rememberLazyListState()
    val days = remember(vm.recipes) { vm.recipes.groupBy { localDate(it.creationDate) } }

    val nearEnd by remember {
        derivedStateOf {
            val last = list.layoutInfo.visibleItemsInfo.lastOrNull()?.index ?: 0
            last >= list.layoutInfo.totalItemsCount - 3
        }
    }
    LaunchedEffect(nearEnd, vm.recipes.size) { if (nearEnd && vm.loaded) vm.loadMore() }

    LazyColumn(
        Modifier.fillMaxSize().background(c.background).statusBarsPadding(),
        state = list,
        contentPadding = PaddingValues(bottom = 24.dp),
    ) {
        item(key = "head") {
            Column(Modifier.padding(start = 16.dp, end = 16.dp, top = 16.dp, bottom = 8.dp)) {
                Text(stringResource(R.string.following_title), style = MaterialTheme.typography.headlineMedium)
                Text(stringResource(R.string.following_subtitle), color = c.muted, style = MaterialTheme.typography.bodyMedium)
            }
        }
        if (vm.cooks.isNotEmpty()) {
            item(key = "cooks") {
                LazyRow(
                    contentPadding = PaddingValues(horizontal = 16.dp, vertical = 8.dp),
                    horizontalArrangement = Arrangement.spacedBy(14.dp),
                ) {
                    items(vm.cooks, key = { it.id }) { cook ->
                        Column(
                            Modifier.width(68.dp).clip(RoundedCornerShape(12.dp)).clickable { navigator.user(cook.id) },
                            horizontalAlignment = Alignment.CenterHorizontally,
                        ) {
                            Box(Modifier.border(2.dp, c.accent, CircleShape).padding(3.dp)) {
                                UserAvatar(cook.name, cook.imageUrl, size = 52.dp)
                            }
                            Text(
                                cook.name, style = MaterialTheme.typography.labelSmall, maxLines = 1, overflow = TextOverflow.Ellipsis,
                                textAlign = TextAlign.Center, modifier = Modifier.padding(top = 4.dp),
                            )
                        }
                    }
                }
            }
        }

        val failure = vm.failure
        when {
            failure != null && vm.recipes.isEmpty() -> item(key = "error") { ErrorView(failure, vm::retry) }
            !vm.loaded -> item(key = "loading") { Loader() }
            vm.recipes.isEmpty() -> item(key = "empty") {
                val quiet = vm.cooks.isNotEmpty()
                StatusView(
                    heading = stringResource(if (quiet) R.string.following_quiet_title else R.string.following_empty_title),
                    kind = StatusKind.Empty,
                    message = stringResource(if (quiet) R.string.following_quiet_message else R.string.following_empty_message),
                ) {
                    PrimaryButton(stringResource(R.string.following_find_cooks), onClick = { navigator.tab(ExploreRoute) })
                }
            }
            else -> {
                days.forEach { (date, recipes) ->
                    stickyHeader(key = "day-$date") {
                        Text(
                            (date?.let { formatDay(it) } ?: "").uppercase(),
                            color = c.muted, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.labelMedium,
                            modifier = Modifier.fillMaxWidth().background(c.background).padding(horizontal = 16.dp, vertical = 10.dp),
                        )
                    }
                    recipes.forEachIndexed { index, recipe ->
                        item(key = recipe.id) {
                            TimelineEntry(recipe, last = index == recipes.lastIndex, onOpen = { navigator.recipe(recipe.id) })
                        }
                    }
                }
                item(key = "footer") {
                    Box(Modifier.fillMaxWidth().padding(16.dp), contentAlignment = Alignment.Center) {
                        when {
                            failure != null -> ErrorView(failure, vm::retry)
                            vm.hasMore -> Loader()
                            else -> Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(Icons.Outlined.CheckCircle, null, tint = c.muted, modifier = Modifier.size(18.dp))
                                Text(stringResource(R.string.following_caught_up), color = c.muted, modifier = Modifier.padding(start = 6.dp))
                            }
                        }
                    }
                }
            }
        }
    }
}

/** Time on the left, a rail with a dot, then the post. */
@Composable
private fun TimelineEntry(recipe: FeedRecipe, last: Boolean, onOpen: () -> Unit) {
    val c = Sizzle.colors
    Row(Modifier.fillMaxWidth().height(androidx.compose.foundation.layout.IntrinsicSize.Min).padding(horizontal = 16.dp)) {
        Text(
            formatTime(recipe.creationDate), color = c.muted, style = MaterialTheme.typography.labelSmall,
            textAlign = TextAlign.End, modifier = Modifier.width(44.dp).padding(top = 14.dp),
        )
        Box(Modifier.width(24.dp).fillMaxHeight(), contentAlignment = Alignment.TopCenter) {
            if (!last) Box(Modifier.padding(top = 20.dp).width(2.dp).fillMaxHeight().background(c.border))
            Box(Modifier.padding(top = 14.dp).size(12.dp).clip(CircleShape).background(c.accent))
        }
        val shape = RoundedCornerShape(14.dp)
        Row(
            Modifier.weight(1f).padding(bottom = 12.dp).clip(shape).background(c.surface).border(1.dp, c.border, shape)
                .clickable(onClick = onOpen).padding(10.dp),
        ) {
            Column(Modifier.weight(1f).padding(end = 10.dp), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    UserAvatar(recipe.userName, recipe.userImageUrl, size = 20.dp)
                    Text(
                        stringResource(R.string.following_posted, recipe.userName), color = c.muted,
                        style = MaterialTheme.typography.labelSmall, maxLines = 1, overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.padding(start = 6.dp),
                    )
                }
                Text(recipe.name, fontWeight = FontWeight.Bold, maxLines = 2, overflow = TextOverflow.Ellipsis)
                if (recipe.description.isNotBlank()) {
                    Text(recipe.description, color = c.muted, style = MaterialTheme.typography.bodySmall, maxLines = 2,
                        overflow = TextOverflow.Ellipsis)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.padding(top = 2.dp)) {
                    MetaItem(Icons.Outlined.Schedule, stringResource(R.string.common_minutes, recipe.cookingTimeMinutes))
                    if (recipe.calories > 0) MetaItem(Icons.Outlined.LocalFireDepartment, kcal(recipe.calories.roundToInt()))
                    MetaItem(if (recipe.isLiked) Icons.Filled.Favorite else Icons.Outlined.FavoriteBorder, recipe.likeCount.toString())
                }
            }
            Box(Modifier.size(84.dp).clip(RoundedCornerShape(10.dp)).background(c.surface2), contentAlignment = Alignment.Center) {
                if (recipe.fileUrl != null) {
                    MediaThumbnail(recipe.fileUrl, Modifier.fillMaxSize(), recipe.name)
                } else {
                    Icon(Icons.Outlined.Restaurant, null, tint = c.muted)
                }
            }
        }
    }
}
