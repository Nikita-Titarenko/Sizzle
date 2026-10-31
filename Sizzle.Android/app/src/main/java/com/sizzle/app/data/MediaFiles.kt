package com.sizzle.app.data

import android.content.Context
import android.net.Uri
import android.provider.OpenableColumns
import android.webkit.MimeTypeMap
import com.sizzle.app.R
import okhttp3.MediaType
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.MultipartBody
import okhttp3.RequestBody
import okio.BufferedSink
import okio.source

/** A photo or video the user picked, described well enough to validate and upload it. */
data class PickedMedia(
    val uri: Uri,
    val name: String,
    val size: Long,
    val mimeType: String?,
) {
    val isVideo: Boolean get() = MediaFiles.extensionOf(name) in MediaFiles.VIDEO_EXTENSIONS
}

/** Mirrors the API's FileService rules so users get feedback before uploading. */
object MediaFiles {
    val IMAGE_EXTENSIONS = setOf("jpg", "jpeg", "png", "webp", "gif")
    val VIDEO_EXTENSIONS = setOf("mp4", "webm", "mov", "avi", "mkv")
    private const val MAX_IMAGE_BYTES = 5L * 1024 * 1024
    private const val MAX_VIDEO_BYTES = 100L * 1024 * 1024

    private val VIDEO_URL = Regex("\\.(${VIDEO_EXTENSIONS.joinToString("|")})(\\?.*)?$", RegexOption.IGNORE_CASE)

    /** The API serves images and videos from the same fields; the file extension tells them apart. */
    fun isVideoUrl(url: String?): Boolean = url != null && VIDEO_URL.containsMatchIn(url)

    fun extensionOf(name: String): String = name.substringAfterLast('.', "").lowercase()

    /** Reads the file name and size. The API decides the file type by extension, so one is added
     * from the MIME type when the provider's display name has none (some galleries omit it). */
    fun describe(context: Context, uri: Uri): PickedMedia {
        var name: String? = null
        var size = -1L
        context.contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE), null, null, null)
            ?.use { cursor ->
                if (cursor.moveToFirst()) {
                    name = cursor.getString(0)
                    if (!cursor.isNull(1)) size = cursor.getLong(1)
                }
            }
        val mimeType = context.contentResolver.getType(uri)
        var fileName = name ?: uri.lastPathSegment ?: "upload"
        val known = IMAGE_EXTENSIONS + VIDEO_EXTENSIONS
        if (extensionOf(fileName) !in known) {
            MimeTypeMap.getSingleton().getExtensionFromMimeType(mimeType)?.let { fileName = "$fileName.$it" }
        }
        return PickedMedia(uri, fileName, size, mimeType)
    }

    /** Why the API would reject the file, as a message, or null when it is acceptable. */
    fun problem(context: Context, media: PickedMedia, allowVideo: Boolean): String? {
        val extension = extensionOf(media.name)
        return when {
            extension in IMAGE_EXTENSIONS ->
                if (media.size <= MAX_IMAGE_BYTES) null else context.getString(R.string.media_image_too_large, media.name)
            allowVideo && extension in VIDEO_EXTENSIONS ->
                if (media.size <= MAX_VIDEO_BYTES) null else context.getString(R.string.media_video_too_large, media.name)
            allowVideo -> context.getString(R.string.media_unsupported_media, media.name)
            else -> context.getString(R.string.media_unsupported_image, media.name)
        }
    }

    /** A multipart file field that streams from the content provider instead of loading the whole file. */
    fun part(context: Context, field: String, media: PickedMedia): MultipartBody.Part {
        val body = object : RequestBody() {
            override fun contentType(): MediaType? = media.mimeType?.toMediaTypeOrNull()
            override fun contentLength(): Long = media.size
            override fun writeTo(sink: BufferedSink) {
                val stream = context.contentResolver.openInputStream(media.uri)
                    ?: throw java.io.IOException("Cannot open ${media.name}")
                stream.source().use { sink.writeAll(it) }
            }
        }
        return MultipartBody.Part.createFormData(field, media.name, body)
    }

    fun field(name: String, value: String): MultipartBody.Part = MultipartBody.Part.createFormData(name, value)
}
