package com.sizzle.app.ui.components

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.scale
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.rotate
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.graphics.drawscope.withTransform
import androidx.compose.ui.graphics.vector.PathParser
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.sizzle.app.R
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.message
import com.sizzle.app.ui.theme.Sizzle
import kotlinx.coroutines.delay

/** Spinner around the app emblem. Appears after a short delay so fast loads don't flash it. */
@Composable
fun Loader(modifier: Modifier = Modifier, label: String? = null) {
    var visible by remember { mutableStateOf(false) }
    LaunchedEffect(Unit) {
        delay(150)
        visible = true
    }
    val colors = Sizzle.colors
    val transition = rememberInfiniteTransition(label = "loader")
    val angle by transition.animateFloat(
        initialValue = 0f, targetValue = 360f,
        animationSpec = infiniteRepeatable(tween(800, easing = LinearEasing)), label = "spin",
    )
    val pulse by transition.animateFloat(
        initialValue = 1f, targetValue = 0.86f,
        animationSpec = infiniteRepeatable(tween(800), RepeatMode.Reverse), label = "pulse",
    )
    Box(modifier.fillMaxWidth().padding(vertical = 48.dp), contentAlignment = Alignment.Center) {
        AnimatedVisibility(visible, enter = fadeIn()) {
            Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(14.dp)) {
                Box(Modifier.size(64.dp), contentAlignment = Alignment.Center) {
                    Canvas(Modifier.size(64.dp)) {
                        val stroke = 3.dp.toPx()
                        val inset = stroke / 2
                        val arcSize = Size(size.width - stroke, size.height - stroke)
                        drawArc(colors.surface3, 0f, 360f, false, Offset(inset, inset), arcSize, style = Stroke(stroke))
                        rotate(angle) {
                            drawArc(colors.accent, -90f, 180f, false, Offset(inset, inset), arcSize,
                                style = Stroke(stroke, cap = StrokeCap.Round))
                        }
                    }
                    Image(painterResource(R.drawable.logo_mark), null, Modifier.size(34.dp).scale(pulse))
                }
                Text(label ?: stringResource(R.string.common_loading), color = colors.muted, style = MaterialTheme.typography.bodyMedium)
            }
        }
    }
}

enum class StatusKind { Error, Empty }

/** Big message with an illustration, for failed loads and empty results. [actions] go underneath. */
@Composable
fun StatusView(
    heading: String,
    modifier: Modifier = Modifier,
    kind: StatusKind = StatusKind.Error,
    message: String? = null,
    actions: @Composable ColumnScope.() -> Unit = {},
) {
    Column(
        modifier.fillMaxWidth().padding(horizontal = 24.dp, vertical = 40.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        if (kind == StatusKind.Error) BurntPan(Modifier.size(200.dp, 160.dp)) else EmptyPlate(Modifier.size(200.dp, 160.dp))
        Spacer(Modifier.height(12.dp))
        Text(heading, style = MaterialTheme.typography.headlineSmall, textAlign = TextAlign.Center)
        if (!message.isNullOrBlank()) {
            Spacer(Modifier.height(6.dp))
            Text(
                message,
                color = Sizzle.colors.muted,
                style = MaterialTheme.typography.bodyLarge,
                textAlign = TextAlign.Center,
                modifier = Modifier.widthIn(max = 440.dp),
            )
        }
        Spacer(Modifier.height(18.dp))
        Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(10.dp), content = actions)
    }
}

/** [StatusView] for a failed request, with a retry button. */
@Composable
fun ErrorView(failure: ApiFailure, onRetry: (() -> Unit)?, modifier: Modifier = Modifier) {
    StatusView(stringResource(R.string.errors_title), modifier, message = failure.message()) {
        if (onRetry != null) {
            PrimaryButton(stringResource(R.string.common_try_again), onRetry)
        }
    }
}

// The illustrations are the website's SVGs (status-view.html), drawn in a 200×160 viewBox.

private val parser = PathParser()
private fun path(d: String) = parser.parsePathString(d).toPath()

private val smokePaths = listOf(
    path("M70 66c-8-8 8-14 0-24s6-16 0-24"),
    path("M92 60c-8-8 8-14 0-24s6-16 0-24"),
    path("M114 66c-8-8 8-14 0-24s6-16 0-24"),
)
private val panPath = path("M38 92h108v6a28 28 0 0 1-28 28H66a28 28 0 0 1-28-28z")
private val facePath = path("M72 103l7 7m0-7l-7 7M105 103l7 7m0-7l-7 7M84 120q8-6 16 0")
private val forkPath = path("M38 40v28a8 8 0 0 0 8 8v52M46 40v24M54 40v28a8 8 0 0 1-8 8")
private val knifePath = path("M160 128V40c-10 6-14 20-14 38h14")

private fun DrawScope.inViewBox(block: DrawScope.() -> Unit) {
    val scale = minOf(size.width / 200f, size.height / 160f)
    withTransform({ scale(scale, scale, Offset.Zero) }, block)
}

@Composable
private fun BurntPan(modifier: Modifier) {
    val c = Sizzle.colors
    val transition = rememberInfiniteTransition(label = "smoke")
    val phase by transition.animateFloat(0f, 1f, infiniteRepeatable(tween(2400, easing = LinearEasing)), label = "phase")
    Canvas(modifier) {
        inViewBox {
            drawCircle(c.accentSoft, 66f, Offset(96f, 88f))
            smokePaths.forEachIndexed { i, smoke ->
                val t = (phase + i / 3f) % 1f
                val alpha = if (t < 0.3f) t / 0.3f * 0.8f else (1f - t) / 0.7f * 0.8f
                translate(top = 10f - 24f * t) {
                    drawPath(smoke, c.muted.copy(alpha = alpha), style = Stroke(4f, cap = StrokeCap.Round))
                }
            }
            drawRoundRect(c.accent, Offset(140f, 90f), Size(48f, 11f), CornerRadius(5.5f))
            drawPath(panPath, c.surface3)
            drawPath(panPath, c.borderStrong, style = Stroke(2f))
            drawOval(c.surface2, Offset(38f, 83f), Size(108f, 18f))
            drawOval(c.borderStrong, Offset(38f, 83f), Size(108f, 18f), style = Stroke(2f))
            drawPath(facePath, c.text, style = Stroke(3f, cap = StrokeCap.Round))
        }
    }
}

@Composable
private fun EmptyPlate(modifier: Modifier) {
    val c = Sizzle.colors
    Canvas(modifier) {
        inViewBox {
            drawCircle(c.accentSoft, 66f, Offset(100f, 84f))
            val cutlery = Stroke(4f, cap = StrokeCap.Round, join = androidx.compose.ui.graphics.StrokeJoin.Round)
            drawPath(forkPath, c.muted, style = cutlery)
            drawPath(knifePath, c.muted, style = cutlery)
            drawCircle(c.surface3, 42f, Offset(100f, 84f))
            drawCircle(c.borderStrong, 42f, Offset(100f, 84f), style = Stroke(2f))
            drawCircle(c.surface2, 28f, Offset(100f, 84f))
            drawCircle(c.borderStrong, 28f, Offset(100f, 84f), style = Stroke(2f))
        }
    }
}
