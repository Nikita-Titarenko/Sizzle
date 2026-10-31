package com.sizzle.app.data.model

import kotlinx.serialization.Serializable

// Mirrors the API's JSON (ASP.NET camelCase). IDs are GUID strings, dates ISO-8601 strings.

@Serializable
data class PageResponse<T>(
    val data: List<T> = emptyList(),
    val totalSize: Int = 0,
    val pageIndex: Int = 0,
    val pageSize: Int = 0,
)

@Serializable
data class ApiError(
    val key: Int? = null,
    val field: String? = null,
    val message: String = "",
)

@Serializable
data class Category(val id: String, val name: String)

/** Catalog ingredient. Variants (e.g. Sweet Potato under Potato) are nested one level deep. */
@Serializable
data class Ingredient(
    val id: String,
    val name: String,
    val unit: String,
    val children: List<Ingredient> = emptyList(),
)

/** An ingredient chosen in a picker, with its parent for display ("Potato › Sweet Potato"). */
data class IngredientChoice(
    val id: String,
    val name: String,
    val unit: String,
    val parentName: String?,
)

/** Values of the API's IngredientUnit enum, sent by name when creating a new ingredient. */
enum class IngredientUnit(val short: String) {
    Grams("g"),
    Milliliters("ml"),
    Pieces("pcs");

    companion object {
        fun fromShort(short: String): IngredientUnit? = entries.firstOrNull { it.short == short }
    }
}

@Serializable
data class Recipe(
    val id: String,
    val userId: String,
    val userName: String = "",
    val userImageUrl: String? = null,
    val isFollowed: Boolean = false,
    val name: String = "",
    val description: String = "",
    val cookingTimeMinutes: Int = 0,
    val categoryId: String = "",
    val fileUrl: String? = null,
    val viewCount: Int = 0,
    val creationDate: String = "",
    val isLiked: Boolean = false,
)

@Serializable
data class RecipeStep(
    val id: String,
    val name: String = "",
    val description: String = "",
    val fileUrls: List<String> = emptyList(),
)

@Serializable
data class RecipeIngredient(
    val id: String,
    val name: String = "",
    val quantity: Int = 0,
    val unit: String = "",
)

@Serializable
data class RecipeDetails(
    val id: String,
    val userId: String,
    val userName: String = "",
    val userImageUrl: String? = null,
    val isFollowed: Boolean = false,
    val name: String = "",
    val description: String = "",
    val cookingTimeMinutes: Int = 0,
    val categoryId: String = "",
    val fileUrl: String? = null,
    val viewCount: Int = 0,
    val creationDate: String = "",
    val isLiked: Boolean = false,
    val steps: List<RecipeStep> = emptyList(),
    val ingredients: List<RecipeIngredient> = emptyList(),
)

@Serializable
data class RecipeLike(val recipeId: String, val isLiked: Boolean)

@Serializable
data class Comment(
    val id: String,
    val parentId: String? = null,
    val content: String = "",
    val createdAt: String = "",
    val userId: String,
    val userName: String = "",
    val userImageUrl: String? = null,
    val recipeId: String,
)

@Serializable
data class User(
    val id: String,
    val name: String = "",
    val email: String = "",
    val imageUrl: String? = null,
)

@Serializable
data class LoginResult(
    val userId: String,
    val emailConfirmed: Boolean = false,
    val isNewUser: Boolean = false,
    val jwtToken: String? = null,
)

@Serializable
data class RegisterResult(val userId: String)

@Serializable
data class ConfirmEmailResult(val jwtToken: String)

// Request bodies

@Serializable
data class LoginRequest(val email: String, val password: String)

@Serializable
data class RegisterRequest(val email: String, val name: String, val password: String)

@Serializable
data class ConfirmEmailRequest(val userId: String, val code: String)

@Serializable
data class UserIdRequest(val userId: String)

@Serializable
data class GoogleTokenRequest(val idToken: String)

@Serializable
data class DeviceTokenRequest(val token: String)

@Serializable
data class CreateCommentRequest(val content: String, val parentId: String? = null)

@Serializable
data class UpdateCommentRequest(val content: String)
