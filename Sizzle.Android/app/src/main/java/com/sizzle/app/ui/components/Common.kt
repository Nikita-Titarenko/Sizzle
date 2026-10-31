package com.sizzle.app.ui.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TextFieldColors
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import coil.compose.AsyncImage
import com.sizzle.app.R
import com.sizzle.app.data.api.ApiClient
import com.sizzle.app.ui.theme.Sizzle

/** Round profile photo; falls back to the first letter of the name on a tinted circle. */
@Composable
fun UserAvatar(name: String, imageUrl: String?, modifier: Modifier = Modifier, size: Dp = 32.dp) {
    val colors = Sizzle.colors
    Box(
        modifier.size(size).clip(CircleShape).background(colors.accentSoft),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            name.trim().take(1).uppercase(),
            color = colors.accentText,
            fontWeight = FontWeight.Bold,
            fontSize = (size.value * 0.42f).sp,
        )
        if (imageUrl != null) {
            AsyncImage(
                model = ApiClient.mediaUrl(imageUrl),
                contentDescription = name,
                contentScale = ContentScale.Crop,
                modifier = Modifier.size(size),
            )
        }
    }
}

@Composable
fun PrimaryButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    busy: Boolean = false,
) {
    Button(
        onClick = onClick,
        enabled = enabled && !busy,
        modifier = modifier.height(48.dp),
        shape = RoundedCornerShape(10.dp),
        colors = ButtonDefaults.buttonColors(containerColor = Sizzle.colors.accent, contentColor = Sizzle.colors.onAccent),
    ) {
        if (busy) {
            CircularProgressIndicator(Modifier.size(18.dp), color = Sizzle.colors.onAccent, strokeWidth = 2.dp)
        } else {
            Text(text, fontWeight = FontWeight.SemiBold)
        }
    }
}

@Composable
fun SecondaryButton(text: String, onClick: () -> Unit, modifier: Modifier = Modifier, enabled: Boolean = true) {
    OutlinedButton(
        onClick = onClick,
        enabled = enabled,
        modifier = modifier,
        shape = RoundedCornerShape(10.dp),
        border = BorderStroke(1.dp, Sizzle.colors.accent),
        colors = ButtonDefaults.outlinedButtonColors(contentColor = Sizzle.colors.accentText),
    ) {
        Text(text, fontWeight = FontWeight.SemiBold)
    }
}

/** Follow / Following pill. */
@Composable
fun FollowButton(following: Boolean, onClick: () -> Unit, modifier: Modifier = Modifier) {
    val text = stringResource(if (following) R.string.common_following else R.string.common_follow)
    if (following) {
        OutlinedButton(
            onClick, modifier.height(34.dp), shape = CircleShape,
            border = BorderStroke(1.dp, Sizzle.colors.borderStrong),
            contentPadding = ButtonDefaults.TextButtonContentPadding,
            colors = ButtonDefaults.outlinedButtonColors(contentColor = Sizzle.colors.text),
        ) { Text(text, fontWeight = FontWeight.SemiBold) }
    } else {
        Button(
            onClick, modifier.height(34.dp), shape = CircleShape,
            contentPadding = ButtonDefaults.TextButtonContentPadding,
            colors = ButtonDefaults.buttonColors(containerColor = Sizzle.colors.accent, contentColor = Sizzle.colors.onAccent),
        ) { Text(text, fontWeight = FontWeight.SemiBold) }
    }
}

/** "Continue with Google", styled after Google's sign-in button guidelines for the current theme. */
@Composable
fun GoogleButton(onClick: () -> Unit, modifier: Modifier = Modifier, busy: Boolean = false) {
    val light = Sizzle.colors.isLight
    val background = if (light) Color.White else Color(0xFF131314)
    val border = if (light) Color(0xFF747775) else Color(0xFF8E918F)
    val text = if (light) Color(0xFF1F1F1F) else Color(0xFFE3E3E3)
    Surface(
        onClick = onClick,
        enabled = !busy,
        modifier = modifier.fillMaxWidth().height(48.dp),
        shape = RoundedCornerShape(10.dp),
        color = background,
        contentColor = text,
        border = BorderStroke(1.dp, border),
    ) {
        Row(horizontalArrangement = Arrangement.Center, verticalAlignment = Alignment.CenterVertically) {
            if (busy) {
                CircularProgressIndicator(Modifier.size(18.dp), color = text, strokeWidth = 2.dp)
            } else {
                Image(painterResource(R.drawable.ic_google), null, Modifier.size(20.dp))
                Text(
                    stringResource(R.string.auth_continue_with_google),
                    modifier = Modifier.padding(start = 12.dp),
                    fontWeight = FontWeight.Medium,
                    letterSpacing = 0.15.sp,
                )
            }
        }
    }
}

/** Horizontal rule with a word in the middle ("or"). */
@Composable
fun OrDivider(text: String, modifier: Modifier = Modifier) {
    Row(modifier.fillMaxWidth().padding(vertical = 20.dp), verticalAlignment = Alignment.CenterVertically) {
        HorizontalDivider(Modifier.weight(1f), color = Sizzle.colors.border)
        Text(text, color = Sizzle.colors.muted, style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(horizontal = 12.dp))
        HorizontalDivider(Modifier.weight(1f), color = Sizzle.colors.border)
    }
}

/** Inline error box under a form. */
@Composable
fun AlertBox(text: String, modifier: Modifier = Modifier) {
    val c = Sizzle.colors
    Text(
        text,
        color = c.danger,
        style = MaterialTheme.typography.bodyMedium,
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(10.dp))
            .background(c.dangerSoft)
            .border(1.dp, c.danger.copy(alpha = 0.35f), RoundedCornerShape(10.dp))
            .padding(horizontal = 14.dp, vertical = 10.dp),
    )
}

/** Rounded bordered section, like the website's .card / .panel. */
@Composable
fun SectionCard(modifier: Modifier = Modifier, content: @Composable ColumnScope.() -> Unit) {
    Column(
        modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(18.dp))
            .background(Sizzle.colors.surface)
            .border(1.dp, Sizzle.colors.border, RoundedCornerShape(18.dp))
            .padding(18.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
        content = content,
    )
}

@Composable
fun ConfirmDialog(text: String, confirm: String, onConfirm: () -> Unit, onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        text = { Text(text, style = MaterialTheme.typography.bodyLarge) },
        confirmButton = {
            TextButton(onClick = { onDismiss(); onConfirm() }) { Text(confirm, color = Sizzle.colors.danger) }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.common_cancel)) } },
        containerColor = Sizzle.colors.surface,
    )
}

/** Outlined text field colors matching the website's inputs. */
@Composable
fun sizzleFieldColors(): TextFieldColors {
    val c = Sizzle.colors
    return OutlinedTextFieldDefaults.colors(
        focusedContainerColor = c.surface2,
        unfocusedContainerColor = c.surface2,
        disabledContainerColor = c.surface2,
        focusedBorderColor = c.accent,
        unfocusedBorderColor = c.border,
        cursorColor = c.accent,
        focusedLabelColor = c.accentText,
        unfocusedPlaceholderColor = c.muted,
        focusedPlaceholderColor = c.muted,
    )
}

val FieldShape = RoundedCornerShape(10.dp)
