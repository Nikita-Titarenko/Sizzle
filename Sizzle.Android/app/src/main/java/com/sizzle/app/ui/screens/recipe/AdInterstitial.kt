package com.sizzle.app.ui.screens.recipe

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.outlined.AccountBalanceWallet
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.RestaurantMenu
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.IconButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.lifecycle.Lifecycle
import com.sizzle.app.R
import com.sizzle.app.ui.components.PrimaryButton
import com.sizzle.app.ui.theme.Sizzle
import kotlinx.coroutines.delay
import kotlin.math.ceil

/** How long the ad stays up before it can be closed, and so counts as watched. */
private const val AD_MS = 5_000
private const val TICK_MS = 100L

/** Where an ad's button leads. */
enum class AdTarget { Premium, NewRecipe, Payouts }

private class Creative(val target: AdTarget, val icon: ImageVector, val title: Int, val text: Int, val cta: Int)

/** In-house ads; Sizzle shows its own offers rather than a third-party network. */
private val creatives = listOf(
    Creative(AdTarget.Premium, Icons.Outlined.AutoAwesome, R.string.ads_premium_title, R.string.ads_premium_text, R.string.ads_premium_cta),
    Creative(AdTarget.NewRecipe, Icons.Outlined.RestaurantMenu, R.string.ads_create_title, R.string.ads_create_text, R.string.ads_create_cta),
    Creative(AdTarget.Payouts, Icons.Outlined.AccountBalanceWallet, R.string.ads_payouts_title, R.string.ads_payouts_text, R.string.ads_payouts_cta),
)

/**
 * Full-screen ad over a recipe for readers without Premium. It can only be closed (with the
 * cross or Back) once the countdown ends; [onClosed] then gets `true`, so the author is paid
 * for the view. Its button reports whether the countdown had ended and opens [AdTarget].
 * The countdown stops while the app is in the background.
 */
@Composable
fun AdInterstitial(onClosed: (watched: Boolean) -> Unit, onFollow: (AdTarget) -> Unit) {
    val c = Sizzle.colors
    val creative = creatives[rememberSaveable { (creatives.indices).random() }]
    var elapsed by rememberSaveable { mutableIntStateOf(0) }
    val done = elapsed >= AD_MS
    val lifecycle = LocalLifecycleOwner.current.lifecycle

    LaunchedEffect(Unit) {
        while (elapsed < AD_MS) {
            delay(TICK_MS)
            if (lifecycle.currentState.isAtLeast(Lifecycle.State.RESUMED)) elapsed += TICK_MS.toInt()
        }
    }

    val tint = when (creative.target) {
        AdTarget.Premium -> c.accent
        AdTarget.NewRecipe -> c.success
        AdTarget.Payouts -> Color(0xFFF5B942)
    }
    val secondsLeft = ceil((AD_MS - elapsed).coerceAtLeast(0) / 1000.0).toInt()
    val closeInText = stringResource(R.string.ads_close_in, secondsLeft)

    Dialog(
        onDismissRequest = { if (done) onClosed(true) },
        properties = DialogProperties(
            usePlatformDefaultWidth = false,
            dismissOnClickOutside = false,
            decorFitsSystemWindows = false,
        ),
    ) {
        Column(
            Modifier.fillMaxSize().background(c.background).statusBarsPadding().navigationBarsPadding().padding(16.dp),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    stringResource(R.string.ads_label).uppercase(),
                    color = c.muted, fontWeight = FontWeight.Bold, style = MaterialTheme.typography.labelSmall,
                    modifier = Modifier.clip(RoundedCornerShape(6.dp)).background(c.surface3).padding(horizontal = 8.dp, vertical = 3.dp),
                )
                Spacer(Modifier.weight(1f))
                AnimatedContent(done, transitionSpec = { fadeIn() togetherWith fadeOut() }, label = "ad-close") { canClose ->
                    if (canClose) {
                        IconButton(
                            onClick = { onClosed(true) },
                            colors = IconButtonDefaults.iconButtonColors(containerColor = c.surface3, contentColor = c.text),
                        ) {
                            Icon(Icons.Filled.Close, stringResource(R.string.ads_close))
                        }
                    } else {
                        Box(
                            Modifier.size(48.dp).semantics { contentDescription = closeInText },
                            contentAlignment = Alignment.Center,
                        ) {
                            CircularProgressIndicator(
                                progress = { elapsed / AD_MS.toFloat() },
                                modifier = Modifier.size(36.dp),
                                color = c.accent, trackColor = c.surface3, strokeWidth = 3.dp,
                            )
                            Text("$secondsLeft", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.labelLarge)
                        }
                    }
                }
            }

            Column(
                Modifier.weight(1f).fillMaxWidth().padding(vertical = 16.dp).clip(RoundedCornerShape(24.dp))
                    .background(Brush.verticalGradient(listOf(tint.copy(alpha = 0.28f), c.surface2)))
                    .border(1.dp, c.border, RoundedCornerShape(24.dp)).padding(24.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(14.dp, Alignment.CenterVertically),
            ) {
                Box(
                    Modifier.size(88.dp).clip(RoundedCornerShape(26.dp)).background(tint.copy(alpha = 0.2f)),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(creative.icon, null, tint = tint, modifier = Modifier.size(44.dp))
                }
                Text(stringResource(creative.title), style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.Bold, textAlign = TextAlign.Center)
                Text(stringResource(creative.text), color = c.muted, textAlign = TextAlign.Center)
                PrimaryButton(
                    stringResource(creative.cta),
                    { onFollow(creative.target); onClosed(done) },
                    Modifier.fillMaxWidth().padding(top = 8.dp),
                )
            }

            Text(
                if (done) stringResource(R.string.ads_thanks) else closeInText,
                color = c.muted, style = MaterialTheme.typography.bodySmall, textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth(),
            )
        }
    }
}
