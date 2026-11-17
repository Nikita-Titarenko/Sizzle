package com.sizzle.app.data.api

import com.sizzle.app.data.model.Category
import com.sizzle.app.data.model.Comment
import com.sizzle.app.data.model.ConfirmEmailRequest
import com.sizzle.app.data.model.ConfirmEmailResult
import com.sizzle.app.data.model.CreateCommentRequest
import com.sizzle.app.data.model.DeviceTokenRequest
import com.sizzle.app.data.model.FeedRecipe
import com.sizzle.app.data.model.GeneratedRecipe
import com.sizzle.app.data.model.GoogleTokenRequest
import com.sizzle.app.data.model.Ingredient
import com.sizzle.app.data.model.IngredientProportion
import com.sizzle.app.data.model.LoginRequest
import com.sizzle.app.data.model.LoginResult
import com.sizzle.app.data.model.PageResponse
import com.sizzle.app.data.model.ProportionsRequest
import com.sizzle.app.data.model.Recipe
import com.sizzle.app.data.model.RecipeDetails
import com.sizzle.app.data.model.RecipeLike
import com.sizzle.app.data.model.RecipeSave
import com.sizzle.app.data.model.RecipeShare
import com.sizzle.app.data.model.RegisterRequest
import com.sizzle.app.data.model.RegisterResult
import com.sizzle.app.data.model.SubscriptionStatus
import com.sizzle.app.data.model.UpdateCommentRequest
import com.sizzle.app.data.model.User
import com.sizzle.app.data.model.UserIdRequest
import okhttp3.MultipartBody
import okhttp3.ResponseBody
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.HTTP
import retrofit2.http.Multipart
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Part
import retrofit2.http.Path
import retrofit2.http.Query
import retrofit2.http.QueryMap

/** The Sizzle REST API (Sizzle.WebApi). Failed calls throw retrofit2.HttpException or IOException. */
interface SizzleApi {

    // Catalog

    @GET("api/categories")
    suspend fun getCategories(): List<Category>

    @GET("api/ingredients")
    suspend fun getIngredients(
        @Query("Search") search: String?,
        @Query("PageIndex") pageIndex: Int = 0,
        @Query("PageSize") pageSize: Int = 20,
    ): List<Ingredient>

    /** Scales the other ingredients after one quantity changed; same order as `initial`. */
    @POST("api/ingredients/proportions")
    suspend fun getProportions(@Body body: ProportionsRequest): List<IngredientProportion>

    // Recipes

    @GET("api/recipes")
    suspend fun getRecipes(
        @Query("PageIndex") pageIndex: Int,
        @Query("PageSize") pageSize: Int,
        @Query("Search") search: String? = null,
        @Query("UserId") userId: String? = null,
        @Query("CategoryIds") categoryIds: List<String> = emptyList(),
        /** Built with [ingredientFilter]. */
        @QueryMap ingredients: Map<String, String> = emptyMap(),
    ): PageResponse<Recipe>

    /** The feed: recommendations for the signed-in user, or the public list for guests. */
    @GET("api/recipes/personalized")
    suspend fun getPersonalizedRecipes(
        @Query("PageIndex") pageIndex: Int,
        @Query("PageSize") pageSize: Int,
        @Query("CategoryIds") categoryIds: List<String> = emptyList(),
        /** Built with [ingredientFilter]. */
        @QueryMap ingredients: Map<String, String> = emptyMap(),
    ): PageResponse<FeedRecipe>

    @GET("api/recipes/saved")
    suspend fun getSavedRecipes(
        @Query("PageIndex") pageIndex: Int,
        @Query("PageSize") pageSize: Int,
    ): PageResponse<Recipe>

    @GET("api/recipes/mine")
    suspend fun getMyRecipes(
        @Query("PageIndex") pageIndex: Int,
        @Query("PageSize") pageSize: Int,
    ): PageResponse<Recipe>

    /** The signed-in user's unpublished drafts. */
    @GET("api/recipes/mine/drafts")
    suspend fun getDraftRecipes(
        @Query("PageIndex") pageIndex: Int,
        @Query("PageSize") pageSize: Int,
    ): PageResponse<Recipe>

    /** Recipes from cooks the signed-in user follows, newest first. */
    @GET("api/recipes/following")
    suspend fun getFollowingRecipes(
        @Query("PageIndex") pageIndex: Int,
        @Query("PageSize") pageSize: Int,
    ): PageResponse<FeedRecipe>

    /** Draws a cover for the recipe; PNG or JPEG bytes. Needs an active subscription (403). */
    @POST("api/recipes/generate-image")
    suspend fun generateRecipeImage(@Body recipe: GeneratedRecipe): ResponseBody

    @GET("api/recipes/{id}")
    suspend fun getRecipe(@Path("id") id: String): RecipeDetails

    @Multipart
    @POST("api/recipes")
    suspend fun createRecipe(@Part parts: List<MultipartBody.Part>): Recipe

    @Multipart
    @PUT("api/recipes/{id}")
    suspend fun updateRecipe(@Path("id") id: String, @Part parts: List<MultipartBody.Part>): Recipe

    @DELETE("api/recipes/{id}")
    suspend fun deleteRecipe(@Path("id") id: String)

    @POST("api/recipes/{id}/toggle-like")
    suspend fun toggleLike(@Path("id") id: String): RecipeLike

    @POST("api/recipes/{id}/toggle-save")
    suspend fun toggleSave(@Path("id") id: String): RecipeSave

    /** Records a share; the personalized feed learns from views, likes and shares. */
    @POST("api/recipes/{id}/update-share")
    suspend fun registerShare(@Path("id") id: String): RecipeShare

    @POST("api/recipes/{id}/update-view")
    suspend fun registerView(@Path("id") id: String)

    // Comments

    @GET("api/recipes/{recipeId}/comments")
    suspend fun getComments(
        @Path("recipeId") recipeId: String,
        @Query("PageIndex") pageIndex: Int,
        @Query("PageSize") pageSize: Int = 20,
    ): PageResponse<Comment>

    @POST("api/recipes/{recipeId}/comments")
    suspend fun createComment(@Path("recipeId") recipeId: String, @Body body: CreateCommentRequest): Comment

    @PUT("api/comments/{id}")
    suspend fun updateComment(@Path("id") id: String, @Body body: UpdateCommentRequest): Comment

    @DELETE("api/comments/{id}")
    suspend fun deleteComment(@Path("id") id: String)

    // Users and authentication

    @POST("api/users/register")
    suspend fun register(@Body body: RegisterRequest): RegisterResult

    @POST("api/users/confirm-email")
    suspend fun confirmEmail(@Body body: ConfirmEmailRequest): ConfirmEmailResult

    @POST("api/users/resend-confirm-email")
    suspend fun resendConfirmEmail(@Body body: UserIdRequest)

    @POST("api/users/login")
    suspend fun login(@Body body: LoginRequest): LoginResult

    @POST("api/users/google-token")
    suspend fun loginWithGoogle(@Body body: GoogleTokenRequest): LoginResult

    @GET("api/users/{id}")
    suspend fun getUser(@Path("id") id: String): User

    @Multipart
    @PUT("api/users")
    suspend fun updateProfile(@Part parts: List<MultipartBody.Part>)

    @POST("api/users/{id}/follow")
    suspend fun follow(@Path("id") id: String)

    @DELETE("api/users/{id}/follow")
    suspend fun unfollow(@Path("id") id: String)

    @GET("api/users/followers")
    suspend fun getFollowers(): List<User>

    @GET("api/users/following")
    suspend fun getFollowing(): List<User>

    // Subscriptions

    @GET("api/subscriptions")
    suspend fun getSubscription(): SubscriptionStatus

    // Push notifications

    @POST("api/users/devices")
    suspend fun registerDevice(@Body body: DeviceTokenRequest)

    @HTTP(method = "DELETE", path = "api/users/devices", hasBody = true)
    suspend fun unregisterDevice(@Body body: DeviceTokenRequest)
}

/**
 * Ingredient filter in the shape ASP.NET binds: Ingredients[0].IngredientId=…, plus an optional
 * Ingredients[0].Quantity ("I have at most this much": only recipes that need no more match).
 */
fun ingredientFilter(ids: List<String>, quantities: Map<String, Int> = emptyMap()): Map<String, String> =
    buildMap {
        ids.forEachIndexed { i, id ->
            put("Ingredients[$i].IngredientId", id)
            quantities[id]?.takeIf { it > 0 }?.let { put("Ingredients[$i].Quantity", it.toString()) }
        }
    }
