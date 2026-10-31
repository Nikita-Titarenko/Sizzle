package com.sizzle.app.data.api

import android.content.Context
import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext
import com.sizzle.app.R
import com.sizzle.app.data.model.ApiError
import kotlinx.coroutines.CancellationException
import kotlinx.serialization.builtins.ListSerializer
import retrofit2.HttpException
import java.io.IOException

/**
 * A failed API call, with the server's messages already read from the response body.
 * The text is produced at display time ([message]) so it follows the current app language.
 */
class ApiFailure(
    val unreachable: Boolean = false,
    val code: Int = 0,
    val serverMessages: List<String> = emptyList(),
    cause: Throwable? = null,
) : Exception(cause) {

    fun message(context: Context): String = when {
        unreachable -> context.getString(R.string.errors_unreachable)
        serverMessages.isNotEmpty() -> serverMessages.joinToString(". ")
        code > 0 -> context.getString(R.string.errors_request_failed, code)
        else -> context.getString(R.string.errors_generic)
    }
}

@Composable
fun ApiFailure.message(): String = message(LocalContext.current)

/** Runs an API call; failures come back as [ApiFailure], cancellation still propagates. */
suspend fun <T> runApi(block: suspend () -> T): Result<T> = try {
    Result.success(block())
} catch (e: CancellationException) {
    throw e
} catch (e: Exception) {
    Result.failure(e.toApiFailure())
}

fun Throwable.toApiFailure(): ApiFailure = when (this) {
    is ApiFailure -> this
    is IOException -> ApiFailure(unreachable = true, cause = this)
    is HttpException -> {
        val body = runCatching { response()?.errorBody()?.string() }.getOrNull().orEmpty()
        val messages = runCatching { ApiClient.json.decodeFromString(ListSerializer(ApiError.serializer()), body) }
            .getOrNull()
            .orEmpty()
            .map { it.message }
            .filter { it.isNotBlank() }
        ApiFailure(unreachable = code() in listOf(502, 503, 504), code = code(), serverMessages = messages, cause = this)
    }
    else -> ApiFailure(cause = this)
}

val Result<*>.failure: ApiFailure? get() = exceptionOrNull() as ApiFailure?
