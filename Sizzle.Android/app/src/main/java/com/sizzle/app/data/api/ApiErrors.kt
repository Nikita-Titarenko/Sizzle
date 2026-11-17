package com.sizzle.app.data.api

import android.content.Context
import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext
import com.sizzle.app.R
import com.sizzle.app.data.model.ApiError
import com.sizzle.app.data.model.ReviewErrorBody
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
    /** ErrorKey values of the server's errors, see [ErrorKeys]. */
    val keys: List<Int> = emptyList(),
    /** The raw error body, for responses that carry more than errors (e.g. correctedRecipe). */
    val body: String = "",
    /** A message chosen by the app, shown instead of the server's. */
    val localMessage: Int? = null,
) : Exception(cause) {

    fun message(context: Context): String = when {
        localMessage != null -> context.getString(localMessage)
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
        httpFailure(code(), body, this)
    }
    else -> ApiFailure(cause = this)
}

/** Reads `[{key, message}]`, or `{errors: [...]}` when the body carries more than the errors. */
fun httpFailure(code: Int, body: String, cause: Throwable? = null): ApiFailure {
    val errors = runCatching { ApiClient.json.decodeFromString(ListSerializer(ApiError.serializer()), body) }
        .recoverCatching { ApiClient.json.decodeFromString(ReviewErrorBody.serializer(), body).errors }
        .getOrNull()
        .orEmpty()
    return ApiFailure(
        unreachable = code in listOf(502, 503, 504),
        code = code,
        serverMessages = errors.map { it.message }.filter { it.isNotBlank() },
        cause = cause,
        keys = errors.mapNotNull { it.key },
        body = body,
    )
}

/** ErrorKey values the app reacts to (the API sends the enum as a number). */
object ErrorKeys {
    const val FORBIDDEN = 7
    const val RECIPE_REJECTED = 8
    const val RECIPE_NEEDS_CORRECTION = 9
}

val Result<*>.failure: ApiFailure? get() = exceptionOrNull() as ApiFailure?
