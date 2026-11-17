package com.sizzle.app.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.sizzle.app.R

/**
 * Marks a cover the AI drew. Place it over the image; [label] null shows only the icon,
 * for small thumbnails.
 */
@Composable
fun AiImageBadge(modifier: Modifier = Modifier, label: String? = stringResource(R.string.recipe_ai_image_short)) {
    Row(
        modifier.clip(CircleShape).background(Color.Black.copy(alpha = 0.6f))
            .padding(horizontal = if (label == null) 5.dp else 8.dp, vertical = if (label == null) 5.dp else 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            Icons.Outlined.AutoAwesome,
            if (label == null) stringResource(R.string.recipe_ai_image) else null,
            tint = Color.White,
            modifier = Modifier.size(if (label == null) 12.dp else 14.dp),
        )
        if (label != null) {
            Text(
                label,
                color = Color.White,
                fontWeight = FontWeight.SemiBold,
                style = MaterialTheme.typography.labelMedium,
                modifier = Modifier.padding(start = 5.dp),
            )
        }
    }
}
