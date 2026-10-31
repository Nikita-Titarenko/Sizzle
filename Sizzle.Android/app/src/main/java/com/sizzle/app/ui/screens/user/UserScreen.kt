package com.sizzle.app.ui.screens.user

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.GridItemSpan
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sizzle.app.AppContainer
import com.sizzle.app.R
import com.sizzle.app.data.RecipeEvent
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.failure
import com.sizzle.app.data.api.message
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.User
import com.sizzle.app.ui.appViewModel
import com.sizzle.app.ui.currentUserId
import com.sizzle.app.ui.components.AlertBox
import com.sizzle.app.ui.components.ErrorView
import com.sizzle.app.ui.components.FollowButton
import com.sizzle.app.ui.components.Loader
import com.sizzle.app.ui.components.UserAvatar
import com.sizzle.app.ui.components.recipeItems
import com.sizzle.app.ui.navigation.Navigator
import com.sizzle.app.ui.screens.RecipeListState
import com.sizzle.app.ui.theme.Sizzle
import kotlinx.coroutines.launch

class UserViewModel(private val container: AppContainer, private val id: String) : ViewModel() {
    var user by mutableStateOf<User?>(null)
        private set
    var followed by mutableStateOf(false)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set
    var actionFailure by mutableStateOf<ApiFailure?>(null)
        private set

    val recipes = RecipeListState(viewModelScope) { pageIndex, pageSize ->
        container.api.getRecipes(pageIndex = pageIndex, pageSize = pageSize, userId = id)
    }

    init {
        load()
        viewModelScope.launch { container.recipeEvents.events.collect(recipes::apply) }
    }

    fun load() {
        failure = null
        viewModelScope.launch {
            val result = runApi { container.api.getUser(id) }
            result.onSuccess { user = it }
            failure = result.failure
        }
        if (container.session.userId != null) {
            viewModelScope.launch {
                runApi { container.api.getFollowing() }.onSuccess { list -> followed = list.any { it.id == id } }
            }
        }
        recipes.reset()
    }

    fun toggleFollow() {
        val follow = !followed
        viewModelScope.launch {
            val result = runApi { if (follow) container.api.follow(id) else container.api.unfollow(id) }
            result.onSuccess {
                followed = follow
                container.recipeEvents.emit(RecipeEvent.Followed(id, follow))
            }
            actionFailure = result.failure
        }
    }
}

@Composable
fun UserScreen(id: String, navigator: Navigator) {
    val vm = appViewModel(key = "user-$id") { UserViewModel(it, id) }
    val signedIn = currentUserId() != null
    val emptyText = stringResource(R.string.common_no_recipes)

    Scaffold(
        containerColor = Sizzle.colors.background,
        topBar = {
            TopAppBar(
                title = { Text(vm.user?.name.orEmpty()) },
                navigationIcon = {
                    IconButton(onClick = navigator::back) { Icon(Icons.AutoMirrored.Filled.ArrowBack, stringResource(R.string.nav_back)) }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = Sizzle.colors.background),
            )
        },
    ) { padding ->
        val user = vm.user
        val failure = vm.failure
        if (user == null) {
            Box(Modifier.padding(padding)) { if (failure != null) ErrorView(failure, vm::load) else Loader() }
            return@Scaffold
        }
        LazyVerticalGrid(
            columns = GridCells.Adaptive(160.dp),
            contentPadding = PaddingValues(16.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
            modifier = Modifier.fillMaxSize().padding(padding),
        ) {
            item(span = { GridItemSpan(maxLineSpan) }) {
                Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    UserAvatar(user.name, user.imageUrl, size = 96.dp)
                    Text(user.name, style = MaterialTheme.typography.headlineSmall)
                    if (signedIn) {
                        FollowButton(vm.followed, vm::toggleFollow)
                    }
                    vm.actionFailure?.let { AlertBox(it.message()) }
                }
            }
            item(span = { GridItemSpan(maxLineSpan) }) {
                Text(stringResource(R.string.user_recipes), style = MaterialTheme.typography.titleLarge, modifier = Modifier.padding(top = 8.dp))
            }
            val list = vm.recipes
            if (!list.loaded) {
                item(span = { GridItemSpan(maxLineSpan) }) { list.failure?.let { ErrorView(it, list::retry) } ?: Loader() }
            } else {
                recipeItems(list.recipes, emptyText, onOpen = { navigator.recipe(it.id) }, onAuthor = {})
                if (list.hasMore) {
                    item(span = { GridItemSpan(maxLineSpan) }) {
                        LaunchedEffect(list.recipes.size) { list.loadMore() }
                        Loader()
                    }
                }
            }
        }
    }
}
