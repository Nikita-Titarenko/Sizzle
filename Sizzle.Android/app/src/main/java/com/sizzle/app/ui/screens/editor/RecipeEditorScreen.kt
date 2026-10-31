package com.sizzle.app.ui.screens.editor

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.AddPhotoAlternate
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.Remove
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.PlayCircle
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.MenuAnchorType
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import coil.compose.AsyncImage
import com.sizzle.app.R
import com.sizzle.app.data.MediaFiles
import com.sizzle.app.data.PickedMedia
import com.sizzle.app.data.api.message
import com.sizzle.app.data.model.IngredientUnit
import com.sizzle.app.ui.appViewModel
import com.sizzle.app.ui.components.AlertBox
import com.sizzle.app.ui.components.FieldShape
import com.sizzle.app.ui.components.IngredientPicker
import com.sizzle.app.ui.components.Loader
import com.sizzle.app.ui.components.MediaThumbnail
import com.sizzle.app.ui.components.PrimaryButton
import com.sizzle.app.ui.components.SectionCard
import com.sizzle.app.ui.components.sizzleFieldColors
import com.sizzle.app.ui.navigation.Navigator
import com.sizzle.app.ui.theme.Sizzle
import com.sizzle.app.ui.unitLabel

private const val MAX_STEP_FILES = 10

@Composable
fun RecipeEditorScreen(recipeId: String?, navigator: Navigator) {
    val vm = appViewModel(key = "editor-${recipeId ?: "new"}") { RecipeEditorViewModel(it, recipeId) }
    val context = LocalContext.current

    val pickCover = rememberLauncherForActivityResult(ActivityResultContracts.PickVisualMedia()) { uri ->
        if (uri != null) vm.accept(listOf(MediaFiles.describe(context, uri)), allowVideo = true).firstOrNull()?.let { vm.cover = it }
    }
    var stepForMedia by remember { mutableStateOf(-1) }
    val pickStepMedia = rememberLauncherForActivityResult(ActivityResultContracts.PickMultipleVisualMedia(MAX_STEP_FILES)) { uris ->
        val index = stepForMedia
        if (index >= 0 && uris.isNotEmpty()) {
            val accepted = vm.accept(uris.map { MediaFiles.describe(context, it) }, allowVideo = true)
            vm.updateStep(index) { it.copy(files = it.files + accepted) }
        }
    }
    val imageAndVideo = PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageAndVideo)

    Scaffold(
        containerColor = Sizzle.colors.background,
        topBar = {
            TopAppBar(
                title = { Text(stringResource(if (recipeId == null) R.string.editor_new_title else R.string.editor_edit_title)) },
                navigationIcon = {
                    IconButton(onClick = navigator::back) { Icon(Icons.Outlined.Close, stringResource(R.string.common_cancel)) }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = Sizzle.colors.background),
            )
        },
        bottomBar = { PublishBar(vm) { vm.submit(onSaved = navigator::showSavedRecipe) } },
    ) { padding ->
        if (vm.loading) {
            Box(Modifier.padding(padding)) { Loader() }
            return@Scaffold
        }
        Column(
            Modifier.fillMaxSize().padding(padding).imePadding().verticalScroll(rememberScrollState()).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            Text(stringResource(R.string.editor_intro), color = Sizzle.colors.muted)

            CoverPicker(vm, onPick = { pickCover.launch(imageAndVideo) })

            SectionCard {
                OutlinedTextField(
                    value = vm.name, onValueChange = { vm.name = it },
                    modifier = Modifier.fillMaxWidth(),
                    placeholder = { Text(stringResource(R.string.editor_name_placeholder)) },
                    textStyle = MaterialTheme.typography.titleLarge,
                    singleLine = true, shape = FieldShape, colors = sizzleFieldColors(),
                )
                OutlinedTextField(
                    value = vm.description, onValueChange = { vm.description = it },
                    modifier = Modifier.fillMaxWidth().height(130.dp),
                    placeholder = { Text(stringResource(R.string.editor_description_placeholder)) },
                    shape = FieldShape, colors = sizzleFieldColors(),
                )
                CategoryDropdown(vm)
                CookingTime(vm)
            }

            IngredientsSection(vm)

            SectionCard {
                Text(stringResource(R.string.editor_steps), style = MaterialTheme.typography.titleLarge)
                Text(stringResource(R.string.editor_steps_hint), color = Sizzle.colors.muted)
                if (recipeId != null) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Outlined.Info, null, tint = Sizzle.colors.accentText, modifier = Modifier.size(18.dp))
                        Text(stringResource(R.string.editor_steps_reupload), color = Sizzle.colors.accentText,
                            style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(start = 6.dp))
                    }
                }
                vm.steps.forEachIndexed { index, step ->
                    StepEditor(
                        index = index,
                        step = step,
                        canRemove = vm.steps.size > 1,
                        vm = vm,
                        onAddMedia = {
                            stepForMedia = index
                            pickStepMedia.launch(imageAndVideo)
                        },
                    )
                }
                AddRow(stringResource(R.string.editor_add_step), vm::addStep)
            }

            vm.mediaProblem?.let { AlertBox(it) }
            vm.failure?.let { AlertBox(it.message()) }
            Spacer(Modifier.height(8.dp))
        }
    }
}

@Composable
private fun PublishBar(vm: RecipeEditorViewModel, onPublish: () -> Unit) {
    Surface(color = Sizzle.colors.surface, border = BorderStroke(1.dp, Sizzle.colors.border)) {
        Row(
            Modifier.fillMaxWidth().navigationBarsPadding().padding(horizontal = 16.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            val missing = vm.missingHint
            if (missing != null) {
                Text(stringResource(missing), color = Sizzle.colors.muted, style = MaterialTheme.typography.bodySmall, modifier = Modifier.weight(1f))
            } else {
                Row(Modifier.weight(1f), verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Outlined.CheckCircle, null, tint = Sizzle.colors.success, modifier = Modifier.size(18.dp))
                    Text(stringResource(R.string.editor_ready), color = Sizzle.colors.success, fontWeight = FontWeight.SemiBold,
                        style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(start = 6.dp))
                }
            }
            PrimaryButton(
                text = stringResource(if (vm.recipeId == null) R.string.editor_publish else R.string.editor_save_changes),
                onClick = onPublish,
                enabled = missing == null,
                busy = vm.busy,
            )
        }
    }
}

@Composable
private fun CoverPicker(vm: RecipeEditorViewModel, onPick: () -> Unit) {
    val c = Sizzle.colors
    val shape = RoundedCornerShape(18.dp)
    val picked = vm.cover
    val current = vm.currentCoverUrl
    if (picked == null && current == null) {
        Column(
            Modifier.fillMaxWidth().aspectRatio(16f / 10f).clip(shape).background(c.surface)
                .border(1.dp, c.borderStrong, shape).clickable(onClick = onPick),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            Icon(Icons.Outlined.AddPhotoAlternate, null, tint = c.accent, modifier = Modifier.size(40.dp))
            Text(stringResource(R.string.media_add_cover), fontWeight = FontWeight.Bold, modifier = Modifier.padding(top = 8.dp))
            Text(stringResource(R.string.media_cover_hint), color = c.muted, style = MaterialTheme.typography.bodySmall)
            Text(stringResource(R.string.media_hint_media), color = c.muted, style = MaterialTheme.typography.bodySmall)
        }
        return
    }
    Box(Modifier.fillMaxWidth().aspectRatio(16f / 10f).clip(shape)) {
        if (picked != null) {
            PickedPreview(picked, Modifier.fillMaxSize())
        } else {
            MediaThumbnail(current, Modifier.fillMaxSize())
        }
        Row(Modifier.align(Alignment.BottomEnd).padding(10.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            OverlayButton(stringResource(R.string.media_replace), onPick)
            if (picked != null) OverlayButton(stringResource(R.string.media_remove)) { vm.cover = null }
        }
    }
}

@Composable
private fun OverlayButton(text: String, onClick: () -> Unit) {
    Text(
        text, color = Color.White, fontWeight = FontWeight.SemiBold,
        modifier = Modifier.clip(CircleShape).background(Color.Black.copy(alpha = 0.6f)).clickable(onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 8.dp),
    )
}

/** Local preview of a picked file; Coil decodes the first frame of videos. */
@Composable
private fun PickedPreview(media: PickedMedia, modifier: Modifier) {
    Box(modifier.background(Sizzle.colors.surface2)) {
        AsyncImage(model = media.uri, contentDescription = media.name, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
        if (media.isVideo) {
            Icon(Icons.Outlined.PlayCircle, null, tint = Color.White, modifier = Modifier.align(Alignment.Center).size(36.dp))
        }
    }
}

@Composable
private fun CategoryDropdown(vm: RecipeEditorViewModel) {
    var expanded by remember { mutableStateOf(false) }
    val selected = vm.categories.firstOrNull { it.id == vm.categoryId }
    ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
        OutlinedTextField(
            value = selected?.name.orEmpty(),
            onValueChange = {},
            readOnly = true,
            label = { Text(stringResource(R.string.editor_category)) },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.fillMaxWidth().menuAnchor(MenuAnchorType.PrimaryNotEditable),
            shape = FieldShape,
            colors = sizzleFieldColors(),
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }, containerColor = Sizzle.colors.surface) {
            vm.categories.forEach { category ->
                DropdownMenuItem(text = { Text(category.name) }, onClick = {
                    vm.categoryId = category.id
                    expanded = false
                })
            }
        }
    }
}

@Composable
private fun CookingTime(vm: RecipeEditorViewModel) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(stringResource(R.string.editor_cooking_time), fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
        IconButton(onClick = { vm.adjustTime(-5) }) { Icon(Icons.Outlined.Remove, stringResource(R.string.editor_less_time)) }
        OutlinedTextField(
            value = vm.cookingTime.toString(),
            onValueChange = { text -> vm.cookingTime = text.filter(Char::isDigit).take(4).toIntOrNull() ?: 0 },
            modifier = Modifier.width(76.dp),
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
            shape = FieldShape, colors = sizzleFieldColors(),
        )
        Text(stringResource(R.string.units_min_suffix), color = Sizzle.colors.muted, modifier = Modifier.padding(start = 6.dp))
        IconButton(onClick = { vm.adjustTime(5) }) { Icon(Icons.Outlined.Add, stringResource(R.string.editor_more_time)) }
    }
}

@Composable
private fun IngredientsSection(vm: RecipeEditorViewModel) {
    SectionCard {
        Text(stringResource(R.string.editor_ingredients), style = MaterialTheme.typography.titleLarge)
        Text(stringResource(R.string.editor_ingredients_hint), color = Sizzle.colors.muted)
        IngredientPicker(
            placeholder = stringResource(R.string.editor_ingredient_search),
            excludeIds = vm.ingredients.map { it.id }.toSet(),
            onPicked = vm::addIngredient,
        )
        vm.ingredients.forEachIndexed { index, ingredient ->
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    ingredient.parentName?.let { Text("$it ›", color = Sizzle.colors.muted, style = MaterialTheme.typography.bodySmall) }
                    Text(ingredient.name, fontWeight = FontWeight.SemiBold)
                }
                QuantityField(ingredient.quantity) { vm.updateIngredient(index, it) }
                Text(unitLabel(ingredient.unit), color = Sizzle.colors.muted, modifier = Modifier.padding(start = 6.dp).width(28.dp))
                IconButton(onClick = { vm.removeIngredient(index) }) {
                    Icon(Icons.Outlined.Close, stringResource(R.string.common_remove, ingredient.name), tint = Sizzle.colors.muted)
                }
            }
        }
        vm.newIngredients.forEachIndexed { index, draft -> NewIngredientEditor(index, draft, vm) }
        AddRow(stringResource(R.string.editor_add_custom_ingredient), vm::addNewIngredient)
    }
}

@Composable
private fun QuantityField(value: String, onChange: (String) -> Unit) {
    OutlinedTextField(
        value = value,
        onValueChange = { onChange(it.filter(Char::isDigit).take(6)) },
        modifier = Modifier.width(84.dp),
        singleLine = true,
        placeholder = { Text(stringResource(R.string.editor_quantity)) },
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
        shape = FieldShape,
        colors = sizzleFieldColors(),
    )
}

@Composable
private fun NewIngredientEditor(index: Int, draft: NewIngredientDraft, vm: RecipeEditorViewModel) {
    val c = Sizzle.colors
    Column(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).border(1.dp, c.border, RoundedCornerShape(14.dp)).padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(
                stringResource(R.string.editor_new_tag), color = c.accentText, fontWeight = FontWeight.Bold,
                style = MaterialTheme.typography.labelSmall,
                modifier = Modifier.clip(CircleShape).background(c.accentSoft).padding(horizontal = 8.dp, vertical = 3.dp),
            )
            Spacer(Modifier.weight(1f))
            IconButton(onClick = { vm.removeNewIngredient(index) }) { Icon(Icons.Outlined.Delete, null, tint = c.muted) }
        }
        OutlinedTextField(
            value = draft.name,
            onValueChange = { name -> vm.updateNewIngredient(index) { it.copy(name = name) } },
            modifier = Modifier.fillMaxWidth(),
            placeholder = { Text(stringResource(R.string.editor_new_name_placeholder)) },
            singleLine = true, shape = FieldShape, colors = sizzleFieldColors(),
        )
        Row(verticalAlignment = Alignment.CenterVertically) {
            QuantityField(draft.quantity) { quantity -> vm.updateNewIngredient(index) { it.copy(quantity = quantity) } }
            Spacer(Modifier.width(8.dp))
            IngredientUnit.entries.forEach { unit ->
                val selected = unit == draft.unit
                Text(
                    unitLabel(unit.short),
                    color = if (selected) c.onAccent else c.text,
                    fontWeight = FontWeight.SemiBold,
                    modifier = Modifier.padding(end = 6.dp).clip(CircleShape)
                        .background(if (selected) c.accent else c.surface2)
                        .clickable { vm.updateNewIngredient(index) { it.copy(unit = unit) } }
                        .padding(horizontal = 12.dp, vertical = 8.dp),
                )
            }
        }
        val parent = draft.parent
        if (parent != null) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(stringResource(R.string.editor_variant_of, parent.name), modifier = Modifier.weight(1f))
                IconButton(onClick = { vm.setParent(index, null) }) { Icon(Icons.Outlined.Close, null, tint = c.muted) }
            }
        } else {
            IngredientPicker(
                placeholder = stringResource(R.string.editor_parent_placeholder),
                onPicked = { vm.setParent(index, it) },
            )
        }
    }
}

@Composable
private fun StepEditor(index: Int, step: StepDraft, canRemove: Boolean, vm: RecipeEditorViewModel, onAddMedia: () -> Unit) {
    val c = Sizzle.colors
    Column(
        Modifier.fillMaxWidth().clip(RoundedCornerShape(14.dp)).border(1.dp, c.border, RoundedCornerShape(14.dp)).padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(stringResource(R.string.recipe_step_number, index + 1), fontWeight = FontWeight.Bold, color = c.accentText, modifier = Modifier.weight(1f))
            if (canRemove) {
                IconButton(onClick = { vm.removeStep(index) }) {
                    Icon(Icons.Outlined.Delete, stringResource(R.string.editor_remove_step), tint = c.muted)
                }
            }
        }
        OutlinedTextField(
            value = step.name,
            onValueChange = { name -> vm.updateStep(index) { it.copy(name = name) } },
            modifier = Modifier.fillMaxWidth(),
            placeholder = { Text(stringResource(R.string.editor_step_title_placeholder)) },
            singleLine = true, shape = FieldShape, colors = sizzleFieldColors(),
        )
        OutlinedTextField(
            value = step.description,
            onValueChange = { text -> vm.updateStep(index) { it.copy(description = text) } },
            modifier = Modifier.fillMaxWidth().height(110.dp),
            placeholder = { Text(stringResource(R.string.editor_step_description_placeholder)) },
            shape = FieldShape, colors = sizzleFieldColors(),
        )
        Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            step.files.forEachIndexed { fileIndex, media ->
                Box(Modifier.size(88.dp).clip(RoundedCornerShape(10.dp))) {
                    PickedPreview(media, Modifier.fillMaxSize())
                    Icon(
                        Icons.Outlined.Close, stringResource(R.string.media_remove), tint = Color.White,
                        modifier = Modifier.align(Alignment.TopEnd).padding(4.dp).size(22.dp).clip(CircleShape)
                            .background(Color.Black.copy(alpha = 0.6f))
                            .clickable { vm.updateStep(index) { it.copy(files = it.files.filterIndexed { i, _ -> i != fileIndex }) } }
                            .padding(3.dp),
                    )
                }
            }
            Column(
                Modifier.size(88.dp).clip(RoundedCornerShape(10.dp)).border(1.dp, c.borderStrong, RoundedCornerShape(10.dp))
                    .clickable(onClick = onAddMedia),
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center,
            ) {
                Icon(Icons.Outlined.AddPhotoAlternate, stringResource(R.string.media_add), tint = c.accent)
            }
        }
    }
}

@Composable
private fun AddRow(text: String, onClick: () -> Unit) {
    OutlinedButton(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        border = BorderStroke(1.dp, Sizzle.colors.borderStrong),
    ) {
        Icon(Icons.Outlined.Add, null, tint = Sizzle.colors.muted)
        Text(text, color = Sizzle.colors.muted, fontWeight = FontWeight.SemiBold, modifier = Modifier.padding(start = 6.dp))
    }
}

