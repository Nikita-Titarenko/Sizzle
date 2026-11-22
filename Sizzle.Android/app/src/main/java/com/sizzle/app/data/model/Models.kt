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
    /** Per 100 g / 100 ml, or per piece. */
    val calories: Int = 0,
    val children: List<Ingredient> = emptyList(),
)

/** An ingredient chosen in a picker, with its parent for display ("Potato › Sweet Potato"). */
data class IngredientChoice(
    val id: String,
    val name: String,
    val unit: String,
    val parentName: String?,
    val parentId: String? = null,
    val calories: Int = 0,
)

/** Values of the API's IngredientUnit enum, sent by name when creating a new ingredient. */
enum class IngredientUnit(val short: String, private val localizedShort: Set<String>) {
    // Localized forms come from the API's InitialData/ingredient-unit.json; keep them in sync.
    Grams("g", setOf("г")),
    Milliliters("ml", setOf("мл")),
    Pieces("pcs", setOf("шт"));

    companion object {
        /** A catalog short unit in any language the API answers in ("g", "г"). */
        fun fromShort(short: String): IngredientUnit? {
            val key = short.trim().lowercase()
            return entries.firstOrNull { it.short == key || key in it.localizedShort }
        }

        /** Accepts every form the API uses: "g"/"г" on catalog items, "Grams" from the AI. */
        fun parse(value: String): IngredientUnit? =
            fromShort(value) ?: entries.firstOrNull { it.name.equals(value.trim(), ignoreCase = true) }
    }
}

/** A recipe in a list (explore, profiles, saved). Lists are cached for everyone, so they carry no per-user state. */
@Serializable
data class Recipe(
    val id: String,
    val userId: String,
    val userName: String = "",
    val userImageUrl: String? = null,
    val name: String = "",
    val description: String = "",
    val cookingTimeMinutes: Int = 0,
    val categoryId: String = "",
    val fileUrl: String? = null,
    val viewCount: Int = 0,
    val likeCount: Int = 0,
    val creationDate: String = "",
    val isDraft: Boolean = false,
    val isAIGeneratedImage: Boolean = false,
    /** Sum of the ingredients' calories; 0 when the catalog has none for them. */
    val calories: Double = 0.0,
)

/** A recipe in the personalized feed, with the signed-in user's like, follow and save state. */
@Serializable
data class FeedRecipe(
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
    val likeCount: Int = 0,
    val creationDate: String = "",
    val isDraft: Boolean = false,
    val isAIGeneratedImage: Boolean = false,
    /** Sum of the ingredients' calories; 0 when the catalog has none for them. */
    val calories: Double = 0.0,
    val isLiked: Boolean = false,
    val isSaved: Boolean = false,
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
    val likeCount: Int = 0,
    val creationDate: String = "",
    val isDraft: Boolean = false,
    val isAIGeneratedImage: Boolean = false,
    /** Sum of the ingredients' calories; 0 when the catalog has none for them. */
    val calories: Double = 0.0,
    val isLiked: Boolean = false,
    val isSaved: Boolean = false,
    val steps: List<RecipeStep> = emptyList(),
    val ingredients: List<RecipeIngredient> = emptyList(),
)

@Serializable
data class RecipeLike(val recipeId: String, val isLiked: Boolean)

@Serializable
data class RecipeSave(val recipeId: String, val isSaved: Boolean)

@Serializable
data class RecipeShare(val recipeId: String, val isShared: Boolean = true)

/** [id] is the API's SubscriptionPlan enum: 1 monthly, 2 half-yearly, 3 yearly. */
@Serializable
data class SubscriptionPlan(
    val id: Int,
    val name: String = "",
    val durationMonths: Int,
    val price: Double,
    val currency: String = "UAH",
)

/** A paid order. */
@Serializable
data class SubscriptionPayment(
    val orderId: String,
    val plan: Int,
    val amount: Double,
    val currency: String = "UAH",
    val startsAt: String,
    val expiresAt: String,
)

/** [remainingSeconds] is 0 without an active subscription; [history] is newest first. */
@Serializable
data class SubscriptionStatus(val remainingSeconds: Long = 0, val history: List<SubscriptionPayment> = emptyList())

/** A Stripe Payment Intent for one order. */
@Serializable
data class SubscriptionCheckout(val orderId: String, val clientSecret: String, val publishableKey: String)

@Serializable
data class IngredientProportion(val id: String, val quantity: Int)

@Serializable
data class ProportionsRequest(val initial: List<IngredientProportion>, val changedIngredient: IngredientProportion)

/**
 * A recipe as the AI reads and writes it (recipes/generate, generate-from-video, generate-image
 * and the reviewer's correctedRecipe). Ingredients without an id are new to the catalog.
 */
@Serializable
data class GeneratedRecipe(
    val name: String = "",
    val description: String = "",
    val cookingTimeMinutes: Int = 0,
    val categoryId: String = EMPTY_ID,
    val steps: List<GeneratedStep> = emptyList(),
    val ingredients: List<GeneratedIngredient> = emptyList(),
) {
    companion object {
        const val EMPTY_ID = "00000000-0000-0000-0000-000000000000"
    }
}

@Serializable
data class GeneratedStep(val name: String = "", val description: String = "")

@Serializable
data class GeneratedIngredient(
    val id: String? = null,
    val name: String = "",
    val parentId: String? = null,
    /** API enum name: Grams, Milliliters or Pieces. */
    val unit: String = "Grams",
    val quantity: Int = 0,
)

@Serializable
data class GenerateRecipeRequest(
    val message: String,
    /** Null (left out of the JSON) for an empty form: the AI then writes a new recipe. */
    val recipe: GeneratedRecipe? = null,
)

/** One server-sent event of a generation stream: chunk (raw JSON text), completed or error. */
@Serializable
data class GenerationEvent(
    val type: String = "",
    val text: String? = null,
    val result: GenerationResult? = null,
    val errors: List<GenerationError>? = null,
)

@Serializable
data class GenerationResult(val recipe: GeneratedRecipe, val message: String = "")

/** Stream errors carry the key as a name ("Forbidden"), unlike [ApiError]. */
@Serializable
data class GenerationError(val message: String = "")

/** 400 from create/update when the AI reviewer fixed the recipe instead of rejecting it. */
@Serializable
data class ReviewErrorBody(
    val errors: List<ApiError> = emptyList(),
    val correctedRecipe: GeneratedRecipe? = null,
)

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
    val description: String = "",
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
