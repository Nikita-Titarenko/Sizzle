package com.sizzle.app.data.api

import androidx.appcompat.app.AppCompatDelegate
import com.sizzle.app.BuildConfig
import com.sizzle.app.data.SessionStore
import kotlinx.serialization.json.Json
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.Interceptor
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.util.Locale
import java.util.concurrent.TimeUnit

object ApiClient {

    val baseUrl = BuildConfig.API_URL.toHttpUrl()

    val json = Json {
        ignoreUnknownKeys = true
        explicitNulls = false
        coerceInputValues = true
    }

    fun okHttp(session: SessionStore): OkHttpClient = OkHttpClient.Builder()
        .addInterceptor(languageInterceptor)
        .addInterceptor(authInterceptor(session))
        .addInterceptor(HttpLoggingInterceptor().apply {
            level = if (BuildConfig.DEBUG) HttpLoggingInterceptor.Level.BASIC else HttpLoggingInterceptor.Level.NONE
        })
        .connectTimeout(15, TimeUnit.SECONDS)
        // Recipe videos can be up to 100 MB.
        .writeTimeout(5, TimeUnit.MINUTES)
        .readTimeout(60, TimeUnit.SECONDS)
        .build()

    fun api(client: OkHttpClient): SizzleApi = Retrofit.Builder()
        .baseUrl(baseUrl)
        .client(client)
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()
        .create(SizzleApi::class.java)

    /** Media URLs are usually absolute; relative ones ("/uploads/…") are resolved against the API. */
    fun mediaUrl(url: String?): String? = url?.let { baseUrl.resolve(it)?.toString() ?: it }

    /** Current app language: the API translates category and ingredient names with it. */
    fun languageTag(): String =
        (AppCompatDelegate.getApplicationLocales()[0] ?: Locale.getDefault()).language

    private val languageInterceptor = Interceptor { chain ->
        chain.proceed(chain.request().newBuilder().header("Accept-Language", languageTag()).build())
    }

    private fun authInterceptor(session: SessionStore) = Interceptor { chain ->
        val token = session.session.value?.token
        val request = if (token == null || chain.request().url.host != baseUrl.host) {
            chain.request()
        } else {
            chain.request().newBuilder().header("Authorization", "Bearer $token").build()
        }
        chain.proceed(request).also { response ->
            if (response.code == 401 && token != null) {
                session.onUnauthorized(token)
            }
        }
    }
}
