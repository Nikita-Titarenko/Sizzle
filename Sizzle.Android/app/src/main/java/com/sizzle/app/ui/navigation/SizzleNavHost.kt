package com.sizzle.app.ui.navigation

import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AddCircle
import androidx.compose.material.icons.filled.Explore
import androidx.compose.material.icons.filled.LocalFireDepartment
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.outlined.AddCircleOutline
import androidx.compose.material.icons.outlined.Explore
import androidx.compose.material.icons.outlined.LocalFireDepartment
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavDestination
import androidx.navigation.NavDestination.Companion.hasRoute
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import androidx.navigation.toRoute
import com.sizzle.app.R
import com.sizzle.app.ui.LocalAppContainer
import com.sizzle.app.ui.screens.auth.ConfirmEmailScreen
import com.sizzle.app.ui.screens.auth.LoginScreen
import com.sizzle.app.ui.screens.auth.RegisterScreen
import com.sizzle.app.ui.screens.editor.RecipeEditorScreen
import com.sizzle.app.ui.screens.explore.ExploreScreen
import com.sizzle.app.ui.screens.feed.FeedScreen
import com.sizzle.app.ui.screens.profile.ProfileScreen
import com.sizzle.app.ui.screens.recipe.RecipeScreen
import com.sizzle.app.ui.screens.user.UserScreen
import com.sizzle.app.ui.theme.Sizzle
import kotlin.reflect.KClass

/** Navigation actions shared by all screens. */
class Navigator(private val nav: NavHostController, private val currentUserId: () -> String?) {
    fun back() = nav.popBackStack()
    fun recipe(id: String) = nav.navigate(RecipeRoute(id))
    fun editRecipe(id: String) = nav.navigate(RecipeEditorRoute(id))

    /** Your own user page is the profile tab. */
    fun user(id: String) = if (id == currentUserId()) tab(ProfileRoute) else nav.navigate(UserRoute(id))
    fun login() = nav.navigate(LoginRoute) { launchSingleTop = true }
    fun register() = nav.navigate(RegisterRoute) { launchSingleTop = true }
    fun confirmEmail(userId: String) = nav.navigate(ConfirmEmailRoute(userId))

    fun newRecipe() = if (currentUserId() == null) login() else nav.navigate(RecipeEditorRoute())

    /** Leaves the login/register/confirm screens and returns to where the user started. */
    fun finishAuth() {
        while (nav.currentDestination.isAuth()) {
            if (!nav.popBackStack()) break
        }
    }

    /** After saving a recipe: replace the editor with the recipe page. */
    fun showSavedRecipe(id: String) = nav.navigate(RecipeRoute(id)) {
        popUpTo<RecipeEditorRoute> { inclusive = true }
    }

    fun tab(route: Any) = nav.navigate(route) {
        popUpTo(nav.graph.findStartDestination().id) { saveState = true }
        launchSingleTop = true
        restoreState = true
    }

    fun open(route: Any) = nav.navigate(route)

    private fun NavDestination?.isAuth() =
        this != null && (hasRoute(LoginRoute::class) || hasRoute(RegisterRoute::class) || hasRoute(ConfirmEmailRoute::class))
}

private data class Tab(val route: Any, val type: KClass<*>, val label: Int, val icon: ImageVector, val selectedIcon: ImageVector)

private val tabs = listOf(
    Tab(FeedRoute, FeedRoute::class, R.string.nav_for_you, Icons.Outlined.LocalFireDepartment, Icons.Filled.LocalFireDepartment),
    Tab(ExploreRoute, ExploreRoute::class, R.string.nav_explore, Icons.Outlined.Explore, Icons.Filled.Explore),
    Tab(RecipeEditorRoute(), RecipeEditorRoute::class, R.string.nav_new_recipe, Icons.Outlined.AddCircleOutline, Icons.Filled.AddCircle),
    Tab(ProfileRoute, ProfileRoute::class, R.string.nav_profile, Icons.Outlined.Person, Icons.Filled.Person),
)

@Composable
fun SizzleNavHost(pendingLink: Any?, onLinkHandled: () -> Unit) {
    val container = LocalAppContainer.current
    val nav = rememberNavController()
    val navigator = remember(nav) { Navigator(nav) { container.session.userId } }
    val snackbar = remember { SnackbarHostState() }
    val context = LocalContext.current
    val backStack by nav.currentBackStackEntryAsState()
    val destination = backStack?.destination
    val session by container.session.session.collectAsStateWithLifecycle()

    LaunchedEffect(pendingLink) {
        if (pendingLink != null) {
            nav.navigate(pendingLink)
            onLinkHandled()
        }
    }
    LaunchedEffect(Unit) {
        container.session.expired.collect {
            snackbar.showSnackbar(context.getString(R.string.errors_session_expired))
        }
    }

    // The bottom bar belongs to the main sections; detail and form screens use the whole height.
    val showBar = destination == null || listOf(FeedRoute::class, ExploreRoute::class, ProfileRoute::class).any { destination.hasRoute(it) }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbar) },
        containerColor = Sizzle.colors.background,
        // Screens handle the system bars themselves; only the bottom bar's height is reserved here.
        contentWindowInsets = WindowInsets(0),
        bottomBar = {
            if (showBar) {
                NavigationBar(containerColor = Sizzle.colors.surface, tonalElevation = androidx.compose.ui.unit.Dp.Hairline) {
                    tabs.forEach { tab ->
                        val selected = destination?.hasRoute(tab.type) == true
                        NavigationBarItem(
                            selected = selected,
                            onClick = {
                                when (tab.type) {
                                    RecipeEditorRoute::class -> navigator.newRecipe()
                                    else -> navigator.tab(tab.route)
                                }
                            },
                            icon = { Icon(if (selected) tab.selectedIcon else tab.icon, null) },
                            label = { Text(stringResource(tab.label)) },
                            colors = NavigationBarItemDefaults.colors(
                                selectedIconColor = Sizzle.colors.accent,
                                selectedTextColor = Sizzle.colors.accent,
                                indicatorColor = Sizzle.colors.accentSoft,
                                unselectedIconColor = Sizzle.colors.muted,
                                unselectedTextColor = Sizzle.colors.muted,
                            ),
                        )
                    }
                }
            }
        },
    ) { padding ->
        Box(Modifier.fillMaxSize().padding(bottom = padding.calculateBottomPadding())) {
            NavHost(
                navController = nav,
                startDestination = FeedRoute,
                enterTransition = { fadeIn() },
                exitTransition = { fadeOut() },
            ) {
                composable<FeedRoute> { FeedScreen(navigator) }
                composable<ExploreRoute> { ExploreScreen(navigator) }
                composable<ProfileRoute> { ProfileScreen(navigator) }
                composable<RecipeRoute> { RecipeScreen(it.toRoute<RecipeRoute>().id, navigator) }
                composable<RecipeEditorRoute> { entry ->
                    if (session == null) {
                        LaunchedEffect(Unit) { navigator.back(); navigator.login() }
                    } else {
                        RecipeEditorScreen(entry.toRoute<RecipeEditorRoute>().id, navigator)
                    }
                }
                composable<UserRoute> { UserScreen(it.toRoute<UserRoute>().id, navigator) }
                composable<LoginRoute> { LoginScreen(navigator) }
                composable<RegisterRoute> { RegisterScreen(navigator) }
                composable<ConfirmEmailRoute> { ConfirmEmailScreen(it.toRoute<ConfirmEmailRoute>().userId, navigator) }
            }
        }
    }
}
