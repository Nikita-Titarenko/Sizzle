package com.sizzle.app.ui.screens.editor

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.sizzle.app.R
import com.sizzle.app.data.api.message
import com.sizzle.app.data.model.Recipe
import com.sizzle.app.ui.components.AlertBox
import com.sizzle.app.ui.components.ConfirmDialog
import com.sizzle.app.ui.components.MediaThumbnail
import com.sizzle.app.ui.formatDate
import com.sizzle.app.ui.theme.Sizzle

/** First screen of "New recipe" when the user has drafts: continue one or start fresh. */
@Composable
fun DraftChooser(vm: RecipeEditorViewModel, modifier: Modifier, onOpen: (Recipe) -> Unit) {
    val c = Sizzle.colors
    var deleting by remember { mutableStateOf<Recipe?>(null) }
    LazyColumn(
        modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        item { Text(stringResource(R.string.drafts_intro), color = c.muted) }
        item {
            val shape = RoundedCornerShape(16.dp)
            Row(
                Modifier.fillMaxWidth().clip(shape).border(1.dp, c.borderStrong, shape).clickable(onClick = vm::startFresh).padding(16.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Box(Modifier.size(48.dp).clip(CircleShape).background(c.accent), contentAlignment = Alignment.Center) {
                    Icon(Icons.Outlined.Add, null, tint = c.onAccent)
                }
                Column(Modifier.padding(start = 14.dp)) {
                    Text(stringResource(R.string.drafts_start_fresh), fontWeight = FontWeight.Bold)
                    Text(stringResource(R.string.drafts_start_fresh_hint), color = c.muted, style = MaterialTheme.typography.bodySmall)
                }
            }
        }
        items(vm.drafts, key = { it.id }) { draft ->
            val shape = RoundedCornerShape(16.dp)
            Row(
                Modifier.fillMaxWidth().clip(shape).background(c.surface).border(1.dp, c.border, shape)
                    .clickable { onOpen(draft) }.padding(10.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Box(Modifier.size(72.dp).clip(RoundedCornerShape(12.dp)).background(c.surface2), contentAlignment = Alignment.Center) {
                    if (draft.fileUrl != null) {
                        MediaThumbnail(draft.fileUrl, Modifier.fillMaxSize())
                    } else {
                        Icon(Icons.Outlined.Description, null, tint = c.muted)
                    }
                }
                Column(Modifier.weight(1f).padding(horizontal = 12.dp)) {
                    Text(
                        stringResource(R.string.drafts_tag).uppercase(), color = c.muted, fontWeight = FontWeight.Bold,
                        style = MaterialTheme.typography.labelSmall,
                    )
                    Text(draft.name, fontWeight = FontWeight.SemiBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    Text(stringResource(R.string.drafts_created, formatDate(draft.creationDate)), color = c.muted,
                        style = MaterialTheme.typography.bodySmall)
                }
                IconButton(onClick = { deleting = draft }) {
                    Icon(Icons.Outlined.Delete, stringResource(R.string.drafts_delete, draft.name), tint = c.muted)
                }
            }
        }
        vm.failure?.let { failure -> item { AlertBox(failure.message()) } }
    }
    deleting?.let { draft ->
        ConfirmDialog(
            text = stringResource(R.string.drafts_confirm_delete, draft.name),
            confirm = stringResource(R.string.common_delete),
            onConfirm = { vm.deleteDraft(draft) },
            onDismiss = { deleting = null },
        )
    }
}
