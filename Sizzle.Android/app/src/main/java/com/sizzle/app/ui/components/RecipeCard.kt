package com.sizzle.app.ui.components

import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.grid.GridItemSpan
import androidx.compose.foundation.lazy.grid.LazyGridScope
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Favorite
import androidx.compose.material.icons.outlined.RemoveRedEye
import androidx.compose.material.icons.outlined.Schedule
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.sizzle.app.R
import com.sizzle.app.data.model.Recipe
import com.sizzle.app.ui.theme.Sizzle

@Composable
fun RecipeCard(recipe: Recipe, onOpen: () -> Unit, onAuthor: () -> Unit, modifier: Modifier = Modifier) {
    val c = Sizzle.colors
    val shape = RoundedCornerShape(16.dp)
    Column(
        modifier
            .clip(shape)
            .border(1.dp, c.border, shape)
            .clickable(onClick = onOpen),
    ) {
        MediaThumbnail(recipe.fileUrl, Modifier.fillMaxWidth().aspectRatio(3f / 4f), recipe.name)
        Column(Modifier.padding(10.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text(
                recipe.name,
                style = MaterialTheme.typography.titleSmall,
                fontWeight = FontWeight.Bold,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
            )
            Row(verticalAlignment = Alignment.CenterVertically) {
                MetaItem(Icons.Outlined.Schedule, stringResource(R.string.common_minutes, recipe.cookingTimeMinutes))
                Spacer(Modifier.width(10.dp))
                MetaItem(Icons.Outlined.RemoveRedEye, recipe.viewCount.toString())
                if (recipe.isLiked) {
                    Spacer(Modifier.width(10.dp))
                    Icon(Icons.Filled.Favorite, stringResource(R.string.feed_liked), tint = c.like, modifier = Modifier.size(14.dp))
                }
            }
            Row(
                Modifier.clip(RoundedCornerShape(8.dp)).clickable(onClick = onAuthor),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                UserAvatar(recipe.userName, recipe.userImageUrl, size = 20.dp)
                Text(
                    recipe.userName,
                    style = MaterialTheme.typography.bodySmall,
                    color = c.muted,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                    modifier = Modifier.padding(start = 6.dp),
                )
            }
        }
    }
}

@Composable
fun MetaItem(icon: androidx.compose.ui.graphics.vector.ImageVector, text: String) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, null, tint = Sizzle.colors.muted, modifier = Modifier.size(14.dp))
        Text(text, style = MaterialTheme.typography.bodySmall, color = Sizzle.colors.muted, modifier = Modifier.padding(start = 3.dp))
    }
}

/** Two-column recipe grid rows for a LazyVerticalGrid, with an empty state. */
fun LazyGridScope.recipeItems(
    recipes: List<Recipe>,
    emptyText: String,
    onOpen: (Recipe) -> Unit,
    onAuthor: (Recipe) -> Unit,
) {
    if (recipes.isEmpty()) {
        item(span = { GridItemSpan(maxLineSpan) }) {
            StatusView(emptyText, kind = StatusKind.Empty)
        }
    }
    items(recipes, key = { it.id }) { recipe ->
        RecipeCard(recipe, onOpen = { onOpen(recipe) }, onAuthor = { onAuthor(recipe) })
    }
}
