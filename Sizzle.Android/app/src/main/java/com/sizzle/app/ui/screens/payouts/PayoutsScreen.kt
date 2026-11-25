package com.sizzle.app.ui.screens.payouts

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.Group
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.Movie
import androidx.compose.material.icons.outlined.Visibility
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.compose.LifecycleResumeEffect
import androidx.lifecycle.viewModelScope
import com.sizzle.app.AppContainer
import com.sizzle.app.R
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.message
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.AdEarning
import com.sizzle.app.data.model.ConnectedAccountStatus
import com.sizzle.app.ui.appViewModel
import com.sizzle.app.ui.components.AlertBox
import com.sizzle.app.ui.components.ErrorView
import com.sizzle.app.ui.components.Loader
import com.sizzle.app.ui.components.PrimaryButton
import com.sizzle.app.ui.components.SecondaryButton
import com.sizzle.app.ui.components.SectionCard
import com.sizzle.app.ui.formatDate
import com.sizzle.app.ui.navigation.Navigator
import com.sizzle.app.ui.screens.premium.money
import com.sizzle.app.ui.theme.Sizzle
import kotlinx.coroutines.launch
import java.text.NumberFormat

private const val PAGE_SIZE = 10

/** Which Stripe page is being opened. */
enum class StripeLink { Onboarding, Dashboard }

/**
 * Creator payouts: authors with enough followers and recent views set up a Stripe payout
 * profile and are paid every month for the ad views on their recipes.
 */
class PayoutsViewModel(private val container: AppContainer) : ViewModel() {
    var status by mutableStateOf<ConnectedAccountStatus?>(null)
        private set
    var loadFailure by mutableStateOf<ApiFailure?>(null)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set

    /** Stripe returned no link (not configured or unavailable). */
    var stripeUnavailable by mutableStateOf(false)
        private set
    var opening by mutableStateOf<StripeLink?>(null)
        private set

    /** The user went to Stripe's onboarding; until Stripe's webhook arrives, say it's being checked. */
    var onboardingStarted by mutableStateOf(false)
        private set

    var earnings by mutableStateOf<List<AdEarning>>(emptyList())
        private set
    var earningsTotal by mutableStateOf<Int?>(null)
        private set
    var loadingMore by mutableStateOf(false)
        private set
    private var nextPage = 0

    init {
        load()
    }

    fun load() {
        loadFailure = null
        refreshStatus()
        nextPage = 0
        earnings = emptyList()
        earningsTotal = null
        loadMore()
    }

    fun refreshStatus() {
        viewModelScope.launch {
            runApi { container.api.getPayoutStatus() }
                .onSuccess { status = it }
                .onFailure { if (status == null) loadFailure = it as ApiFailure }
        }
    }

    fun loadMore() {
        if (loadingMore) return
        loadingMore = true
        viewModelScope.launch {
            runApi { container.api.getEarnings(nextPage, PAGE_SIZE) }
                .onSuccess {
                    earnings = earnings + it.data
                    earningsTotal = it.totalSize
                    nextPage++
                }
                .onFailure { failure = it as ApiFailure }
            loadingMore = false
        }
    }

    /** Stripe's links are single-use and expire within minutes, so every tap gets a new one. */
    fun open(link: StripeLink, openUri: (String) -> Unit) {
        if (opening != null) return
        opening = link
        failure = null
        stripeUnavailable = false
        viewModelScope.launch {
            runApi { container.api.getPayoutStatus() }
                .onSuccess {
                    status = it
                    val url = if (link == StripeLink.Onboarding) it.onboardingLink else it.expressDashboardLoginLink
                    if (url == null) {
                        stripeUnavailable = true
                    } else {
                        if (link == StripeLink.Onboarding) onboardingStarted = true
                        runCatching { openUri(url) }.onFailure { stripeUnavailable = true }
                    }
                }
                .onFailure { failure = it as ApiFailure }
            opening = null
        }
    }
}

@Composable
fun PayoutsScreen(navigator: Navigator) {
    val vm = appViewModel { PayoutsViewModel(it) }
    val colors = Sizzle.colors
    val uriHandler = LocalUriHandler.current

    // Back from Stripe in the browser: the profile may be ready now.
    LifecycleResumeEffect(Unit) {
        if (vm.onboardingStarted) vm.refreshStatus()
        onPauseOrDispose { }
    }

    Column(
        Modifier.fillMaxSize().background(colors.background).statusBarsPadding().navigationBarsPadding()
            .verticalScroll(rememberScrollState()).padding(horizontal = 16.dp, vertical = 8.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = navigator::back) {
                Icon(Icons.AutoMirrored.Filled.ArrowBack, stringResource(R.string.nav_back), tint = colors.text)
            }
            Text(stringResource(R.string.payouts_title), style = MaterialTheme.typography.headlineSmall)
        }

        val status = vm.status
        val loadFailure = vm.loadFailure
        if (status == null) {
            if (loadFailure != null) ErrorView(loadFailure, vm::load) else Loader()
            return@Column
        }

        Text(stringResource(R.string.payouts_subtitle), color = colors.muted)
        StateCard(vm, status) { link -> vm.open(link) { uriHandler.openUri(it) } }

        vm.failure?.let { AlertBox(it.message()) }
        if (vm.stripeUnavailable) AlertBox(stringResource(R.string.payouts_stripe_unavailable))

        Goal(Icons.Outlined.Group, R.string.payouts_followers, status.followersCount, status.requiredFollowersCount)
        Goal(Icons.Outlined.Visibility, R.string.payouts_views, status.recipeViewsLast90Days, status.requiredViewsLast90Days)

        Text(stringResource(R.string.payouts_how_title), style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(top = 8.dp))
        HowItem(Icons.Outlined.Movie, R.string.payouts_how_ads)
        HowItem(Icons.Outlined.AutoAwesome, R.string.payouts_how_premium)
        HowItem(Icons.Outlined.AccountBalanceWallet, R.string.payouts_how_monthly)

        Text(stringResource(R.string.payouts_history), style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(top = 8.dp))
        val total = vm.earningsTotal
        when {
            total == null -> Loader()
            vm.earnings.isEmpty() -> Row(verticalAlignment = Alignment.CenterVertically) {
                Text(stringResource(R.string.payouts_no_history), color = colors.muted)
                TextButton(onClick = navigator::newRecipe) { Text(stringResource(R.string.payouts_write_recipe), color = colors.accentText) }
            }
            else -> SectionCard {
                vm.earnings.forEachIndexed { index, earning ->
                    if (index > 0) HorizontalDivider(color = colors.border)
                    EarningRow(earning)
                }
                if (vm.earnings.size < total) {
                    SecondaryButton(
                        stringResource(R.string.payouts_show_more), vm::loadMore, Modifier.fillMaxWidth(),
                        enabled = !vm.loadingMore,
                    )
                }
            }
        }
    }
}

/** Where the user stands: not eligible, ready to set up, or being paid. */
@Composable
private fun StateCard(vm: PayoutsViewModel, status: ConnectedAccountStatus, onOpen: (StripeLink) -> Unit) {
    val c = Sizzle.colors
    val (icon, tint, background, border) = when {
        status.isStripeProfileConfigured -> Quad(Icons.Outlined.CheckCircle, c.success, c.success.copy(alpha = 0.16f), c.success.copy(alpha = 0.5f))
        status.isEligible -> Quad(Icons.Outlined.AccountBalanceWallet, c.accentText, c.accentSoft, c.accent)
        else -> Quad(Icons.Outlined.Lock, c.muted, c.surface3, c.border)
    }
    val (title, hint) = when {
        status.isStripeProfileConfigured -> R.string.payouts_active_title to R.string.payouts_active_hint
        status.isEligible && vm.onboardingStarted -> R.string.payouts_checking_title to R.string.payouts_checking_hint
        status.isEligible -> R.string.payouts_eligible_title to R.string.payouts_eligible_hint
        else -> R.string.payouts_not_eligible_title to R.string.payouts_not_eligible_hint
    }
    Column(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(16.dp)).background(c.surface)
            .border(BorderStroke(1.dp, border), RoundedCornerShape(16.dp)).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(40.dp).clip(CircleShape).background(background), contentAlignment = Alignment.Center) {
                Icon(icon, null, tint = tint, modifier = Modifier.size(22.dp))
            }
            Column(Modifier.padding(start = 14.dp)) {
                Text(stringResource(title), fontWeight = FontWeight.SemiBold)
                Text(stringResource(hint), color = c.muted, style = MaterialTheme.typography.bodySmall)
            }
        }
        when {
            status.isStripeProfileConfigured -> PrimaryButton(
                stringResource(R.string.payouts_dashboard), { onOpen(StripeLink.Dashboard) }, Modifier.fillMaxWidth(),
                busy = vm.opening == StripeLink.Dashboard,
            )
            status.isEligible -> {
                if (vm.onboardingStarted) {
                    SecondaryButton(stringResource(R.string.payouts_check_again), vm::refreshStatus, Modifier.fillMaxWidth())
                }
                PrimaryButton(
                    stringResource(if (vm.onboardingStarted) R.string.payouts_continue_setup else R.string.payouts_setup),
                    { onOpen(StripeLink.Onboarding) }, Modifier.fillMaxWidth(),
                    enabled = vm.opening == null, busy = vm.opening == StripeLink.Onboarding,
                )
            }
        }
    }
}

private data class Quad(val icon: ImageVector, val tint: Color, val background: Color, val border: Color)

@Composable
private fun Goal(icon: ImageVector, label: Int, value: Int, required: Int) {
    val c = Sizzle.colors
    val done = value >= required
    val number = NumberFormat.getIntegerInstance(LocalConfiguration.current.locales[0])
    SectionCard {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(icon, null, tint = if (done) c.success else c.muted, modifier = Modifier.size(20.dp))
            Text(stringResource(label), fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f).padding(horizontal = 10.dp))
            Text("${number.format(value)} / ${number.format(required)}", color = if (done) c.success else c.muted, style = MaterialTheme.typography.bodySmall)
        }
        LinearProgressIndicator(
            progress = { if (required > 0) (value.toFloat() / required).coerceIn(0f, 1f) else 1f },
            modifier = Modifier.fillMaxWidth().height(8.dp).clip(CircleShape),
            color = if (done) c.success else c.accent,
            trackColor = c.surface3,
            strokeCap = StrokeCap.Round,
            gapSize = 0.dp,
            drawStopIndicator = {},
        )
    }
}

@Composable
private fun HowItem(icon: ImageVector, text: Int) {
    val c = Sizzle.colors
    Row(Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).background(c.surface2).padding(14.dp)) {
        Icon(icon, null, tint = c.accentText, modifier = Modifier.size(22.dp))
        Text(stringResource(text), style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(start = 12.dp))
    }
}

@Composable
private fun EarningRow(earning: AdEarning) {
    val c = Sizzle.colors
    val number = NumberFormat.getIntegerInstance(LocalConfiguration.current.locales[0])
    Row(verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text(formatDate(earning.createdAt), fontWeight = FontWeight.SemiBold)
            Text(
                stringResource(R.string.payouts_ad_views, number.format(earning.adViewCount)),
                color = c.muted, style = MaterialTheme.typography.bodySmall,
            )
            Text(
                earning.stripeTransferId, color = c.muted, fontFamily = FontFamily.Monospace,
                style = MaterialTheme.typography.labelSmall, maxLines = 1, overflow = TextOverflow.Ellipsis,
            )
        }
        Text(money(earning.amount, earning.currency), fontWeight = FontWeight.Bold, modifier = Modifier.padding(start = 12.dp))
    }
}

/** Profile entry point, next to the Premium card. */
@Composable
fun PayoutsCard(onOpen: () -> Unit) {
    val c = Sizzle.colors
    Row(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(16.dp)).background(c.surface)
            .border(BorderStroke(1.dp, c.border), RoundedCornerShape(16.dp))
            .clickable(onClick = onOpen).padding(16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(Icons.Outlined.AccountBalanceWallet, null, tint = c.accentText, modifier = Modifier.size(24.dp))
        Column(Modifier.weight(1f).padding(horizontal = 12.dp)) {
            Text(stringResource(R.string.payouts_title), fontWeight = FontWeight.SemiBold)
            Text(stringResource(R.string.payouts_card_hint), color = c.muted, style = MaterialTheme.typography.bodySmall)
        }
        Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, null, tint = c.muted)
    }
}
