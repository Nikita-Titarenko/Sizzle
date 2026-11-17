package com.sizzle.app.data.api

import com.sizzle.app.data.MediaFiles
import com.sizzle.app.data.PickedMedia
import com.sizzle.app.data.model.GenerateRecipeRequest
import com.sizzle.app.data.model.GeneratedRecipe
import com.sizzle.app.data.model.GenerationEvent
import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOn
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.concurrent.TimeUnit

/**
 * The AI generation endpoints answer with server-sent events (`event: chunk` + `data: {json}`),
 * which Retrofit cannot stream, so they are read line by line with OkHttp.
 * Failures are thrown as [ApiFailure]; cancelling the collector cancels the request.
 */
class AiStream(client: OkHttpClient) {
    // A recipe takes a while to write and a video longer to watch; chunks can be far apart.
    private val client = client.newBuilder().readTimeout(5, TimeUnit.MINUTES).build()

    /** Writes a new recipe from [message], or revises [recipe] when given. Uses the app language. */
    fun generate(message: String, recipe: GeneratedRecipe?): Flow<GenerationEvent> {
        val json = ApiClient.json.encodeToString(GenerateRecipeRequest.serializer(), GenerateRecipeRequest(message, recipe))
        return stream("api/recipes/generate", json.toRequestBody("application/json".toMediaType()))
    }

    /** Reads a recipe out of a cooking video. Needs an active subscription (403 otherwise). */
    fun generateFromVideo(context: Context, video: PickedMedia): Flow<GenerationEvent> {
        // The API rejects the request when it carries anything besides the one video.
        val body = MultipartBody.Builder().setType(MultipartBody.FORM)
            .addPart(MediaFiles.part(context, "video", video))
            .build()
        return stream("api/recipes/generate-from-video", body)
    }

    private fun stream(path: String, body: RequestBody): Flow<GenerationEvent> = flow {
        val request = Request.Builder()
            .url(ApiClient.baseUrl.resolve(path)!!)
            .header("Accept", "text/event-stream")
            .post(body)
            .build()
        val call = client.newCall(request)
        val response = try {
            call.execute()
        } catch (e: java.io.IOException) {
            throw ApiFailure(unreachable = true, cause = e)
        }
        response.use {
            if (!response.isSuccessful) {
                throw httpFailure(response.code, response.body?.string().orEmpty())
            }
            val source = response.body?.source() ?: return@use
            val data = StringBuilder()
            try {
                while (true) {
                    currentCoroutineContext().ensureActive()
                    val line = source.readUtf8Line() ?: break
                    when {
                        line.isEmpty() -> {
                            parse(data)?.let { event -> emit(event) }
                            data.clear()
                        }
                        line.startsWith("data:") -> {
                            if (data.isNotEmpty()) data.append('\n')
                            data.append(line.removePrefix("data:").trimStart())
                        }
                    }
                }
                parse(data)?.let { event -> emit(event) }
            } catch (e: java.io.IOException) {
                throw ApiFailure(unreachable = true, cause = e)
            } finally {
                call.cancel()
            }
        }
    }.flowOn(Dispatchers.IO)

    private fun parse(data: CharSequence): GenerationEvent? =
        if (data.isBlank()) null
        else runCatching { ApiClient.json.decodeFromString(GenerationEvent.serializer(), data.toString()) }
            .getOrNull()
            ?.takeIf { it.type.isNotEmpty() }
}
