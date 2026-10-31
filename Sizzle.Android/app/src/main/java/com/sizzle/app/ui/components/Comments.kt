package com.sizzle.app.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sizzle.app.AppContainer
import com.sizzle.app.R
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.failure
import com.sizzle.app.data.api.message
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.Comment
import com.sizzle.app.data.model.CreateCommentRequest
import com.sizzle.app.data.model.UpdateCommentRequest
import com.sizzle.app.ui.formatDateTime
import com.sizzle.app.ui.theme.Sizzle
import kotlinx.coroutines.launch

private const val MAX_COMMENT_LENGTH = 2000
private const val PAGE_SIZE = 20

/** Comments of one recipe: paged loading, posting, replies, editing and deleting. */
class CommentsViewModel(private val container: AppContainer, private val recipeId: String) : ViewModel() {
    var comments by mutableStateOf<List<Comment>>(emptyList())
        private set
    var total by mutableIntStateOf(0)
        private set
    var loading by mutableStateOf(false)
        private set
    var posting by mutableStateOf(false)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set

    private var nextPage = 0

    val roots: List<Comment> get() = comments.filter { it.parentId == null }
    fun repliesOf(id: String): List<Comment> = comments.filter { it.parentId == id }

    init {
        loadMore()
    }

    fun loadMore() {
        if (loading) return
        loading = true
        viewModelScope.launch {
            val result = runApi { container.api.getComments(recipeId, nextPage, PAGE_SIZE) }
            result.onSuccess { page ->
                nextPage++
                total = page.totalSize
                val known = comments.map { it.id }.toSet()
                comments = comments + page.data.filter { it.id !in known }
            }
            failure = result.failure
            loading = false
        }
    }

    fun post(text: String, parentId: String?, onDone: () -> Unit) {
        posting = true
        viewModelScope.launch {
            val result = runApi { container.api.createComment(recipeId, CreateCommentRequest(text.trim(), parentId)) }
            result.onSuccess { comment ->
                comments = if (parentId == null) listOf(comment) + comments else comments + comment
                total++
                onDone()
            }
            failure = result.failure
            posting = false
        }
    }

    fun edit(comment: Comment, text: String, onDone: () -> Unit) {
        viewModelScope.launch {
            val result = runApi { container.api.updateComment(comment.id, UpdateCommentRequest(text.trim())) }
            result.onSuccess { updated ->
                comments = comments.map { if (it.id == updated.id) updated else it }
                onDone()
            }
            failure = result.failure
        }
    }

    fun delete(comment: Comment) {
        viewModelScope.launch {
            val result = runApi { container.api.deleteComment(comment.id) }
            result.onSuccess {
                // Replies of a deleted comment lose their parent on the server (SetNull) and become top-level.
                comments = comments.filter { it.id != comment.id }
                    .map { if (it.parentId == comment.id) it.copy(parentId = null) else it }
                total--
            }
            failure = result.failure
        }
    }
}

/** The comment list as LazyColumn items: composer, comments with their replies, "load more". */
fun LazyListScope.commentItems(
    vm: CommentsViewModel,
    currentUserId: String?,
    onLogin: () -> Unit,
    onUser: (String) -> Unit,
    showTitle: Boolean = true,
) {
    if (showTitle) {
        item(key = "comments-title") {
            Text(
                stringResource(R.string.comments_title, vm.total),
                style = MaterialTheme.typography.titleLarge,
                modifier = Modifier.padding(top = 8.dp, bottom = 4.dp),
            )
        }
    }
    item(key = "comments-composer") {
        if (currentUserId != null) {
            CommentComposer(stringResource(R.string.comments_placeholder), vm.posting) { text, done -> vm.post(text, null, done) }
        } else {
            Text(
                stringResource(R.string.comments_log_in),
                color = Sizzle.colors.accentText,
                fontWeight = FontWeight.SemiBold,
                modifier = Modifier.clip(RoundedCornerShape(8.dp)).clickable(onClick = onLogin).padding(vertical = 8.dp),
            )
        }
        vm.failure?.let { AlertBox(it.message(), Modifier.padding(top = 8.dp)) }
    }
    if (vm.comments.isEmpty() && !vm.loading) {
        item(key = "comments-empty") {
            Text(stringResource(R.string.comments_empty), color = Sizzle.colors.muted, modifier = Modifier.padding(vertical = 12.dp))
        }
    }
    items(vm.roots, key = { it.id }) { comment ->
        Column {
            CommentItem(
                comment = comment,
                canReply = currentUserId != null,
                canModify = comment.userId == currentUserId,
                vm = vm,
                onUser = onUser,
            )
            vm.repliesOf(comment.id).forEach { reply ->
                CommentItem(
                    comment = reply,
                    canReply = false,
                    canModify = reply.userId == currentUserId,
                    vm = vm,
                    onUser = onUser,
                    modifier = Modifier.padding(start = 40.dp),
                )
            }
        }
    }
    if (vm.loading) {
        item(key = "comments-loading") {
            CircularProgressIndicator(Modifier.padding(16.dp).size(24.dp), color = Sizzle.colors.accent, strokeWidth = 2.dp)
        }
    } else if (vm.comments.size < vm.total) {
        item(key = "comments-more") {
            SecondaryButton(stringResource(R.string.comments_load_more), vm::loadMore, Modifier.padding(vertical = 8.dp))
        }
    }
}

@Composable
private fun CommentComposer(
    placeholder: String,
    busy: Boolean,
    modifier: Modifier = Modifier,
    initial: String = "",
    onSend: (String, () -> Unit) -> Unit,
) {
    var text by rememberSaveable { mutableStateOf(initial) }
    Row(modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        OutlinedTextField(
            value = text,
            onValueChange = { if (it.length <= MAX_COMMENT_LENGTH) text = it },
            modifier = Modifier.weight(1f),
            placeholder = { Text(placeholder) },
            shape = FieldShape,
            colors = sizzleFieldColors(),
            maxLines = 5,
        )
        IconButton(
            onClick = { onSend(text) { text = "" } },
            enabled = text.isNotBlank() && !busy,
        ) {
            Icon(Icons.AutoMirrored.Filled.Send, stringResource(R.string.comments_post), tint = if (text.isNotBlank()) Sizzle.colors.accent else Sizzle.colors.muted)
        }
    }
}

private enum class CommentMode { View, Reply, Edit }

@Composable
private fun CommentItem(
    comment: Comment,
    canReply: Boolean,
    canModify: Boolean,
    vm: CommentsViewModel,
    onUser: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    var mode by rememberSaveable(comment.id) { mutableStateOf(CommentMode.View) }
    var confirmDelete by rememberSaveable(comment.id) { mutableStateOf(false) }
    val c = Sizzle.colors

    Column(modifier.fillMaxWidth().padding(vertical = 10.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Row(
            Modifier.clip(RoundedCornerShape(8.dp)).clickable { onUser(comment.userId) },
            verticalAlignment = Alignment.CenterVertically,
        ) {
            UserAvatar(comment.userName, comment.userImageUrl, size = 28.dp)
            Text(comment.userName, fontWeight = FontWeight.Bold, modifier = Modifier.padding(start = 8.dp))
            Text(formatDateTime(comment.createdAt), color = c.muted, style = MaterialTheme.typography.bodySmall, modifier = Modifier.padding(start = 8.dp))
        }
        if (mode == CommentMode.Edit) {
            CommentComposer(stringResource(R.string.comments_placeholder), busy = false, initial = comment.content) { text, _ ->
                vm.edit(comment, text) { mode = CommentMode.View }
            }
        } else {
            Text(comment.content, style = MaterialTheme.typography.bodyMedium, modifier = Modifier.padding(start = 36.dp))
        }
        Row(Modifier.padding(start = 28.dp)) {
            if (canReply) {
                TextButton(onClick = { mode = if (mode == CommentMode.Reply) CommentMode.View else CommentMode.Reply }) {
                    Text(stringResource(R.string.common_reply), color = c.accentText)
                }
            }
            if (canModify) {
                TextButton(onClick = { mode = if (mode == CommentMode.Edit) CommentMode.View else CommentMode.Edit }) {
                    Text(stringResource(if (mode == CommentMode.Edit) R.string.common_cancel else R.string.common_edit), color = c.accentText)
                }
                TextButton(onClick = { confirmDelete = true }) {
                    Text(stringResource(R.string.common_delete), color = c.danger)
                }
            }
        }
        if (mode == CommentMode.Reply) {
            CommentComposer(
                stringResource(R.string.comments_reply_placeholder),
                vm.posting,
                Modifier.padding(start = 36.dp),
            ) { text, done ->
                vm.post(text, comment.id) {
                    done()
                    mode = CommentMode.View
                }
            }
        }
    }
    if (confirmDelete) {
        ConfirmDialog(
            text = stringResource(R.string.comments_confirm_delete),
            confirm = stringResource(R.string.common_delete),
            onConfirm = { vm.delete(comment) },
            onDismiss = { confirmDelete = false },
        )
    }
}
