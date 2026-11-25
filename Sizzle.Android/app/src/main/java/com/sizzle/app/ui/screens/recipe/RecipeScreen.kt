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
import androidx.compose.material.icons.filled.Bookmark
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.outlined.BookmarkBorder
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
import com.sizzle.app.ui.components.AiImageBadge
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
import com.sizzle.app.ui.withLike
import kotlinx.coroutines.launch
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.LocalFireDepartment
import androidx.compose.material.icons.outlined.Replay
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.TextButton
import androidx.compose.runtime.remember
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.input.TextFieldValue
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.input.KeyboardType
import com.sizzle.app.data.model.IngredientProportion
import com.sizzle.app.data.model.ProportionsRequest
import com.sizzle.app.data.model.RecipeIngredient
import com.sizzle.app.ui.components.FieldShape
import com.sizzle.app.ui.components.sizzleFieldColors
import com.sizzle.app.ui.kcal
import kotlin.math.roundToInt

class RecipeViewModel(private val container: AppContainer, private val id: String) : ViewModel() {
    var recipe by mutableStateOf<RecipeDetails?>(null)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set

    /** Error of an action (like, follow, delete), shown above the content. */
    var actionFailure by mutableStateOf<ApiFailure?>(null)
        private set

    private var viewRegistered = false

    /** The full-screen ad is showing over the recipe. */
    var showAd by mutableStateOf(false)
        private set
    private var adHandled = false

    /** Quantities after the reader scaled the recipe to what they have; null when not scaled. */
    var scaled by mutableStateOf<Map<String, Int>?>(null)
        private set
    /** How much the scaled recipe is of the original, for the calories. */
    private var scale by mutableStateOf(1.0)
    var scaling by mutableStateOf(false)
        private set

    val calories: Int get() = ((recipe?.calories ?: 0.0) * scale).roundToInt()

    fun quantityOf(ingredient: RecipeIngredient): Int = scaled?.get(ingredient.id) ?: ingredient.quantity

    /**
     * "I only have 150 g of cheese": the API scales every other ingredient to match.
     * Scaling always starts from the original recipe, so repeated changes do not drift.
     */
    fun setQuantity(ingredient: RecipeIngredient, quantity: Int) {
        val current = recipe ?: return
        if (quantity < 1 || quantity == quantityOf(ingredient) || scaling) return
        scaling = true
        viewModelScope.launch {
            val result = runApi {
                container.api.getProportions(
                    ProportionsRequest(
                        initial = current.ingredients.map { IngredientProportion(it.id, it.quantity) },
                        changedIngredient = IngredientProportion(ingredient.id, quantity),
                    ),
                )
            }
            result.onSuccess { list ->
                scaled = list.associate { it.id to it.quantity }
                scale = quantity.toDouble() / ingredient.quantity
            }
            actionFailure = result.failure
            scaling = false
        }
    }

    fun resetScale() {
        scaled = null
        scale = 1.0
    }

    init {
        load()
    }

    fun load() {
        failure = null
        viewModelScope.launch {
            val result = runApi { container.api.getRecipe(id) }
            result.onSuccess {
                recipe = it
                // Only an existing recipe counts a view (signed-in users only).
                if (!viewRegistered && container.session.userId != null) {
                    viewRegistered = true
                    runApi { container.api.registerView(id) }
                }
                startAd(it)
            }
            failure = result.failure
        }
    }

    /**
     * Readers without Premium see a full-screen ad; watching it earns the author an ad view.
     * Premium readers skip it and the view is counted straight away. Authors reading their own
     * recipes and drafts earn nothing.
     */
    private suspend fun startAd(recipe: RecipeDetails) {
        if (adHandled) return
        adHandled = true
        val userId = container.session.userId
        if (recipe.isDraft || recipe.userId == userId) return
        if (userId != null) {
            val premium = runApi { container.api.getSubscription() }.getOrNull()?.let { it.remainingSeconds > 0 } ?: false
            if (premium) {
                runApi { container.api.registerAdView(id) }
                return
            }
        }
        if (!AdViews.seenRecently(id)) showAd = true
    }

    fun adClosed(watched: Boolean) {
        showAd = false
        if (!watched) return
        AdViews.remember(id)
        // Signed-out readers see the ad too, but the API only counts signed-in views.
        if (container.session.userId != null) {
            viewModelScope.launch { runApi { container.api.registerAdView(id) } }
        }
    }

    fun toggleLike() {
        viewModelScope.launch {
            val result = runApi { container.api.toggleLike(id) }
            result.onSuccess {
                recipe = recipe?.let { r ->
                    if (r.isLiked == it.isLiked) r else r.copy(isLiked = it.isLiked, likeCount = r.likeCount.withLike(it.isLiked))
                }
                container.recipeEvents.emit(RecipeEvent.Liked(id, it.isLiked))
            }
            actionFailure = result.failure
        }
    }

    fun toggleSave() {
        viewModelScope.launch {
            val result = runApi { container.api.toggleSave(id) }
            result.onSuccess {
                recipe = recipe?.copy(isSaved = it.isSaved)
                container.recipeEvents.emit(RecipeEvent.Bookmarked(id, it.isSaved))
            }
            actionFailure = result.failure
        }
    }

    /** Tells the server about a share, so recommendations can use it (signed-in users only). */
    fun registerShare() {
        if (container.session.userId == null) return
        viewModelScope.launch { runApi { container.api.registerShare(id) } }
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

@OptIn(ExperimentalLayoutApi::class)
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
    var scaling by remember { mutableStateOf<RecipeIngredient?>(null) }
    scaling?.let { ingredient ->
        ScaleDialog(ingredient, vm.quantityOf(ingredient), onDismiss = { scaling = null }) { vm.setQuantity(ingredient, it) }
    }

    Scaffold(
        containerColor = Sizzle.colors.background,
        topBar = {
            TopAppBar(
                title = {},
                navigationIcon = {
                    IconButton(onClick = navigator::back) { Icon(Icons.AutoMirrored.Filled.ArrowBack, stringResource(R.string.nav_back)) }
                },
                actions = {
                    if (recipe != null && userId != null) {
                        IconButton(onClick = vm::toggleSave) {
                            Icon(
                                if (recipe.isSaved) Icons.Filled.Bookmark else Icons.Outlined.BookmarkBorder,
                                stringResource(if (recipe.isSaved) R.string.recipe_unsave else R.string.recipe_save),
                                tint = if (recipe.isSaved) Sizzle.colors.accent else Sizzle.colors.text,
                            )
                        }
                    }
                    if (recipe != null) {
                        IconButton(onClick = {
                            vm.registerShare()
                            shareRecipe(context, recipe.id, recipe.name)
                        }) {
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
                if (recipe.isDraft) {
                    item(key = "draft") {
                        Row(
                            Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).background(Sizzle.colors.surface3).padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Icon(Icons.Outlined.Description, null, tint = Sizzle.colors.muted, modifier = Modifier.size(18.dp))
                            Text(stringResource(R.string.recipe_draft_notice), color = Sizzle.colors.muted, fontWeight = FontWeight.SemiBold,
                                modifier = Modifier.padding(start = 8.dp))
                        }
                    }
                }
                recipe.fileUrl?.let { url ->
                    item(key = "cover") {
                        Box {
                            MediaView(url, Modifier.fillMaxWidth().aspectRatio(1f).clip(RoundedCornerShape(18.dp)), recipe.name)
                            if (recipe.isAIGeneratedImage) {
                                AiImageBadge(Modifier.align(Alignment.BottomStart).padding(12.dp), stringResource(R.string.recipe_ai_image))
                            }
                        }
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
                        FlowRow(horizontalArrangement = Arrangement.spacedBy(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                            MetaItem(Icons.Outlined.Schedule, stringResource(R.string.common_minutes, recipe.cookingTimeMinutes))
                            if (vm.calories > 0) MetaItem(Icons.Outlined.LocalFireDepartment, kcal(vm.calories))
                            MetaItem(Icons.Outlined.RemoveRedEye, recipe.viewCount.toString())
                            MetaItem(Icons.Outlined.FavoriteBorder, recipe.likeCount.toString())
                            MetaItem(Icons.Outlined.CalendarToday, formatDate(recipe.creationDate))
                        }
                    }
                }
                if (recipe.description.isNotBlank()) {
                    item(key = "description") { Text(recipe.description, style = MaterialTheme.typography.bodyLarge) }
                }
                item(key = "ingredients") {
                    SectionCard {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text(stringResource(R.string.recipe_ingredients), style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
                            if (vm.scaled != null) {
                                TextButton(onClick = vm::resetScale) {
                                    Icon(Icons.Outlined.Replay, null, Modifier.size(16.dp), tint = Sizzle.colors.accentText)
                                    Text(stringResource(R.string.recipe_reset_scale), color = Sizzle.colors.accentText,
                                        modifier = Modifier.padding(start = 4.dp))
                                }
                            }
                        }
                        if (recipe.ingredients.isNotEmpty()) {
                            Text(
                                stringResource(if (vm.scaled != null) R.string.recipe_scaled_hint else R.string.recipe_scale_hint),
                                color = Sizzle.colors.muted, style = MaterialTheme.typography.bodySmall,
                            )
                        }
                        recipe.ingredients.forEachIndexed { index, ingredient ->
                            if (index > 0) HorizontalDivider(color = Sizzle.colors.border)
                            val quantity = vm.quantityOf(ingredient)
                            val scaled = quantity != ingredient.quantity
                            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                                Text(ingredient.name, modifier = Modifier.weight(1f))
                                Text(
                                    "$quantity ${unitLabel(ingredient.unit)}",
                                    color = if (scaled) Sizzle.colors.accentText else Sizzle.colors.muted,
                                    fontWeight = if (scaled) FontWeight.Bold else FontWeight.Normal,
                                    modifier = Modifier.clip(RoundedCornerShape(8.dp))
                                        .border(1.dp, Sizzle.colors.border, RoundedCornerShape(8.dp))
                                        .clickable(enabled = !vm.scaling) { scaling = ingredient }
                                        .padding(horizontal = 10.dp, vertical = 6.dp),
                                )
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

    if (vm.showAd) {
        AdInterstitial(
            onClosed = vm::adClosed,
            onFollow = { target ->
                when (target) {
                    AdTarget.Premium -> navigator.premium()
                    AdTarget.NewRecipe -> navigator.newRecipe()
                    AdTarget.Payouts -> navigator.payouts()
                }
            },
        )
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

/** Asks how much of an ingredient the reader has; the recipe is scaled to it. */
@Composable
private fun ScaleDialog(ingredient: RecipeIngredient, current: Int, onDismiss: () -> Unit, onScale: (Int) -> Unit) {
    // Opens focused with the amount selected, so typing replaces it.
    var field by remember { mutableStateOf(TextFieldValue(current.toString(), TextRange(0, current.toString().length))) }
    val focus = remember { FocusRequester() }
    LaunchedEffect(Unit) { focus.requestFocus() }
    val quantity = field.text.toIntOrNull()
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.recipe_scale_title, ingredient.name)) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(stringResource(R.string.recipe_scale_hint), color = Sizzle.colors.muted, style = MaterialTheme.typography.bodySmall)
                OutlinedTextField(
                    value = field,
                    onValueChange = { value ->
                        val digits = value.text.filter(Char::isDigit).take(6)
                        field = if (digits == value.text) value else TextFieldValue(digits, TextRange(digits.length))
                    },
                    modifier = Modifier.focusRequester(focus),
                    singleLine = true,
                    suffix = { Text(unitLabel(ingredient.unit)) },
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    shape = FieldShape,
                    colors = sizzleFieldColors(),
                )
            }
        },
        confirmButton = {
            TextButton(onClick = { onDismiss(); quantity?.let(onScale) }, enabled = quantity != null && quantity > 0) {
                Text(stringResource(R.string.recipe_scale_apply), color = Sizzle.colors.accentText)
            }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.common_cancel)) } },
        containerColor = Sizzle.colors.surface,
    )
}

/**
 * Recipes whose ad was watched lately. The API counts one ad view per reader and recipe every
 * 10 minutes (BusinessLogic:AdViewUpdateIntervalMinutes), so the ad is not shown again sooner.
 */
private object AdViews {
    private const val INTERVAL_MS = 10 * 60 * 1000L
    private val seenAt = mutableMapOf<String, Long>()

    fun seenRecently(recipeId: String): Boolean =
        seenAt[recipeId]?.let { System.currentTimeMillis() - it < INTERVAL_MS } ?: false

    fun remember(recipeId: String) {
        seenAt[recipeId] = System.currentTimeMillis()
    }
}
