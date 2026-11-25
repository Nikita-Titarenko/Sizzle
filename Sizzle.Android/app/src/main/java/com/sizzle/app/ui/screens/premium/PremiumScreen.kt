package com.sizzle.app.ui.screens.premium

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
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.Image
import androidx.compose.material.icons.outlined.Lock
import androidx.compose.material.icons.outlined.Movie
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material.icons.outlined.VisibilityOff
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sizzle.app.AppContainer
import com.sizzle.app.R
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.message
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.SubscriptionCheckout
import com.sizzle.app.data.model.SubscriptionPayment
import com.sizzle.app.data.model.SubscriptionPlan
import com.sizzle.app.data.model.SubscriptionStatus
import com.sizzle.app.ui.appViewModel
import com.sizzle.app.ui.components.AlertBox
import com.sizzle.app.ui.components.ErrorView
import com.sizzle.app.ui.components.Loader
import com.sizzle.app.ui.components.PrimaryButton
import com.sizzle.app.ui.components.SecondaryButton
import com.sizzle.app.ui.components.SectionCard
import com.sizzle.app.ui.formatDate
import com.sizzle.app.ui.navigation.Navigator
import com.sizzle.app.ui.theme.Sizzle
import com.sizzle.app.ui.theme.SizzleColors
import com.stripe.android.PaymentConfiguration
import com.stripe.android.paymentsheet.PaymentSheet
import com.stripe.android.paymentsheet.PaymentSheetResult
import com.stripe.android.paymentsheet.rememberPaymentSheet
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import java.text.NumberFormat
import java.time.Instant
import java.util.Currency
import kotlin.math.ceil
import kotlin.math.roundToInt

/** How often, and how many times, to ask the API whether Stripe's webhook has arrived. */
private const val POLL_INTERVAL_MS = 2_000L
private const val POLL_ATTEMPTS = 20

enum class PremiumStage { Choose, Activating, Activated, Pending }

class PremiumViewModel(private val container: AppContainer) : ViewModel() {
    var plans by mutableStateOf<List<SubscriptionPlan>>(emptyList())
        private set
    var status by mutableStateOf<SubscriptionStatus?>(null)
        private set
    var loadFailure by mutableStateOf<ApiFailure?>(null)
        private set
    var stage by mutableStateOf(PremiumStage.Choose)
        private set

    /** Plan whose checkout is being created. */
    var opening by mutableStateOf<Int?>(null)
        private set

    /** A checkout waiting for the screen to show Stripe's payment sheet. */
    var checkout by mutableStateOf<SubscriptionCheckout?>(null)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set

    /** Stripe's own message when the payment fails (declined card and so on). */
    var paymentError by mutableStateOf<String?>(null)
        private set

    private var orderId: String? = null

    init {
        load()
    }

    fun load() {
        loadFailure = null
        viewModelScope.launch {
            runApi { container.api.getSubscriptionPlans() }
                .onSuccess { plans = it.sortedBy(SubscriptionPlan::durationMonths) }
                .onFailure { loadFailure = it as ApiFailure }
        }
        refreshStatus()
    }

    fun refreshStatus() {
        viewModelScope.launch {
            runApi { container.api.getSubscription() }
                .onSuccess {
                    status = it
                    if (stage == PremiumStage.Pending && it.history.any { order -> order.orderId == orderId }) {
                        stage = PremiumStage.Activated
                    }
                }
                .onFailure { if (status == null) loadFailure = it as ApiFailure }
        }
    }

    fun choose(plan: SubscriptionPlan) {
        if (opening != null || checkout != null) return
        failure = null
        paymentError = null
        opening = plan.id
        viewModelScope.launch {
            runApi { container.api.startCheckout(plan.id) }
                .onSuccess {
                    orderId = it.orderId
                    checkout = it
                }
                .onFailure { failure = it as ApiFailure }
            opening = null
        }
    }

    /** The screen took [checkout] and opened the payment sheet. */
    fun checkoutShown() {
        checkout = null
    }

    fun onPaymentResult(result: PaymentSheetResult) {
        when (result) {
            is PaymentSheetResult.Completed -> waitForActivation()
            is PaymentSheetResult.Canceled -> Unit
            is PaymentSheetResult.Failed -> paymentError = result.error.localizedMessage
        }
    }

    /** The order only counts once the API has heard from Stripe, so wait for it in the history. */
    private fun waitForActivation() {
        val id = orderId ?: return
        stage = PremiumStage.Activating
        viewModelScope.launch {
            repeat(POLL_ATTEMPTS) {
                runApi { container.api.getSubscription() }.onSuccess {
                    status = it
                    if (it.history.any { order -> order.orderId == id }) {
                        stage = PremiumStage.Activated
                        return@launch
                    }
                }
                delay(POLL_INTERVAL_MS)
            }
            stage = PremiumStage.Pending
        }
    }
}

@Composable
fun PremiumScreen(navigator: Navigator) {
    val vm = appViewModel { PremiumViewModel(it) }
    val context = LocalContext.current
    val colors = Sizzle.colors
    val paymentSheet = rememberPaymentSheet(vm::onPaymentResult)
    val merchant = stringResource(R.string.app_name)

    LaunchedEffect(vm.checkout) {
        val checkout = vm.checkout ?: return@LaunchedEffect
        // The key comes from the API with each checkout, so test and live keys never get mixed up.
        PaymentConfiguration.init(context.applicationContext, checkout.publishableKey)
        paymentSheet.presentWithPaymentIntent(
            checkout.clientSecret,
            PaymentSheet.Configuration.Builder(merchant)
                .appearance(sheetAppearance(colors))
                .build(),
        )
        vm.checkoutShown()
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
            Text(stringResource(R.string.premium_title), style = MaterialTheme.typography.headlineSmall)
        }

        val status = vm.status
        val loadFailure = vm.loadFailure
        if (vm.plans.isEmpty() || status == null) {
            if (loadFailure != null) ErrorView(loadFailure, vm::load) else Loader()
            return@Column
        }

        Text(stringResource(R.string.premium_subtitle), color = colors.muted)
        StatusCard(status)
        Perk(Icons.Outlined.Image, R.string.premium_perk_covers, R.string.premium_perk_covers_hint)
        Perk(Icons.Outlined.Movie, R.string.premium_perk_video, R.string.premium_perk_video_hint)
        Perk(Icons.Outlined.VisibilityOff, R.string.premium_perk_no_ads, R.string.premium_perk_no_ads_hint)

        when (vm.stage) {
            PremiumStage.Choose -> Plans(vm, active = status.remainingSeconds > 0)
            PremiumStage.Activating -> SectionCard { Loader(label = stringResource(R.string.premium_activating)) }
            PremiumStage.Activated -> ResultCard(
                Icons.Outlined.CheckCircle, colors.success,
                stringResource(R.string.premium_activated),
                stringResource(R.string.premium_active_until, expiryDate(status.remainingSeconds)),
            ) { PrimaryButton(stringResource(R.string.premium_try_it), navigator::newRecipe, Modifier.fillMaxWidth()) }
            PremiumStage.Pending -> ResultCard(
                Icons.Outlined.Schedule, colors.muted,
                stringResource(R.string.premium_pending_title),
                stringResource(R.string.premium_pending_hint),
            ) { SecondaryButton(stringResource(R.string.premium_check_again), vm::refreshStatus, Modifier.fillMaxWidth()) }
        }

        vm.failure?.let { AlertBox(it.message()) }
        vm.paymentError?.let { AlertBox(it) }

        if (status.history.isNotEmpty()) {
            Text(stringResource(R.string.premium_history), style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(top = 8.dp))
            SectionCard { status.history.forEach { HistoryRow(it) } }
        }
    }
}

/** Profile entry point: Premium's state, tapping opens the plans. */
@Composable
fun PremiumCard(daysLeft: Int?, onOpen: () -> Unit) {
    val c = Sizzle.colors
    val active = (daysLeft ?: 0) > 0
    Row(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(16.dp)).background(if (active) c.surface else c.accentSoft)
            .border(BorderStroke(1.dp, if (active) c.border else c.accent), RoundedCornerShape(16.dp))
            .clickable(onClick = onOpen).padding(16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(Icons.Outlined.AutoAwesome, null, tint = c.accentText, modifier = Modifier.size(24.dp))
        Column(Modifier.weight(1f).padding(horizontal = 12.dp)) {
            Text(stringResource(R.string.premium_title), fontWeight = FontWeight.SemiBold)
            Text(
                if (active) stringResource(R.string.premium_days_left, daysLeft!!) else stringResource(R.string.premium_card_hint),
                color = c.muted, style = MaterialTheme.typography.bodySmall,
            )
        }
        Icon(Icons.AutoMirrored.Filled.KeyboardArrowRight, null, tint = c.muted)
    }
}

@Composable
private fun StatusCard(status: SubscriptionStatus) {
    val c = Sizzle.colors
    val active = status.remainingSeconds > 0
    Row(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(16.dp)).background(c.surface)
            .border(BorderStroke(1.dp, if (active) c.accent else c.border), RoundedCornerShape(16.dp)).padding(16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            Modifier.size(40.dp).clip(CircleShape).background(if (active) c.accentSoft else c.surface3),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                if (active) Icons.Outlined.CheckCircle else Icons.Outlined.AutoAwesome, null,
                tint = if (active) c.accentText else c.muted, modifier = Modifier.size(22.dp),
            )
        }
        Column(Modifier.padding(start = 14.dp)) {
            if (active) {
                Text(stringResource(R.string.premium_active_until, expiryDate(status.remainingSeconds)), fontWeight = FontWeight.SemiBold)
                Text(
                    stringResource(R.string.premium_days_left, ceil(status.remainingSeconds / 86_400.0).toInt()),
                    color = c.muted, style = MaterialTheme.typography.bodySmall,
                )
            } else {
                Text(stringResource(R.string.premium_inactive), fontWeight = FontWeight.SemiBold)
                Text(stringResource(R.string.premium_inactive_hint), color = c.muted, style = MaterialTheme.typography.bodySmall)
            }
        }
    }
}

@Composable
private fun Perk(icon: ImageVector, title: Int, hint: Int) {
    val c = Sizzle.colors
    Row(Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).background(c.surface2).padding(14.dp)) {
        Icon(icon, null, tint = c.accentText, modifier = Modifier.size(22.dp))
        Column(Modifier.padding(start = 12.dp)) {
            Text(stringResource(title), fontWeight = FontWeight.SemiBold)
            Text(stringResource(hint), color = c.muted, style = MaterialTheme.typography.bodySmall)
        }
    }
}

@Composable
private fun Plans(vm: PremiumViewModel, active: Boolean) {
    val c = Sizzle.colors
    val monthly = vm.plans.firstOrNull { it.durationMonths == 1 }?.price
    Text(
        stringResource(if (active) R.string.premium_extend else R.string.premium_choose),
        style = MaterialTheme.typography.titleMedium, modifier = Modifier.padding(top = 8.dp),
    )
    vm.plans.forEach { plan ->
        val best = plan.durationMonths == 12
        val savings = if (monthly != null && plan.durationMonths > 1) {
            ((1 - plan.price / (monthly * plan.durationMonths)) * 100).roundToInt()
        } else 0
        Column(
            Modifier.fillMaxWidth().clip(RoundedCornerShape(16.dp)).background(c.surface)
                .border(BorderStroke(if (best) 2.dp else 1.dp, if (best) c.accent else c.border), RoundedCornerShape(16.dp))
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(planName(plan.id), fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
                if (savings > 0) {
                    Text(
                        stringResource(R.string.premium_save, savings),
                        color = c.onAccent, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.labelSmall,
                        modifier = Modifier.clip(CircleShape).background(c.accent).padding(horizontal = 9.dp, vertical = 3.dp),
                    )
                }
            }
            Text(money(plan.price, plan.currency), style = MaterialTheme.typography.headlineMedium, fontWeight = FontWeight.ExtraBold)
            Text(
                if (plan.durationMonths > 1) stringResource(R.string.premium_per_month, money(plan.price / plan.durationMonths, plan.currency))
                else stringResource(R.string.premium_billed_once),
                color = c.muted, style = MaterialTheme.typography.bodySmall,
            )
            val label = stringResource(R.string.premium_select)
            val enabled = vm.opening == null && vm.checkout == null
            Box(Modifier.padding(top = 10.dp)) {
                if (best) PrimaryButton(label, { vm.choose(plan) }, Modifier.fillMaxWidth(), enabled = enabled, busy = vm.opening == plan.id)
                else SecondaryButton(label, { vm.choose(plan) }, Modifier.fillMaxWidth(), enabled = enabled)
            }
        }
    }
    Row(verticalAlignment = Alignment.CenterVertically) {
        Icon(Icons.Outlined.Lock, null, tint = c.muted, modifier = Modifier.size(14.dp))
        Text(stringResource(R.string.premium_secure), color = c.muted, style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(start = 6.dp))
    }
    Text(stringResource(R.string.premium_one_off), color = c.muted, style = MaterialTheme.typography.bodySmall)
}

@Composable
private fun ResultCard(icon: ImageVector, tint: androidx.compose.ui.graphics.Color, title: String, text: String, action: @Composable () -> Unit) {
    SectionCard {
        Column(Modifier.fillMaxWidth(), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Icon(icon, null, tint = tint, modifier = Modifier.size(44.dp))
            Text(title, style = MaterialTheme.typography.titleLarge, textAlign = TextAlign.Center)
            Text(text, color = Sizzle.colors.muted, textAlign = TextAlign.Center)
            action()
        }
    }
}

@Composable
private fun HistoryRow(order: SubscriptionPayment) {
    Row(Modifier.fillMaxWidth().padding(vertical = 4.dp), verticalAlignment = Alignment.CenterVertically) {
        Column(Modifier.weight(1f)) {
            Text(planName(order.plan), fontWeight = FontWeight.SemiBold)
            Text(
                "${formatDate(order.startsAt)} – ${formatDate(order.expiresAt)}",
                color = Sizzle.colors.muted, style = MaterialTheme.typography.bodySmall,
            )
        }
        Text(money(order.amount, order.currency), fontWeight = FontWeight.SemiBold)
    }
}

@Composable
private fun planName(id: Int): String = stringResource(
    when (id) {
        1 -> R.string.premium_plan_monthly
        2 -> R.string.premium_plan_half_yearly
        else -> R.string.premium_plan_yearly
    },
)

@Composable
private fun expiryDate(remainingSeconds: Long): String = formatDate(Instant.now().plusSeconds(remainingSeconds).toString())

/** "₴399.99" / "399,99 ₴", without ".00" on whole amounts. */
@Composable
internal fun money(amount: Double, currency: String): String {
    val format = NumberFormat.getCurrencyInstance(LocalConfiguration.current.locales[0])
    runCatching { format.currency = Currency.getInstance(currency.uppercase()) }
    if (amount % 1.0 == 0.0) format.maximumFractionDigits = 0
    return format.format(amount)
}

/**
 * The sheet follows the system's dark mode, not the app's own theme switch, so both of its
 * palettes get the colors of the theme the app is showing.
 */
private fun sheetAppearance(c: SizzleColors): PaymentSheet.Appearance {
    val base = if (c.isLight) PaymentSheet.Colors.defaultLight else PaymentSheet.Colors.defaultDark
    val colors = base.copy(
        primary = c.accent.toArgb(),
        surface = c.surface.toArgb(),
        component = c.surface2.toArgb(),
        componentBorder = c.border.toArgb(),
        componentDivider = c.border.toArgb(),
        onComponent = c.text.toArgb(),
        onSurface = c.text.toArgb(),
        subtitle = c.muted.toArgb(),
        placeholderText = c.muted.toArgb(),
        appBarIcon = c.text.toArgb(),
        error = c.danger.toArgb(),
    )
    return PaymentSheet.Appearance(
        colorsLight = colors,
        colorsDark = colors,
        primaryButton = PaymentSheet.PrimaryButton(
            colorsLight = PaymentSheet.PrimaryButtonColors.defaultLight.copy(background = c.accent.toArgb(), onBackground = c.onAccent.toArgb()),
            colorsDark = PaymentSheet.PrimaryButtonColors.defaultDark.copy(background = c.accent.toArgb(), onBackground = c.onAccent.toArgb()),
        ),
    )
}
