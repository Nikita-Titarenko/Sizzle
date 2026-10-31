package com.sizzle.app.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp

/** The website's design tokens (Sizzle.Client/src/styles/_variables.scss), in both themes. */
@Immutable
data class SizzleColors(
    val background: Color,
    val surface: Color,
    val surface2: Color,
    val surface3: Color,
    val border: Color,
    val borderStrong: Color,
    val text: Color,
    val muted: Color,
    val accent: Color,
    val accentHover: Color,
    /** Accent for text and links, readable on the theme's surfaces. */
    val accentText: Color,
    val accentSoft: Color,
    val onAccent: Color,
    val like: Color,
    val danger: Color,
    val dangerSoft: Color,
    val success: Color,
    val isLight: Boolean,
)

val DarkColors = SizzleColors(
    background = Color(0xFF0B0B0D),
    surface = Color(0xFF141417),
    surface2 = Color(0xFF1C1C21),
    surface3 = Color(0xFF26262D),
    border = Color(0xFF2A2A31),
    borderStrong = Color(0xFF3B3B44),
    text = Color(0xFFF4F4F5),
    muted = Color(0xFF9B9BA6),
    accent = Color(0xFFFF6A2B),
    accentHover = Color(0xFFFF8148),
    accentText = Color(0xFFFF8A57),
    accentSoft = Color(0x24FF6A2B),
    onAccent = Color.White,
    like = Color(0xFFFF3B5C),
    danger = Color(0xFFFF5C5C),
    dangerSoft = Color(0x24FF5C5C),
    success = Color(0xFF4ADE80),
    isLight = false,
)

val LightColors = SizzleColors(
    background = Color(0xFFFFFAF5),
    surface = Color(0xFFFFFFFF),
    surface2 = Color(0xFFFDF5EE),
    surface3 = Color(0xFFF4E9DF),
    border = Color(0xFFEADFD4),
    borderStrong = Color(0xFFD8C8B9),
    text = Color(0xFF2B2118),
    muted = Color(0xFF8A7B6E),
    accent = Color(0xFFE8590C),
    accentHover = Color(0xFFD14D08),
    accentText = Color(0xFFC2410C),
    accentSoft = Color(0x1AE8590C),
    onAccent = Color.White,
    like = Color(0xFFE03150),
    danger = Color(0xFFC92A2A),
    dangerSoft = Color(0x14C92A2A),
    success = Color(0xFF15803D),
    isLight = true,
)

val LocalSizzleColors = staticCompositionLocalOf { DarkColors }

/** Shortcut for the Sizzle tokens inside composables: `Sizzle.colors.muted`. */
object Sizzle {
    val colors: SizzleColors
        @Composable get() = LocalSizzleColors.current
}

private val Typography = Typography().run {
    copy(
        headlineLarge = headlineLarge.copy(fontWeight = FontWeight.Bold, letterSpacing = (-0.5).sp),
        headlineMedium = headlineMedium.copy(fontWeight = FontWeight.Bold, letterSpacing = (-0.4).sp),
        headlineSmall = headlineSmall.copy(fontWeight = FontWeight.Bold),
        titleLarge = titleLarge.copy(fontWeight = FontWeight.Bold),
        titleMedium = titleMedium.copy(fontWeight = FontWeight.SemiBold),
        labelLarge = labelLarge.copy(fontWeight = FontWeight.SemiBold),
    )
}

@Composable
fun SizzleTheme(light: Boolean, content: @Composable () -> Unit) {
    val c = if (light) LightColors else DarkColors
    val scheme = if (light) {
        lightColorScheme(
            primary = c.accent, onPrimary = c.onAccent,
            primaryContainer = c.accentSoft, onPrimaryContainer = c.accentText,
            secondary = c.accent, onSecondary = c.onAccent,
            secondaryContainer = c.accentSoft, onSecondaryContainer = c.accentText,
            background = c.background, onBackground = c.text,
            surface = c.surface, onSurface = c.text,
            surfaceVariant = c.surface2, onSurfaceVariant = c.muted,
            surfaceContainerLowest = c.background, surfaceContainerLow = c.surface,
            surfaceContainer = c.surface, surfaceContainerHigh = c.surface2, surfaceContainerHighest = c.surface3,
            outline = c.borderStrong, outlineVariant = c.border,
            error = c.danger, onError = Color.White,
        )
    } else {
        darkColorScheme(
            primary = c.accent, onPrimary = c.onAccent,
            primaryContainer = c.accentSoft, onPrimaryContainer = c.accentText,
            secondary = c.accent, onSecondary = c.onAccent,
            secondaryContainer = c.accentSoft, onSecondaryContainer = c.accentText,
            background = c.background, onBackground = c.text,
            surface = c.surface, onSurface = c.text,
            surfaceVariant = c.surface2, onSurfaceVariant = c.muted,
            surfaceContainerLowest = c.background, surfaceContainerLow = c.surface,
            surfaceContainer = c.surface, surfaceContainerHigh = c.surface2, surfaceContainerHighest = c.surface3,
            outline = c.borderStrong, outlineVariant = c.border,
            error = c.danger, onError = Color.White,
        )
    }
    CompositionLocalProvider(LocalSizzleColors provides c) {
        MaterialTheme(colorScheme = scheme, typography = Typography, content = content)
    }
}
