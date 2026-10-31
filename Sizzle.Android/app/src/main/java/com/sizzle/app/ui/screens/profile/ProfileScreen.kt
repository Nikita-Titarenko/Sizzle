package com.sizzle.app.ui.screens.profile

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.GridItemSpan
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.Logout
import androidx.compose.material.icons.outlined.CameraAlt
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import coil.annotation.ExperimentalCoilApi
import coil.compose.AsyncImage
import com.sizzle.app.AppContainer
import com.sizzle.app.R
import com.sizzle.app.data.MediaFiles
import com.sizzle.app.data.PickedMedia
import com.sizzle.app.data.RecipeEvent
import com.sizzle.app.data.api.ApiClient
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.failure
import com.sizzle.app.data.api.message
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.User
import com.sizzle.app.ui.LocalAppContainer
import com.sizzle.app.ui.appViewModel
import com.sizzle.app.ui.components.AlertBox
import com.sizzle.app.ui.components.ErrorView
import com.sizzle.app.ui.components.FieldShape
import com.sizzle.app.ui.components.Loader
import com.sizzle.app.ui.components.PrimaryButton
import com.sizzle.app.ui.components.SecondaryButton
import com.sizzle.app.ui.components.SectionCard
import com.sizzle.app.ui.components.UserAvatar
import com.sizzle.app.ui.components.recipeItems
import com.sizzle.app.ui.components.sizzleFieldColors
import com.sizzle.app.ui.navigation.Navigator
import com.sizzle.app.ui.screens.RecipeListState
import com.sizzle.app.ui.theme.Sizzle
import kotlinx.coroutines.launch

class ProfileViewModel(private val container: AppContainer) : ViewModel() {
    var user by mutableStateOf<User?>(null)
        private set
    var followers by mutableStateOf<List<User>>(emptyList())
        private set
    var following by mutableStateOf<List<User>>(emptyList())
        private set
    var name by mutableStateOf("")
    var icon by mutableStateOf<PickedMedia?>(null)
    var saving by mutableStateOf(false)
        private set
    var saved by mutableStateOf(false)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set
    var problem by mutableStateOf<String?>(null)
        private set

    val recipes = RecipeListState(viewModelScope, pageSize = 9) { pageIndex, pageSize ->
        container.api.getMyRecipes(pageIndex, pageSize)
    }

    init {
        refresh()
        viewModelScope.launch {
            container.recipeEvents.events.collect { event ->
                when (event) {
                    RecipeEvent.Saved -> recipes.reset()
                    is RecipeEvent.Followed -> loadPeople()
                    else -> recipes.apply(event)
                }
            }
        }
    }

    fun refresh() {
        loadUser()
        loadPeople()
        recipes.reset(pullToRefresh = recipes.loaded)
    }

    fun pickIcon(media: PickedMedia) {
        problem = MediaFiles.problem(container.appContext, media, allowVideo = false)
        if (problem == null) {
            icon = media
            saved = false
        }
    }

    @OptIn(ExperimentalCoilApi::class)
    fun save() {
        saving = true
        saved = false
        failure = null
        viewModelScope.launch {
            val parts = buildList {
                add(MediaFiles.field("Name", name.trim()))
                icon?.let { add(MediaFiles.part(container.appContext, "Icon", it)) }
            }
            val result = runApi { container.api.updateProfile(parts) }
            result.onSuccess {
                // The photo keeps its URL when replaced, so drop the cached copy.
                user?.imageUrl?.let { url ->
                    ApiClient.mediaUrl(url)?.let { container.imageLoader.diskCache?.remove(it) }
                    container.imageLoader.memoryCache?.clear()
                }
                icon = null
                saved = true
                loadUser()
            }
            failure = result.failure
            saving = false
        }
    }

    fun signOut() {
        viewModelScope.launch { container.signOut() }
    }

    private fun loadUser() {
        val id = container.session.userId ?: return
        viewModelScope.launch {
            val result = runApi { container.api.getUser(id) }
            result.onSuccess {
                if (user == null || name.isBlank()) name = it.name
                user = it
            }
            if (user == null) failure = result.failure
        }
    }

    private fun loadPeople() {
        viewModelScope.launch {
            runApi { container.api.getFollowers() }.onSuccess { followers = it }
            runApi { container.api.getFollowing() }.onSuccess { following = it }
        }
    }
}

@Composable
fun ProfileScreen(navigator: Navigator) {
    val container = LocalAppContainer.current
    val session by container.session.session.collectAsStateWithLifecycle()
    if (session == null) {
        SignedOutProfile(navigator)
    } else {
        // Keyed by user, so signing in as someone else starts with a fresh profile.
        SignedInProfile(appViewModel(key = "profile-${session!!.userId}") { ProfileViewModel(it) }, navigator)
    }
}

@Composable
private fun SignedOutProfile(navigator: Navigator) {
    Column(
        Modifier.fillMaxSize().statusBarsPadding().verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        SectionCard {
            Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Image(painterResource(R.drawable.logo_mark), null, Modifier.size(72.dp))
                Text(stringResource(R.string.auth_required_title), style = MaterialTheme.typography.headlineSmall, textAlign = TextAlign.Center)
                Text(stringResource(R.string.auth_required_message), color = Sizzle.colors.muted, textAlign = TextAlign.Center)
                PrimaryButton(stringResource(R.string.nav_log_in), navigator::login, Modifier.fillMaxWidth())
                SecondaryButton(stringResource(R.string.nav_sign_up), navigator::register, Modifier.fillMaxWidth())
            }
        }
        SettingsCard(showNotifications = false)
    }
}

@Composable
private fun SignedInProfile(vm: ProfileViewModel, navigator: Navigator) {
    val context = LocalContext.current
    val pickIcon = rememberLauncherForActivityResult(ActivityResultContracts.PickVisualMedia()) { uri ->
        if (uri != null) vm.pickIcon(MediaFiles.describe(context, uri))
    }
    val emptyText = stringResource(R.string.profile_no_recipes)
    var peopleTab by rememberSaveable { mutableIntStateOf(0) }

    PullToRefreshBox(
        isRefreshing = vm.recipes.refreshing,
        onRefresh = vm::refresh,
        modifier = Modifier.fillMaxSize().statusBarsPadding(),
    ) {
        LazyVerticalGrid(
            columns = GridCells.Adaptive(160.dp),
            contentPadding = PaddingValues(16.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
            modifier = Modifier.fillMaxSize(),
        ) {
            item(span = { GridItemSpan(maxLineSpan) }) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(stringResource(R.string.nav_profile), style = MaterialTheme.typography.headlineMedium, modifier = Modifier.weight(1f))
                    IconButton(onClick = vm::signOut) {
                        Icon(Icons.AutoMirrored.Outlined.Logout, stringResource(R.string.nav_log_out), tint = Sizzle.colors.danger)
                    }
                }
            }
            item(span = { GridItemSpan(maxLineSpan) }) {
                val user = vm.user
                val failure = vm.failure
                if (user == null) {
                    if (failure != null) ErrorView(failure, vm::refresh) else Loader()
                    return@item
                }
                SectionCard {
                    Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        val request = PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly)
                        Box(Modifier.size(96.dp).clip(CircleShape).clickable { pickIcon.launch(request) }) {
                            val picked = vm.icon
                            if (picked != null) {
                                AsyncImage(picked.uri, null, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
                            } else {
                                UserAvatar(user.name, user.imageUrl, size = 96.dp)
                            }
                            Icon(
                                Icons.Outlined.CameraAlt, stringResource(R.string.profile_change_photo), tint = Color.White,
                                modifier = Modifier.align(Alignment.BottomCenter).fillMaxWidth().background(Color.Black.copy(alpha = 0.45f)).padding(4.dp),
                            )
                        }
                        Text(user.email, color = Sizzle.colors.muted, style = MaterialTheme.typography.bodySmall)
                    }
                    OutlinedTextField(
                        value = vm.name,
                        onValueChange = { vm.name = it },
                        label = { Text(stringResource(R.string.profile_name)) },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                        shape = FieldShape,
                        colors = sizzleFieldColors(),
                    )
                    vm.problem?.let { AlertBox(it) }
                    failure?.let { AlertBox(it.message()) }
                    if (vm.saved) Text(stringResource(R.string.profile_saved), color = Sizzle.colors.success)
                    PrimaryButton(
                        stringResource(R.string.profile_save), vm::save, Modifier.fillMaxWidth(),
                        enabled = vm.name.isNotBlank(), busy = vm.saving,
                    )
                }
            }
            item(span = { GridItemSpan(maxLineSpan) }) {
                SectionCard {
                    TabRow(selectedTabIndex = peopleTab, containerColor = Sizzle.colors.surface, contentColor = Sizzle.colors.accent) {
                        Tab(peopleTab == 0, { peopleTab = 0 }, text = { Text(stringResource(R.string.profile_following, vm.following.size)) })
                        Tab(peopleTab == 1, { peopleTab = 1 }, text = { Text(stringResource(R.string.profile_followers, vm.followers.size)) })
                    }
                    val people = if (peopleTab == 0) vm.following else vm.followers
                    if (people.isEmpty()) {
                        Text(
                            stringResource(if (peopleTab == 0) R.string.profile_no_following else R.string.profile_no_followers),
                            color = Sizzle.colors.muted,
                        )
                    }
                    people.forEach { person ->
                        Row(
                            Modifier.fillMaxWidth().clip(CircleShape).clickable { navigator.user(person.id) }.padding(4.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            UserAvatar(person.name, person.imageUrl, size = 36.dp)
                            Text(person.name, fontWeight = FontWeight.SemiBold, modifier = Modifier.padding(start = 10.dp))
                        }
                    }
                }
            }
            item(span = { GridItemSpan(maxLineSpan) }) { SettingsCard(showNotifications = true) }
            item(span = { GridItemSpan(maxLineSpan) }) {
                Text(stringResource(R.string.profile_my_recipes), style = MaterialTheme.typography.titleLarge, modifier = Modifier.padding(top = 8.dp))
            }
            val list = vm.recipes
            if (!list.loaded) {
                item(span = { GridItemSpan(maxLineSpan) }) {
                    list.failure?.let { ErrorView(it, list::retry) } ?: Loader()
                }
            } else {
                recipeItems(list.recipes, emptyText, onOpen = { navigator.recipe(it.id) }, onAuthor = { navigator.user(it.userId) })
                if (list.hasMore) {
                    item(span = { GridItemSpan(maxLineSpan) }) {
                        androidx.compose.runtime.LaunchedEffect(list.recipes.size) { list.loadMore() }
                        Loader()
                    }
                }
            }
        }
    }
}

