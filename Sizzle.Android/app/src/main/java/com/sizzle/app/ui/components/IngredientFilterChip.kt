package com.sizzle.app.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.InputChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.sizzle.app.R
import com.sizzle.app.data.model.IngredientChoice
import com.sizzle.app.ui.unitLabel

/**
 * A chosen ingredient filter. Tapping it sets an optional "up to" amount (only recipes that need
 * no more of it match); the × removes it.
 */
@Composable
fun IngredientFilterChip(
    ingredient: IngredientChoice,
    quantity: Int?,
    onQuantityChange: (Int?) -> Unit,
    onRemove: () -> Unit,
) {
    var editing by rememberSaveable { mutableStateOf(false) }
    val name = listOfNotNull(ingredient.parentName, ingredient.name).joinToString(" › ")
    val unit = unitLabel(ingredient.unit)

    InputChip(
        selected = true,
        onClick = { editing = true },
        label = { Text(if (quantity != null) stringResource(R.string.filter_amount_chip, name, quantity, unit) else name) },
        trailingIcon = {
            Icon(
                Icons.Outlined.Close,
                stringResource(R.string.common_remove, ingredient.name),
                Modifier.size(16.dp).clickable(onClick = onRemove),
            )
        },
        shape = CircleShape,
    )

    if (editing) {
        var text by rememberSaveable { mutableStateOf(quantity?.toString().orEmpty()) }
        fun apply(value: Int?) {
            onQuantityChange(value?.takeIf { it > 0 })
            editing = false
        }
        AlertDialog(
            onDismissRequest = { editing = false },
            title = { Text(stringResource(R.string.filter_amount_title, ingredient.name)) },
            text = {
                Column {
                    Text(stringResource(R.string.filter_amount_hint), style = MaterialTheme.typography.bodyMedium)
                    OutlinedTextField(
                        value = text,
                        onValueChange = { value -> text = value.filter(Char::isDigit).take(6) },
                        label = { Text(stringResource(R.string.filter_amount_label)) },
                        suffix = { Text(unit) },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        modifier = Modifier,
                    )
                }
            },
            confirmButton = {
                TextButton(onClick = { apply(text.toIntOrNull()) }) { Text(stringResource(R.string.filter_amount_apply)) }
            },
            dismissButton = {
                if (quantity != null) {
                    TextButton(onClick = { apply(null) }) { Text(stringResource(R.string.filter_amount_any)) }
                } else {
                    TextButton(onClick = { editing = false }) { Text(stringResource(R.string.common_cancel)) }
                }
            },
        )
    }
}
