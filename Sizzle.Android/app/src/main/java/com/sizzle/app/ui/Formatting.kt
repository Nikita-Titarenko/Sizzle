package com.sizzle.app.ui

import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.res.stringResource
import com.sizzle.app.R
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
    "g" -> stringResource(R.string.units_g)
    "ml" -> stringResource(R.string.units_ml)
    "pcs" -> stringResource(R.string.units_pcs)
    else -> unit
}
