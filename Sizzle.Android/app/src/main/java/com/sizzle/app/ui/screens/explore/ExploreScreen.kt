package com.sizzle.app.ui.screens.explore

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.GridItemSpan
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.rememberLazyGridState
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.InputChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.derivedStateOf
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sizzle.app.AppContainer
import com.sizzle.app.R
import com.sizzle.app.data.RecipeEvent
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.Category
import com.sizzle.app.data.model.IngredientChoice
import com.sizzle.app.ui.appViewModel
import com.sizzle.app.ui.components.ErrorView
import com.sizzle.app.ui.components.FieldShape
import com.sizzle.app.ui.components.IngredientPicker
import com.sizzle.app.ui.components.Loader
import com.sizzle.app.ui.components.recipeItems
import com.sizzle.app.ui.components.sizzleFieldColors
import com.sizzle.app.ui.navigation.Navigator
import com.sizzle.app.ui.screens.RecipeListState
import com.sizzle.app.ui.theme.Sizzle
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.drop
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

class ExploreViewModel(private val container: AppContainer) : ViewModel() {
    var categories by mutableStateOf<List<Category>>(emptyList())
        private set
    var selected by mutableStateOf<Set<String>>(emptySet())
        private set
    var search by mutableStateOf("")
        private set
    var ingredients by mutableStateOf<List<IngredientChoice>>(emptyList())
        private set

    val list = RecipeListState(viewModelScope) { pageIndex, pageSize ->
        container.api.getRecipes(
            pageIndex = pageIndex,
            pageSize = pageSize,
            search = search.trim().ifEmpty { null },
            categoryIds = selected.toList(),
            ingredientIds = ingredients.map { it.id },
        )
    }

    private var searchJob: Job? = null

    init {
        loadCategories()
        list.reset()
        viewModelScope.launch {
            container.session.session.map { it?.userId }.distinctUntilChanged().drop(1).collect { list.reset() }
        }
        viewModelScope.launch {
            container.recipeEvents.events.collect { event ->
                if (event == RecipeEvent.Saved) list.reset() else list.apply(event)
            }
        }
    }

    fun onSearch(value: String) {
        search = value
        searchJob?.cancel()
        searchJob = viewModelScope.launch {
            delay(250)
            list.reset()
        }
    }

    fun toggleCategory(id: String) {
        selected = if (id in selected) selected - id else selected + id
        list.reset()
    }

    fun addIngredient(choice: IngredientChoice) {
        ingredients = ingredients + choice
        list.reset()
    }

    fun removeIngredient(id: String) {
        ingredients = ingredients.filter { it.id != id }
        list.reset()
    }

    fun retry() {
        if (categories.isEmpty()) loadCategories()
        list.retry()
    }

    private fun loadCategories() {
        viewModelScope.launch { runApi { container.api.getCategories() }.onSuccess { categories = it } }
    }
}

@Composable
fun ExploreScreen(navigator: Navigator) {
    val vm = appViewModel { ExploreViewModel(it) }
    val list = vm.list
    val grid = rememberLazyGridState()
    val focus = LocalFocusManager.current
    val emptyText = stringResource(R.string.explore_empty)

    val nearEnd by remember {
        derivedStateOf {
            val last = grid.layoutInfo.visibleItemsInfo.lastOrNull()?.index ?: 0
            last >= grid.layoutInfo.totalItemsCount - 4
        }
    }
    LaunchedEffect(nearEnd, list.recipes.size) { if (nearEnd && list.loaded) list.loadMore() }

    PullToRefreshBox(
        isRefreshing = list.refreshing,
        onRefresh = { list.reset(pullToRefresh = true) },
        modifier = Modifier.fillMaxSize().statusBarsPadding(),
    ) {
        LazyVerticalGrid(
            columns = GridCells.Adaptive(160.dp),
            state = grid,
            contentPadding = PaddingValues(16.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
            modifier = Modifier.fillMaxSize(),
        ) {
            item(span = { GridItemSpan(maxLineSpan) }) {
                Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text(stringResource(R.string.explore_title), style = MaterialTheme.typography.headlineMedium)
                    OutlinedTextField(
                        value = vm.search,
                        onValueChange = vm::onSearch,
                        modifier = Modifier.fillMaxWidth(),
                        placeholder = { Text(stringResource(R.string.explore_search)) },
                        leadingIcon = { Icon(Icons.Outlined.Search, null) },
                        trailingIcon = {
                            if (vm.search.isNotEmpty()) {
                                IconButton(onClick = { vm.onSearch("") }) { Icon(Icons.Outlined.Close, stringResource(R.string.common_close)) }
                            }
                        },
                        singleLine = true,
                        shape = CircleShape,
                        colors = sizzleFieldColors(),
                        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                        keyboardActions = KeyboardActions(onSearch = { focus.clearFocus() }),
                    )
                    IngredientPicker(
                        placeholder = stringResource(R.string.explore_ingredient_filter),
                        excludeIds = vm.ingredients.map { it.id }.toSet(),
                        onPicked = vm::addIngredient,
                    )
                    if (vm.ingredients.isNotEmpty()) {
                        FlowRow(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                            vm.ingredients.forEach { ingredient ->
                                InputChip(
                                    selected = true,
                                    onClick = { vm.removeIngredient(ingredient.id) },
                                    label = { Text(listOfNotNull(ingredient.parentName, ingredient.name).joinToString(" › ")) },
                                    trailingIcon = {
                                        Icon(Icons.Outlined.Close, stringResource(R.string.common_remove, ingredient.name), Modifier.size(16.dp))
                                    },
                                    shape = CircleShape,
                                )
                            }
                        }
                    }
                    LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        items(vm.categories, key = { it.id }) { category ->
                            FilterChip(
                                selected = category.id in vm.selected,
                                onClick = { vm.toggleCategory(category.id) },
                                label = { Text(category.name) },
                                shape = CircleShape,
                                colors = FilterChipDefaults.filterChipColors(
                                    containerColor = Sizzle.colors.surface2,
                                    selectedContainerColor = Sizzle.colors.accent,
                                    selectedLabelColor = Sizzle.colors.onAccent,
                                ),
                            )
                        }
                    }
                }
            }

            val failure = list.failure
            when {
                failure != null && list.recipes.isEmpty() ->
                    item(span = { GridItemSpan(maxLineSpan) }) { ErrorView(failure, vm::retry) }
                !list.loaded -> item(span = { GridItemSpan(maxLineSpan) }) { Loader() }
                else -> {
                    recipeItems(
                        recipes = list.recipes,
                        emptyText = emptyText,
                        onOpen = { navigator.recipe(it.id) },
                        onAuthor = { navigator.user(it.userId) },
                    )
                    if (failure != null) {
                        item(span = { GridItemSpan(maxLineSpan) }) { ErrorView(failure, list::retry) }
                    } else if (list.loading) {
                        item(span = { GridItemSpan(maxLineSpan) }) { Loader() }
                    }
                }
            }
        }
    }
}
