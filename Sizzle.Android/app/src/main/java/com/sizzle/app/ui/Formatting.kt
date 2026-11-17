package com.sizzle.app.ui

import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import com.sizzle.app.R
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import java.time.format.FormatStyle

/** The API sends UTC timestamps, sometimes without an offset. */
private fun parseInstant(value: String) = runCatching { OffsetDateTime.parse(value) }
    .recoverCatching { LocalDateTime.parse(value).atOffset(ZoneOffset.UTC) }
    .getOrNull()
    ?.atZoneSameInstant(ZoneId.systemDefault())

/** The local calendar day of an API timestamp. */
fun localDate(value: String): LocalDate? = parseInstant(value)?.toLocalDate()

@Composable
fun formatTime(value: String): String {
    val locale = LocalConfiguration.current.locales[0]
    return parseInstant(value)?.format(DateTimeFormatter.ofLocalizedTime(FormatStyle.SHORT).withLocale(locale)).orEmpty()
}

/** "Today", "Yesterday", or the weekday and date. */
@Composable
fun formatDay(date: LocalDate): String {
    val today = LocalDate.now()
    val locale = LocalConfiguration.current.locales[0]
    return when (date) {
        today -> stringResource(R.string.following_today)
        today.minusDays(1) -> stringResource(R.string.following_yesterday)
        else -> date.format(DateTimeFormatter.ofPattern("EEEE, d MMMM", locale)).replaceFirstChar { it.titlecase(locale) }
    }
}

@Composable
fun formatDate(value: String): String {
    val locale = LocalConfiguration.current.locales[0]
    return parseInstant(value)?.format(DateTimeFormatter.ofLocalizedDate(FormatStyle.MEDIUM).withLocale(locale)).orEmpty()
}

@Composable
fun formatDateTime(value: String): String {
    val locale = LocalConfiguration.current.locales[0]
    return parseInstant(value)?.format(DateTimeFormatter.ofLocalizedDateTime(FormatStyle.SHORT).withLocale(locale)).orEmpty()
}

/** "g" → "г" etc. The API returns short unit codes for catalog and recipe ingredients. */
@Composable
fun unitLabel(unit: String): String = when (unit) {
    "g", "Grams" -> stringResource(R.string.units_g)
    "ml", "Milliliters" -> stringResource(R.string.units_ml)
    "pcs", "Pieces" -> stringResource(R.string.units_pcs)
    else -> unit
}

/** A catalog ingredient's calories as stored: "52 kcal/100 g" or "78 kcal/pc". */
@Composable
fun kcalRate(unit: String, calories: Int): String =
    if (unit == "pcs" || unit == "Pieces") stringResource(R.string.units_kcal_per_piece, calories)
    else stringResource(R.string.units_kcal_per_100, calories, unitLabel(unit))

@Composable
fun kcal(count: Int): String = stringResource(R.string.common_kcal, count)

/** The like count after a like (+1) or unlike (-1), since toggle-like returns only the new state. */
fun Int.withLike(isLiked: Boolean): Int = if (isLiked) this + 1 else (this - 1).coerceAtLeast(0)
