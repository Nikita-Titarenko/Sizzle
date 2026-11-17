package com.sizzle.app.ui.screens.editor

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ArrowUpward
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.ExpandLess
import androidx.compose.material.icons.outlined.ExpandMore
import androidx.compose.material.icons.outlined.Movie
import androidx.compose.material.icons.outlined.Stop
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.IconButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import com.sizzle.app.R
import com.sizzle.app.ui.components.FieldShape
import com.sizzle.app.ui.components.sizzleFieldColors
import com.sizzle.app.ui.theme.Sizzle

private val SUGGESTIONS = listOf(R.string.ai_suggestion_fridge, R.string.ai_suggestion_quick, R.string.ai_suggestion_notes)
private val REVISIONS = listOf(R.string.ai_revision_vegetarian, R.string.ai_revision_double, R.string.ai_revision_simpler)

/**
 * Chat with the recipe AI above the form, so both stay in view. Collapses to its header;
 * every answer is written straight into the form below.
 */
@Composable
fun AiAssistantCard(vm: RecipeEditorViewModel, onPickVideo: () -> Unit) {
    val c = Sizzle.colors
    val shape = RoundedCornerShape(18.dp)
    var expanded by rememberSaveable { mutableStateOf(true) }
    var text by rememberSaveable { mutableStateOf("") }
    val scroll = rememberScrollState()
    LaunchedEffect(vm.messages.size, vm.preview) { scroll.animateScrollTo(scroll.maxValue) }

    Column(
        Modifier.fillMaxWidth().clip(shape).background(c.surface).border(1.dp, c.accent.copy(alpha = 0.5f), shape),
    ) {
        Row(
            Modifier.fillMaxWidth().clickable { expanded = !expanded }.padding(horizontal = 14.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Box(Modifier.size(34.dp).clip(RoundedCornerShape(10.dp)).background(c.accentSoft), contentAlignment = Alignment.Center) {
                Icon(Icons.Outlined.AutoAwesome, null, tint = c.accentText, modifier = Modifier.size(20.dp))
            }
            Column(Modifier.weight(1f).padding(start = 10.dp)) {
                Text(stringResource(R.string.ai_title), fontWeight = FontWeight.Bold)
                Text(
                    if (vm.generating) stringResource(R.string.ai_thinking) else stringResource(R.string.ai_subtitle),
                    color = c.muted, style = MaterialTheme.typography.bodySmall,
                )
            }
            Icon(if (expanded) Icons.Outlined.ExpandLess else Icons.Outlined.ExpandMore, null, tint = c.muted)
        }

        AnimatedVisibility(expanded) {
            Column(Modifier.padding(start = 14.dp, end = 14.dp, bottom = 14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Column(
                    Modifier.fillMaxWidth().heightIn(max = 320.dp).verticalScroll(scroll),
                    verticalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    if (vm.messages.isEmpty() && !vm.generating) Intro(vm) { vm.send(it) }
                    vm.messages.forEach { Bubble(it) }
                    vm.preview?.let { StreamingBubble(it) }
                }

                OutlinedTextField(
                    value = text,
                    onValueChange = { text = it.take(4000) },
                    modifier = Modifier.fillMaxWidth(),
                    enabled = !vm.generating,
                    placeholder = { Text(stringResource(R.string.ai_placeholder)) },
                    maxLines = 4,
                    shape = FieldShape,
                    colors = sizzleFieldColors(),
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Send),
                    keyboardActions = KeyboardActions(onSend = { vm.send(text); text = "" }),
                    trailingIcon = {
                        if (vm.generating) {
                            IconButton(onClick = vm::stop) { Icon(Icons.Outlined.Stop, stringResource(R.string.ai_stop), tint = c.text) }
                        } else {
                            IconButton(
                                onClick = { vm.send(text); text = "" },
                                enabled = text.isNotBlank(),
                                colors = IconButtonDefaults.iconButtonColors(containerColor = c.accent, contentColor = c.onAccent,
                                    disabledContainerColor = c.surface3, disabledContentColor = c.muted),
                            ) { Icon(Icons.Outlined.ArrowUpward, stringResource(R.string.ai_send)) }
                        }
                    },
                )
                OutlinedButton(
                    onClick = onPickVideo,
                    enabled = !vm.generating,
                    shape = CircleShape,
                    border = BorderStroke(1.dp, c.border),
                ) {
                    Icon(Icons.Outlined.Movie, null, tint = c.text, modifier = Modifier.size(18.dp))
                    Text(stringResource(R.string.ai_from_video), color = c.text, modifier = Modifier.padding(start = 6.dp, end = 8.dp))
                    PremiumTag(locked = vm.subscribed == false)
                }
            }
        }
    }
}

@Composable
private fun Intro(vm: RecipeEditorViewModel, onSuggestion: (String) -> Unit) {
    val c = Sizzle.colors
    val editing = vm.name.isNotBlank() || vm.ingredients.isNotEmpty()
    Text(
        stringResource(if (editing) R.string.ai_intro_edit else R.string.ai_intro_new),
        color = c.muted, style = MaterialTheme.typography.bodyMedium,
    )
    (if (editing) REVISIONS else SUGGESTIONS).forEach { id ->
        val suggestion = stringResource(id)
        Text(
            suggestion,
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(10.dp))
                .border(1.dp, c.borderStrong, RoundedCornerShape(10.dp))
                .clickable { onSuggestion(suggestion) }
                .padding(horizontal = 12.dp, vertical = 9.dp),
        )
    }
}

@Composable
private fun Bubble(message: ChatMessage) {
    val c = Sizzle.colors
    val (background, color) = when (message.role) {
        ChatRole.User -> c.accent to c.onAccent
        ChatRole.Assistant -> c.surface2 to c.text
        ChatRole.Error -> c.dangerSoft to c.danger
    }
    Row(Modifier.fillMaxWidth(), horizontalArrangement = if (message.role == ChatRole.User) Arrangement.End else Arrangement.Start) {
        Column(
            Modifier.widthIn(max = 300.dp).clip(RoundedCornerShape(14.dp)).background(background).padding(horizontal = 12.dp, vertical = 9.dp),
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                if (message.video) Icon(Icons.Outlined.Movie, null, tint = color, modifier = Modifier.size(16.dp).padding(end = 4.dp))
                Text(message.text, color = color, style = MaterialTheme.typography.bodyMedium)
            }
            if (message.applied) {
                Row(Modifier.padding(top = 4.dp), verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Outlined.CheckCircle, null, tint = c.success, modifier = Modifier.size(14.dp))
                    Text(stringResource(R.string.ai_applied), color = c.success, fontWeight = FontWeight.SemiBold,
                        style = MaterialTheme.typography.labelSmall, modifier = Modifier.padding(start = 4.dp))
                }
            }
        }
    }
}

@Composable
private fun StreamingBubble(preview: StreamPreview) {
    val c = Sizzle.colors
    Column(
        Modifier.widthIn(max = 300.dp).clip(RoundedCornerShape(14.dp)).background(c.surface2).padding(horizontal = 12.dp, vertical = 9.dp),
    ) {
        Text(
            when {
                preview.message.isNotEmpty() -> preview.message
                preview.name.isNotEmpty() -> stringResource(R.string.ai_writing, preview.name)
                else -> stringResource(R.string.ai_thinking)
            },
            color = if (preview.message.isEmpty() && preview.name.isEmpty()) c.muted else c.text,
            style = MaterialTheme.typography.bodyMedium,
        )
        if (preview.ingredients > 0 || preview.steps > 0) {
            Spacer(Modifier.size(2.dp))
            Text(stringResource(R.string.ai_progress, preview.ingredients, preview.steps), color = c.muted,
                style = MaterialTheme.typography.labelSmall)
        }
        androidx.compose.material3.LinearProgressIndicator(
            Modifier.fillMaxWidth().padding(top = 6.dp).clip(CircleShape),
            color = c.accent, trackColor = c.surface3,
        )
    }
}
