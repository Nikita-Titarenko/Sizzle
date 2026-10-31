package com.sizzle.app.ui.components

import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import com.sizzle.app.R
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.Ingredient
import com.sizzle.app.data.model.IngredientChoice
import com.sizzle.app.ui.LocalAppContainer
import com.sizzle.app.ui.theme.Sizzle
import com.sizzle.app.ui.unitLabel
import kotlinx.coroutines.delay

/**
 * Search field over the ingredient catalog. Results show variants under their parent
 * ("Potato › Sweet Potato"); picking one calls [onPicked] and clears the field.
 */
@Composable
fun IngredientPicker(
    placeholder: String,
    onPicked: (IngredientChoice) -> Unit,
    modifier: Modifier = Modifier,
    excludeIds: Set<String> = emptySet(),
) {
    val api = LocalAppContainer.current.api
    val focus = LocalFocusManager.current
    var query by remember { mutableStateOf("") }
    var results by remember { mutableStateOf<List<IngredientChoice>?>(null) }

    LaunchedEffect(query) {
        if (query.isBlank()) {
            results = null
            return@LaunchedEffect
        }
        delay(250)
        results = null
        results = runApi { api.getIngredients(query.trim()) }.getOrNull()?.let(::flatten).orEmpty()
    }

    Column(modifier) {
        OutlinedTextField(
            value = query,
            onValueChange = { query = it },
            modifier = Modifier.fillMaxWidth(),
            placeholder = { Text(placeholder) },
            leadingIcon = { Icon(Icons.Outlined.Search, null) },
            trailingIcon = {
                if (query.isNotEmpty()) {
                    IconButton(onClick = { query = "" }) { Icon(Icons.Outlined.Close, stringResource(R.string.common_close)) }
                }
            },
            singleLine = true,
            shape = FieldShape,
            colors = sizzleFieldColors(),
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
        )
        if (query.isNotBlank()) {
            val shape = RoundedCornerShape(12.dp)
            Surface(
                Modifier.fillMaxWidth().padding(top = 6.dp).border(1.dp, Sizzle.colors.border, shape),
                shape = shape,
                color = Sizzle.colors.surface,
                shadowElevation = 6.dp,
            ) {
                Column(Modifier.heightIn(max = 280.dp).verticalScroll(rememberScrollState())) {
                    val list = results
                    when {
                        list == null -> Hint(stringResource(R.string.ingredients_searching))
                        list.isEmpty() -> Hint(stringResource(R.string.ingredients_none))
                        else -> list.forEach { choice ->
                            val added = choice.id in excludeIds
                            Row(
                                Modifier
                                    .fillMaxWidth()
                                    .clip(RoundedCornerShape(8.dp))
                                    .clickable(enabled = !added) {
                                        onPicked(choice)
                                        query = ""
                                        focus.clearFocus()
                                    }
                                    .padding(horizontal = 14.dp, vertical = 11.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                if (choice.parentName != null) {
                                    Text("${choice.parentName} › ", color = Sizzle.colors.muted)
                                }
                                Text(
                                    choice.name,
                                    fontWeight = if (choice.parentName == null) FontWeight.SemiBold else FontWeight.Normal,
                                    color = if (added) Sizzle.colors.muted else Sizzle.colors.text,
                                    modifier = Modifier.weight(1f),
                                )
                                Text(
                                    if (added) stringResource(R.string.ingredients_added) else unitLabel(choice.unit),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = Sizzle.colors.muted,
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun Hint(text: String) {
    Text(text, color = Sizzle.colors.muted, modifier = Modifier.padding(14.dp))
}

private fun flatten(ingredients: List<Ingredient>): List<IngredientChoice> = ingredients.flatMap { parent ->
    listOf(IngredientChoice(parent.id, parent.name, parent.unit, null)) +
        parent.children.map { IngredientChoice(it.id, it.name, it.unit, parent.name) }
}
