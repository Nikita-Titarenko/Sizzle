package com.sizzle.app.ui.components

import android.view.ViewGroup
import androidx.annotation.OptIn
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.outlined.Restaurant
import androidx.compose.material3.Icon
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.media3.common.MediaItem
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.ui.AspectRatioFrameLayout
import androidx.media3.ui.PlayerView
import coil.compose.AsyncImage
import com.sizzle.app.data.MediaFiles
import com.sizzle.app.data.api.ApiClient
import com.sizzle.app.ui.theme.Sizzle

/**
 * ExoPlayer for one video. Pauses while the app is in the background and releases the player when it
 * leaves the composition. [playing] and [muted] can change without recreating the player.
 */
@OptIn(UnstableApi::class)
@Composable
fun VideoPlayer(
    url: String,
    modifier: Modifier = Modifier,
    playing: Boolean = true,
    muted: Boolean = false,
    loop: Boolean = true,
    showControls: Boolean = false,
    fill: Boolean = false,
) {
    val context = LocalContext.current
    val player = remember(url) {
        ExoPlayer.Builder(context).build().apply {
            setMediaItem(MediaItem.fromUri(ApiClient.mediaUrl(url)!!))
            repeatMode = if (loop) Player.REPEAT_MODE_ONE else Player.REPEAT_MODE_OFF
            prepare()
        }
    }
    val currentPlaying = rememberUpdatedState(playing)
    LaunchedEffect(player, playing) { player.playWhenReady = playing }
    LaunchedEffect(player, muted) { player.volume = if (muted) 0f else 1f }

    val lifecycle = LocalLifecycleOwner.current.lifecycle
    DisposableEffect(player, lifecycle) {
        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_PAUSE -> player.pause()
                Lifecycle.Event.ON_RESUME -> player.playWhenReady = currentPlaying.value
                else -> Unit
            }
        }
        lifecycle.addObserver(observer)
        onDispose {
            lifecycle.removeObserver(observer)
            player.release()
        }
    }

    AndroidView(
        factory = {
            PlayerView(it).apply {
                layoutParams = ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT)
                setShutterBackgroundColor(android.graphics.Color.TRANSPARENT)
            }
        },
        update = {
            it.player = player
            it.useController = showControls
            it.resizeMode = if (fill) AspectRatioFrameLayout.RESIZE_MODE_ZOOM else AspectRatioFrameLayout.RESIZE_MODE_FIT
        },
        modifier = modifier,
    )
}

/**
 * Still preview of a recipe photo or video (the video's first frame), or a placeholder when there
 * is no media. Videos get a small play badge.
 */
@Composable
fun MediaThumbnail(url: String?, modifier: Modifier = Modifier, contentDescription: String? = null) {
    val c = Sizzle.colors
    Box(modifier.background(c.surface2), contentAlignment = Alignment.Center) {
        if (url == null) {
            Icon(Icons.Outlined.Restaurant, null, tint = c.muted, modifier = Modifier.size(36.dp))
        } else {
            AsyncImage(
                model = ApiClient.mediaUrl(url),
                contentDescription = contentDescription,
                contentScale = ContentScale.Crop,
                modifier = Modifier.fillMaxSize(),
            )
            if (MediaFiles.isVideoUrl(url)) {
                Icon(
                    Icons.Filled.PlayArrow, null, tint = Color.White,
                    modifier = Modifier.align(Alignment.TopEnd).padding(8.dp).size(26.dp)
                        .clip(CircleShape).background(Color.Black.copy(alpha = 0.45f)).padding(3.dp),
                )
            }
        }
    }
}

/** Full media: plays videos with controls, shows photos. Used on the recipe page. */
@Composable
fun MediaView(url: String, modifier: Modifier = Modifier, contentDescription: String? = null) {
    if (MediaFiles.isVideoUrl(url)) {
        VideoPlayer(url, modifier.background(Color.Black), playing = false, showControls = true, loop = false)
    } else {
        AsyncImage(
            model = ApiClient.mediaUrl(url),
            contentDescription = contentDescription,
            contentScale = ContentScale.Crop,
            modifier = modifier.background(Sizzle.colors.surface2),
        )
    }
}
