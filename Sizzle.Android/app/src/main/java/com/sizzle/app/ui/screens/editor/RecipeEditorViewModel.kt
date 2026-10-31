package com.sizzle.app.ui.screens.editor

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sizzle.app.AppContainer
import com.sizzle.app.R
import com.sizzle.app.data.MediaFiles
import com.sizzle.app.data.PickedMedia
import com.sizzle.app.data.RecipeEvent
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.failure
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.Category
import com.sizzle.app.data.model.IngredientChoice
import com.sizzle.app.data.model.IngredientUnit
import okhttp3.MultipartBody
import kotlinx.coroutines.launch

data class IngredientDraft(
    val id: String,
    val name: String,
    val parentName: String?,
    val unit: String,
    val quantity: String = "1",
)

data class NewIngredientDraft(
    val name: String = "",
    val unit: IngredientUnit = IngredientUnit.Grams,
    val quantity: String = "100",
    val parent: IngredientChoice? = null,
)

data class StepDraft(
    val name: String = "",
    val description: String = "",
    val files: List<PickedMedia> = emptyList(),
)

/** Create or edit a recipe. Mirrors the website's recipe form, including its validation. */
class RecipeEditorViewModel(private val container: AppContainer, val recipeId: String?) : ViewModel() {
    var categories by mutableStateOf<List<Category>>(emptyList())
        private set
    var name by mutableStateOf("")
    var description by mutableStateOf("")
    var cookingTime by mutableStateOf(30)
    var categoryId by mutableStateOf("")
    var cover by mutableStateOf<PickedMedia?>(null)
    var currentCoverUrl by mutableStateOf<String?>(null)
        private set
    var ingredients by mutableStateOf<List<IngredientDraft>>(emptyList())
    var newIngredients by mutableStateOf<List<NewIngredientDraft>>(emptyList())
    var steps by mutableStateOf(listOf(StepDraft()))

    var loading by mutableStateOf(recipeId != null)
        private set
    var busy by mutableStateOf(false)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set

    /** A picked file the API would reject (format or size). */
    var mediaProblem by mutableStateOf<String?>(null)

    init {
        viewModelScope.launch { runApi { container.api.getCategories() }.onSuccess { categories = it } }
        if (recipeId != null) load(recipeId)
    }

    /** What is still missing before the recipe can be published, or null when it is complete. */
    val missingHint: Int?
        get() = when {
            name.isBlank() -> R.string.editor_missing_name
            description.isBlank() -> R.string.editor_missing_description
            categoryId.isEmpty() -> R.string.editor_missing_category
            cookingTime <= 0 -> R.string.editor_missing_time
            steps.any { it.name.isBlank() || it.description.isBlank() } -> R.string.editor_missing_steps
            else -> null
        }

    fun adjustTime(delta: Int) {
        cookingTime = maxOf(1, cookingTime + delta)
    }

    /** Validates picked files; returns the acceptable ones and reports the first problem. */
    fun accept(media: List<PickedMedia>, allowVideo: Boolean): List<PickedMedia> {
        val problems = media.mapNotNull { MediaFiles.problem(container.appContext, it, allowVideo) }
        mediaProblem = problems.firstOrNull()
        return media.filter { MediaFiles.problem(container.appContext, it, allowVideo) == null }
    }

    fun addIngredient(choice: IngredientChoice) {
        ingredients = ingredients + IngredientDraft(choice.id, choice.name, choice.parentName, choice.unit)
    }

    fun updateIngredient(index: Int, quantity: String) {
        ingredients = ingredients.mapIndexed { i, it -> if (i == index) it.copy(quantity = quantity.filter(Char::isDigit)) else it }
    }

    fun removeIngredient(index: Int) {
        ingredients = ingredients.filterIndexed { i, _ -> i != index }
    }

    fun addNewIngredient() {
        newIngredients = newIngredients + NewIngredientDraft()
    }

    fun updateNewIngredient(index: Int, change: (NewIngredientDraft) -> NewIngredientDraft) {
        newIngredients = newIngredients.mapIndexed { i, it -> if (i == index) change(it) else it }
    }

    /** A variant usually shares its parent's unit, so it is preselected. */
    fun setParent(index: Int, parent: IngredientChoice?) = updateNewIngredient(index) { draft ->
        val unit = parent?.let { IngredientUnit.fromShort(it.unit) } ?: draft.unit
        draft.copy(parent = parent, unit = unit)
    }

    fun removeNewIngredient(index: Int) {
        newIngredients = newIngredients.filterIndexed { i, _ -> i != index }
    }

    fun addStep() {
        steps = steps + StepDraft()
    }

    fun updateStep(index: Int, change: (StepDraft) -> StepDraft) {
        steps = steps.mapIndexed { i, it -> if (i == index) change(it) else it }
    }

    fun removeStep(index: Int) {
        steps = steps.filterIndexed { i, _ -> i != index }
    }

    fun submit(onSaved: (String) -> Unit) {
        if (missingHint != null || busy) return
        busy = true
        failure = null
        viewModelScope.launch {
            val parts = buildParts()
            val result = runApi {
                if (recipeId == null) container.api.createRecipe(parts) else container.api.updateRecipe(recipeId, parts)
            }
            result.onSuccess { recipe ->
                container.recipeEvents.emit(RecipeEvent.Saved)
                onSaved(recipeId ?: recipe.id)
            }
            failure = result.failure
            busy = false
        }
    }

    /** Multipart fields in the shape ASP.NET model binding expects (Steps[0].Name, …). */
    private fun buildParts(): List<MultipartBody.Part> = buildList {
        val context = container.appContext
        add(MediaFiles.field("Name", name.trim()))
        add(MediaFiles.field("Description", description.trim()))
        add(MediaFiles.field("CookingTimeMinutes", cookingTime.toString()))
        add(MediaFiles.field("CategoryId", categoryId))
        cover?.let { add(MediaFiles.part(context, "File", it)) }
        steps.forEachIndexed { i, step ->
            add(MediaFiles.field("Steps[$i].Name", step.name.trim()))
            add(MediaFiles.field("Steps[$i].Description", step.description.trim()))
            step.files.forEach { add(MediaFiles.part(context, "Steps[$i].Files", it)) }
        }
        ingredients.forEachIndexed { i, ingredient ->
            add(MediaFiles.field("Ingredients[$i].Id", ingredient.id))
            add(MediaFiles.field("Ingredients[$i].Quantity", (ingredient.quantity.toIntOrNull() ?: 0).toString()))
        }
        newIngredients.filter { it.name.isNotBlank() }.forEachIndexed { i, ingredient ->
            add(MediaFiles.field("NewIngredients[$i].Name", ingredient.name.trim()))
            ingredient.parent?.let { add(MediaFiles.field("NewIngredients[$i].ParentId", it.id)) }
            add(MediaFiles.field("NewIngredients[$i].Unit", ingredient.unit.name))
            add(MediaFiles.field("NewIngredients[$i].Quantity", (ingredient.quantity.toIntOrNull() ?: 0).toString()))
        }
    }

    private fun load(id: String) {
        viewModelScope.launch {
            val result = runApi { container.api.getRecipe(id) }
            result.onSuccess { recipe ->
                name = recipe.name
                description = recipe.description
                cookingTime = recipe.cookingTimeMinutes
                categoryId = recipe.categoryId
                currentCoverUrl = recipe.fileUrl
                ingredients = recipe.ingredients.map { IngredientDraft(it.id, it.name, null, it.unit, it.quantity.toString()) }
                // The API replaces steps on save, so existing step media must be picked again.
                steps = recipe.steps.map { StepDraft(it.name, it.description) }.ifEmpty { listOf(StepDraft()) }
            }
            failure = result.failure
            loading = false
        }
    }
}
