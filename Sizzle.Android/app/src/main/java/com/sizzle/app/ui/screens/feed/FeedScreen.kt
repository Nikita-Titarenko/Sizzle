package com.sizzle.app.ui.screens.feed

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.pager.VerticalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.MenuBook
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.outlined.ChatBubbleOutline
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.FavoriteBorder
import androidx.compose.material.icons.outlined.FilterAlt
import androidx.compose.material.icons.outlined.RemoveRedEye
import androidx.compose.material.icons.outlined.Restaurant
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.Share
import androidx.compose.material.icons.automirrored.outlined.VolumeOff
import androidx.compose.material.icons.automirrored.outlined.VolumeUp
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.InputChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshotFlow
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import com.sizzle.app.R
import com.sizzle.app.data.MediaFiles
import com.sizzle.app.data.api.ApiClient
import com.sizzle.app.data.api.message
import com.sizzle.app.data.model.Recipe
import com.sizzle.app.ui.appViewModel
import com.sizzle.app.ui.currentUserId
import com.sizzle.app.ui.components.CommentsViewModel
import com.sizzle.app.ui.components.ErrorView
import com.sizzle.app.ui.components.IngredientPicker
import com.sizzle.app.ui.components.Loader
import com.sizzle.app.ui.components.SecondaryButton
import com.sizzle.app.ui.components.StatusKind
import com.sizzle.app.ui.components.StatusView
import com.sizzle.app.ui.components.UserAvatar
import com.sizzle.app.ui.components.VideoPlayer
import com.sizzle.app.ui.components.commentItems
import com.sizzle.app.ui.navigation.Navigator
import com.sizzle.app.ui.shareRecipe
import com.sizzle.app.ui.theme.Sizzle
import kotlinx.coroutines.delay

/** How long a post must stay on screen before it counts as a view. */
private const val VIEW_DELAY_MS = 2000L

@Composable
fun FeedScreen(navigator: Navigator) {
    val vm = appViewModel { FeedViewModel(it) }
    val context = LocalContext.current
    val userId = currentUserId()
    val signedIn = userId != null
    var ingredientSheet by rememberSaveable { mutableStateOf(false) }
    var commentsFor by remember { mutableStateOf<Recipe?>(null) }

    val recipes = vm.recipes
    // One extra page at the end for the loading / error / empty / caught-up state.
    val pager = rememberPagerState { recipes.size + 1 }

    LaunchedEffect(pager, recipes.size) {
        snapshotFlow { pager.currentPage }.collect { vm.onActive(it) }
    }
    val active = recipes.getOrNull(pager.currentPage)
    LaunchedEffect(active?.id) {
        if (active != null) {
            delay(VIEW_DELAY_MS)
            vm.registerView(active)
        }
    }
    // A new filter starts from the top.
    LaunchedEffect(vm.categoryId, vm.ingredients) { pager.scrollToPage(0) }

    fun requireLogin(action: () -> Unit) = if (signedIn) action() else navigator.login()

    Box(Modifier.fillMaxSize().background(Sizzle.colors.background)) {
        // No page keys: with them, the pager would keep the status page in view when the first recipes arrive in front of it.
        VerticalPager(state = pager, modifier = Modifier.fillMaxSize()) { page ->
            val recipe = recipes.getOrNull(page)
            if (recipe != null) {
                FeedItem(
                    recipe = recipe,
                    active = page == pager.currentPage && !pager.isScrollInProgress,
                    categoryName = vm.categoryNames[recipe.categoryId],
                    canFollow = recipe.userId != userId,
                    muted = vm.muted,
                    onMutedChange = { vm.muted = it },
                    onLike = { requireLogin { vm.toggleLike(recipe) } },
                    onFollow = { requireLogin { vm.toggleFollow(recipe) } },
                    onComments = { commentsFor = recipe },
                    onShare = { shareRecipe(context, recipe.id, recipe.name) },
                    onOpen = { navigator.recipe(recipe.id) },
                    onAuthor = { navigator.user(recipe.userId) },
                )
            } else {
                Box(Modifier.fillMaxSize().statusBarsPadding(), contentAlignment = Alignment.Center) {
                    val failure = vm.failure
                    when {
                        failure != null -> ErrorView(failure, vm::retry)
                        vm.loading || vm.hasMore -> Loader()
                        recipes.isEmpty() && vm.hasFilters -> StatusView(stringResource(R.string.feed_no_matches), kind = StatusKind.Empty) {
                            SecondaryButton(stringResource(R.string.feed_clear_filters), vm::clearFilters)
                        }
                        recipes.isEmpty() -> StatusView(stringResource(R.string.feed_empty), kind = StatusKind.Empty)
                        else -> Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Outlined.CheckCircle, null, tint = Sizzle.colors.muted)
                            Text(stringResource(R.string.feed_caught_up), color = Sizzle.colors.muted, modifier = Modifier.padding(start = 8.dp))
                        }
                    }
                }
            }
        }

        FilterBar(
            vm = vm,
            onIngredients = { ingredientSheet = true },
            modifier = Modifier.align(Alignment.TopCenter).statusBarsPadding().padding(top = 8.dp),
        )

        vm.toast?.let { failure ->
            LaunchedEffect(failure) {
                delay(2500)
                vm.toast = null
            }
            Text(
                failure.message(),
                color = Color.White,
                modifier = Modifier.align(Alignment.BottomCenter).padding(24.dp)
                    .clip(RoundedCornerShape(12.dp)).background(Color.Black.copy(alpha = 0.8f)).padding(12.dp),
            )
        }
    }

    if (ingredientSheet) {
        IngredientSheet(vm) { ingredientSheet = false }
    }
    commentsFor?.let { recipe ->
        CommentsSheet(recipe, navigator) { commentsFor = null }
    }
}

@Composable
private fun FilterBar(vm: FeedViewModel, onIngredients: () -> Unit, modifier: Modifier) {
    val chipColors = FilterChipDefaults.filterChipColors(
        containerColor = Sizzle.colors.surface.copy(alpha = 0.85f),
        labelColor = Sizzle.colors.text,
        iconColor = Sizzle.colors.text,
        selectedContainerColor = Sizzle.colors.accent,
        selectedLabelColor = Sizzle.colors.onAccent,
        selectedLeadingIconColor = Sizzle.colors.onAccent,
    )
    LazyRow(modifier.fillMaxWidth(), contentPadding = PaddingValues(horizontal = 12.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        item {
            val count = vm.ingredients.size
            FilterChip(
                selected = count > 0,
                onClick = onIngredients,
                label = { Text(stringResource(R.string.feed_ingredients) + if (count > 0) " · $count" else "") },
                leadingIcon = { Icon(Icons.Outlined.FilterAlt, null, Modifier.size(18.dp)) },
                shape = CircleShape,
                colors = chipColors,
                border = null,
            )
        }
        item {
            FilterChip(
                selected = vm.categoryId == null,
                onClick = { vm.selectCategory(null) },
                label = { Text(stringResource(R.string.feed_for_you)) },
                shape = CircleShape,
                colors = chipColors,
                border = null,
            )
        }
        items(vm.categories, key = { it.id }) { category ->
            FilterChip(
                selected = vm.categoryId == category.id,
                onClick = { vm.selectCategory(category.id) },
                label = { Text(category.name) },
                shape = CircleShape,
                colors = chipColors,
                border = null,
            )
        }
    }
}

@Composable
private fun FeedItem(
    recipe: Recipe,
    active: Boolean,
    categoryName: String?,
    canFollow: Boolean,
    muted: Boolean,
    onMutedChange: (Boolean) -> Unit,
    onLike: () -> Unit,
    onFollow: () -> Unit,
    onComments: () -> Unit,
    onShare: () -> Unit,
    onOpen: () -> Unit,
    onAuthor: () -> Unit,
) {
    val haptics = LocalHapticFeedback.current
    val isVideo = MediaFiles.isVideoUrl(recipe.fileUrl)
    var paused by remember { mutableStateOf(false) }
    var burst by remember { mutableStateOf(0) }
    var expanded by remember { mutableStateOf(false) }

    Box(
        Modifier
            .fillMaxSize()
            .background(Color.Black)
            .pointerInput(recipe.id, recipe.isLiked) {
                detectTapGestures(
                    onTap = { if (isVideo) paused = !paused },
                    // Double tap likes (never unlikes), like in other short-video apps.
                    onDoubleTap = {
                        burst++
                        haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                        if (!recipe.isLiked) onLike()
                    },
                )
            },
    ) {
        val url = recipe.fileUrl
        when {
            url == null -> Icon(
                Icons.Outlined.Restaurant, null, tint = Color.White.copy(alpha = 0.5f),
                modifier = Modifier.align(Alignment.Center).size(72.dp),
            )
            isVideo -> VideoPlayer(url, Modifier.fillMaxSize(), playing = active && !paused, muted = muted, fill = true)
            else -> AsyncImage(
                model = ApiClient.mediaUrl(url), contentDescription = recipe.name,
                contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize(),
            )
        }

        if (isVideo && paused) {
            Icon(
                Icons.Filled.PlayArrow, null, tint = Color.White.copy(alpha = 0.85f),
                modifier = Modifier.align(Alignment.Center).size(84.dp),
            )
        }
        if (isVideo) {
            IconButton(
                onClick = { onMutedChange(!muted) },
                modifier = Modifier.align(Alignment.TopEnd).statusBarsPadding().padding(top = 56.dp, end = 8.dp)
                    .clip(CircleShape).background(Color.Black.copy(alpha = 0.45f)),
            ) {
                Icon(
                    if (muted) Icons.AutoMirrored.Outlined.VolumeOff else Icons.AutoMirrored.Outlined.VolumeUp,
                    stringResource(if (muted) R.string.feed_unmute else R.string.feed_mute),
                    tint = Color.White,
                )
            }
        }
        HeartBurst(burst, Modifier.align(Alignment.Center))

        // Caption over a dark gradient, like the website's overlay.
        Column(
            Modifier
                .align(Alignment.BottomStart)
                .fillMaxWidth()
                .background(Brush.verticalGradient(listOf(Color.Transparent, Color.Black.copy(alpha = 0.75f))))
                .padding(start = 16.dp, end = 84.dp, top = 48.dp, bottom = 20.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Row(
                    Modifier.clip(CircleShape).clickable(onClick = onAuthor).padding(end = 6.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    UserAvatar(recipe.userName, recipe.userImageUrl, size = 34.dp)
                    Text(recipe.userName, color = Color.White, fontWeight = FontWeight.Bold, modifier = Modifier.padding(start = 8.dp))
                }
                if (canFollow) {
                    Spacer(Modifier.width(8.dp))
                    Text(
                        stringResource(if (recipe.isFollowed) R.string.common_following else R.string.common_follow),
                        color = Color.White,
                        fontWeight = FontWeight.SemiBold,
                        fontSize = 13.sp,
                        modifier = Modifier
                            .clip(CircleShape)
                            .then(
                                if (recipe.isFollowed) Modifier.background(Color.White.copy(alpha = 0.2f))
                                else Modifier.border(1.dp, Color.White, CircleShape),
                            )
                            .clickable(onClick = onFollow)
                            .padding(horizontal = 12.dp, vertical = 5.dp),
                    )
                }
            }
            Text(
                recipe.name,
                color = Color.White,
                style = MaterialTheme.typography.titleLarge,
                modifier = Modifier.clickable(onClick = onOpen),
            )
            if (recipe.description.isNotBlank()) {
                Text(
                    recipe.description,
                    color = Color.White.copy(alpha = 0.85f),
                    maxLines = if (expanded) Int.MAX_VALUE else 2,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier.clickable { expanded = !expanded },
                )
            }
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                categoryName?.let { Tag(null, it) }
                Tag(Icons.Outlined.Schedule, stringResource(R.string.common_minutes, recipe.cookingTimeMinutes))
            }
        }

        Column(
            Modifier.align(Alignment.BottomEnd).padding(end = 10.dp, bottom = 24.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            RailAction(
                if (recipe.isLiked) Icons.Filled.Favorite else Icons.Outlined.FavoriteBorder,
                stringResource(if (recipe.isLiked) R.string.feed_liked else R.string.feed_like),
                tint = if (recipe.isLiked) Sizzle.colors.like else Color.White,
                onClick = {
                    haptics.performHapticFeedback(HapticFeedbackType.LongPress)
                    onLike()
                },
            )
            RailAction(Icons.Outlined.ChatBubbleOutline, stringResource(R.string.feed_comments), onClick = onComments)
            RailAction(Icons.Outlined.Share, stringResource(R.string.feed_share), onClick = onShare)
            RailAction(Icons.AutoMirrored.Outlined.MenuBook, stringResource(R.string.feed_recipe), onClick = onOpen)
            RailAction(Icons.Outlined.RemoveRedEye, recipe.viewCount.toString(), onClick = null)
        }
    }
}

@Composable
private fun HeartBurst(trigger: Int, modifier: Modifier) {
    var visible by remember { mutableStateOf(false) }
    LaunchedEffect(trigger) {
        if (trigger > 0) {
            visible = true
            delay(600)
            visible = false
        }
    }
    AnimatedVisibility(visible, modifier, enter = scaleIn(tween(200)), exit = fadeOut(tween(300))) {
        Icon(Icons.Filled.Favorite, null, tint = Sizzle.colors.like, modifier = Modifier.size(110.dp))
    }
}

@Composable
private fun Tag(icon: ImageVector?, text: String) {
    Row(
        Modifier.clip(CircleShape).background(Color.White.copy(alpha = 0.2f)).padding(horizontal = 10.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (icon != null) Icon(icon, null, tint = Color.White, modifier = Modifier.size(14.dp).padding(end = 2.dp))
        Text(text, color = Color.White, fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
    }
}

@Composable
private fun RailAction(icon: ImageVector, label: String, onClick: (() -> Unit)?, tint: Color = Color.White) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Box(
            Modifier.size(48.dp).clip(CircleShape).background(Color.Black.copy(alpha = 0.35f))
                .then(if (onClick != null) Modifier.clickable(onClick = onClick) else Modifier),
            contentAlignment = Alignment.Center,
        ) {
            Icon(icon, label, tint = tint, modifier = Modifier.size(26.dp))
        }
        Text(label, color = Color.White, fontSize = 11.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.padding(top = 3.dp))
    }
}

@Composable
private fun IngredientSheet(vm: FeedViewModel, onDismiss: () -> Unit) {
    ModalBottomSheet(onDismissRequest = onDismiss, sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true), containerColor = Sizzle.colors.surface) {
        Column(Modifier.fillMaxWidth().padding(horizontal = 20.dp).padding(bottom = 24.dp).navigationBarsPadding(), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            Text(stringResource(R.string.feed_ingredient_panel_title), style = MaterialTheme.typography.titleLarge)
            IngredientPicker(
                placeholder = stringResource(R.string.feed_ingredient_search),
                excludeIds = vm.ingredients.map { it.id }.toSet(),
                onPicked = vm::addIngredient,
            )
            if (vm.ingredients.isEmpty()) {
                Text(stringResource(R.string.feed_ingredient_hint), color = Sizzle.colors.muted)
            } else {
                androidx.compose.foundation.layout.FlowRow(
                    horizontalArrangement = Arrangement.spacedBy(6.dp),
                    verticalArrangement = Arrangement.spacedBy(6.dp),
                ) {
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
                TextButton(onClick = vm::clearIngredients) { Text(stringResource(R.string.common_clear_all), color = Sizzle.colors.accentText) }
            }
        }
    }
}

@Composable
private fun CommentsSheet(recipe: Recipe, navigator: Navigator, onDismiss: () -> Unit) {
    val vm = appViewModel(key = "comments-${recipe.id}") { CommentsViewModel(it, recipe.id) }
    val userId = currentUserId()
    ModalBottomSheet(onDismissRequest = onDismiss, containerColor = Sizzle.colors.surface) {
        Text(
            stringResource(R.string.feed_comments_title, recipe.name),
            style = MaterialTheme.typography.titleMedium,
            modifier = Modifier.padding(horizontal = 20.dp),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
        )
        Spacer(Modifier.height(8.dp))
        LazyColumn(Modifier.fillMaxWidth(), contentPadding = PaddingValues(horizontal = 20.dp, vertical = 8.dp)) {
            commentItems(
                vm = vm,
                currentUserId = userId,
                onLogin = { onDismiss(); navigator.login() },
                onUser = { onDismiss(); navigator.user(it) },
                showTitle = false,
            )
        }
    }
}
