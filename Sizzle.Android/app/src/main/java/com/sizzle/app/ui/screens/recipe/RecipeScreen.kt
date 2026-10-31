package com.sizzle.app.ui.screens.recipe

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.outlined.CalendarToday
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.Edit
import androidx.compose.material.icons.outlined.FavoriteBorder
import androidx.compose.material.icons.outlined.RemoveRedEye
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.Share
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
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
import com.sizzle.app.data.model.RecipeDetails
import com.sizzle.app.ui.appViewModel
import com.sizzle.app.ui.currentUserId
import com.sizzle.app.ui.components.AlertBox
import com.sizzle.app.ui.components.CommentsViewModel
import com.sizzle.app.ui.components.ConfirmDialog
import com.sizzle.app.ui.components.ErrorView
import com.sizzle.app.ui.components.FollowButton
import com.sizzle.app.ui.components.Loader
import com.sizzle.app.ui.components.MediaView
import com.sizzle.app.ui.components.MetaItem
import com.sizzle.app.ui.components.SectionCard
import com.sizzle.app.ui.components.UserAvatar
import com.sizzle.app.ui.components.commentItems
import com.sizzle.app.ui.formatDate
import com.sizzle.app.ui.navigation.Navigator
import com.sizzle.app.ui.shareRecipe
import com.sizzle.app.ui.theme.Sizzle
import com.sizzle.app.ui.unitLabel
import kotlinx.coroutines.launch

class RecipeViewModel(private val container: AppContainer, private val id: String) : ViewModel() {
    var recipe by mutableStateOf<RecipeDetails?>(null)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set

    /** Error of an action (like, follow, delete), shown above the content. */
    var actionFailure by mutableStateOf<ApiFailure?>(null)
        private set

    init {
        load()
        if (container.session.userId != null) {
            viewModelScope.launch { runApi { container.api.registerView(id) } }
        }
    }

    fun load() {
        failure = null
        viewModelScope.launch {
            val result = runApi { container.api.getRecipe(id) }
            result.onSuccess { recipe = it }
            failure = result.failure
        }
    }

    fun toggleLike() {
        viewModelScope.launch {
            val result = runApi { container.api.toggleLike(id) }
            result.onSuccess {
                recipe = recipe?.copy(isLiked = it.isLiked)
                container.recipeEvents.emit(RecipeEvent.Liked(id, it.isLiked))
            }
            actionFailure = result.failure
        }
    }

    fun toggleFollow() {
        val current = recipe ?: return
        val follow = !current.isFollowed
        viewModelScope.launch {
            val result = runApi { if (follow) container.api.follow(current.userId) else container.api.unfollow(current.userId) }
            result.onSuccess {
                recipe = recipe?.copy(isFollowed = follow)
                container.recipeEvents.emit(RecipeEvent.Followed(current.userId, follow))
            }
            actionFailure = result.failure
        }
    }

    fun delete(onDeleted: () -> Unit) {
        viewModelScope.launch {
            val result = runApi { container.api.deleteRecipe(id) }
            result.onSuccess {
                container.recipeEvents.emit(RecipeEvent.Deleted(id))
                onDeleted()
            }
            actionFailure = result.failure
        }
    }
}

@Composable
fun RecipeScreen(id: String, navigator: Navigator) {
    val vm = appViewModel(key = "recipe-$id") { RecipeViewModel(it, id) }
    val comments = appViewModel(key = "comments-$id") { CommentsViewModel(it, id) }
    val context = LocalContext.current
    val haptics = LocalHapticFeedback.current
    val userId = currentUserId()
    val recipe = vm.recipe
    val isOwner = recipe != null && recipe.userId == userId
    var confirmDelete by rememberSaveable { mutableStateOf(false) }

    Scaffold(
        containerColor = Sizzle.colors.background,
        topBar = {
            TopAppBar(
                title = {},
                navigationIcon = {
                    IconButton(onClick = navigator::back) { Icon(Icons.AutoMirrored.Filled.ArrowBack, stringResource(R.string.nav_back)) }
                },
                actions = {
                    if (recipe != null) {
                        IconButton(onClick = { shareRecipe(context, recipe.id, recipe.name) }) {
                            Icon(Icons.Outlined.Share, stringResource(R.string.feed_share))
                        }
                    }
                    if (isOwner) {
                        IconButton(onClick = { navigator.editRecipe(id) }) { Icon(Icons.Outlined.Edit, stringResource(R.string.common_edit)) }
                        IconButton(onClick = { confirmDelete = true }) {
                            Icon(Icons.Outlined.Delete, stringResource(R.string.common_delete), tint = Sizzle.colors.danger)
                        }
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = Sizzle.colors.background),
            )
        },
    ) { padding ->
        val failure = vm.failure
        when {
            failure != null -> Box(Modifier.padding(padding)) { ErrorView(failure, vm::load) }
            recipe == null -> Box(Modifier.padding(padding)) { Loader() }
            else -> LazyColumn(
                Modifier.fillMaxSize().padding(padding),
                contentPadding = PaddingValues(start = 16.dp, end = 16.dp, bottom = 32.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp),
            ) {
                recipe.fileUrl?.let { url ->
                    item(key = "cover") {
                        MediaView(url, Modifier.fillMaxWidth().aspectRatio(1f).clip(RoundedCornerShape(18.dp)), recipe.name)
                    }
                }
                item(key = "title") {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(recipe.name, style = MaterialTheme.typography.headlineMedium, modifier = Modifier.weight(1f))
                        if (userId != null) {
                            FilledTonalButton(
                                onClick = {
                                    haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                                    vm.toggleLike()
                                },
                                colors = ButtonDefaults.filledTonalButtonColors(
                                    containerColor = if (recipe.isLiked) Sizzle.colors.like.copy(alpha = 0.15f) else Sizzle.colors.surface2,
                                    contentColor = if (recipe.isLiked) Sizzle.colors.like else Sizzle.colors.text,
                                ),
                            ) {
                                Icon(if (recipe.isLiked) Icons.Filled.Favorite else Icons.Outlined.FavoriteBorder, null, Modifier.size(18.dp))
                                Spacer(Modifier.width(6.dp))
                                Text(stringResource(if (recipe.isLiked) R.string.recipe_liked else R.string.recipe_like))
                            }
                        }
                    }
                }
                vm.actionFailure?.let { item(key = "action-error") { AlertBox(it.message()) } }
                item(key = "author") {
                    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Row(
                                Modifier.weight(1f).clip(CircleShape).clickable { navigator.user(recipe.userId) },
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                UserAvatar(recipe.userName, recipe.userImageUrl, size = 40.dp)
                                Text(recipe.userName, fontWeight = FontWeight.Bold, modifier = Modifier.padding(start = 10.dp))
                            }
                            if (userId != null && !isOwner) {
                                FollowButton(recipe.isFollowed, vm::toggleFollow)
                            }
                        }
                        Row(horizontalArrangement = Arrangement.spacedBy(14.dp)) {
                            MetaItem(Icons.Outlined.Schedule, stringResource(R.string.common_minutes, recipe.cookingTimeMinutes))
                            MetaItem(Icons.Outlined.RemoveRedEye, recipe.viewCount.toString())
                            MetaItem(Icons.Outlined.CalendarToday, formatDate(recipe.creationDate))
                        }
                    }
                }
                if (recipe.description.isNotBlank()) {
                    item(key = "description") { Text(recipe.description, style = MaterialTheme.typography.bodyLarge) }
                }
                item(key = "ingredients") {
                    SectionCard {
                        Text(stringResource(R.string.recipe_ingredients), style = MaterialTheme.typography.titleLarge)
                        recipe.ingredients.forEachIndexed { index, ingredient ->
                            if (index > 0) HorizontalDivider(color = Sizzle.colors.border)
                            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                Text(ingredient.name, modifier = Modifier.weight(1f))
                                Text("${ingredient.quantity} ${unitLabel(ingredient.unit)}", color = Sizzle.colors.muted)
                            }
                        }
                    }
                }
                item(key = "steps") {
                    SectionCard {
                        Text(stringResource(R.string.recipe_steps), style = MaterialTheme.typography.titleLarge)
                        recipe.steps.forEachIndexed { index, step ->
                            Row {
                                Box(
                                    Modifier.size(28.dp).clip(CircleShape).background(Sizzle.colors.accentSoft),
                                    contentAlignment = Alignment.Center,
                                ) {
                                    Text("${index + 1}", color = Sizzle.colors.accentText, fontWeight = FontWeight.Bold)
                                }
                                Column(Modifier.padding(start = 12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                    Text(step.name, style = MaterialTheme.typography.titleMedium)
                                    Text(step.description, style = MaterialTheme.typography.bodyMedium)
                                    if (step.fileUrls.isNotEmpty()) {
                                        Row(
                                            Modifier.horizontalScroll(rememberScrollState()),
                                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                                        ) {
                                            step.fileUrls.forEach { url ->
                                                MediaView(url, Modifier.size(220.dp, 165.dp).clip(RoundedCornerShape(12.dp)))
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                commentItems(
                    vm = comments,
                    currentUserId = userId,
                    onLogin = navigator::login,
                    onUser = navigator::user,
                )
            }
        }
    }

    if (confirmDelete && recipe != null) {
        ConfirmDialog(
            text = stringResource(R.string.recipe_confirm_delete, recipe.name),
            confirm = stringResource(R.string.common_delete),
            onConfirm = { vm.delete(onDeleted = { navigator.back() }) },
            onDismiss = { confirmDelete = false },
        )
    }
}
