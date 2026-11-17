package com.sizzle.app.ui.screens.editor

import android.net.Uri
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.sizzle.app.AppContainer
import com.sizzle.app.R
import com.sizzle.app.data.MediaFiles
import com.sizzle.app.data.PartialJson
import com.sizzle.app.data.PickedMedia
import com.sizzle.app.data.RecipeEvent
import com.sizzle.app.data.api.ApiClient
import com.sizzle.app.data.api.ApiFailure
import com.sizzle.app.data.api.ErrorKeys
import com.sizzle.app.data.api.failure
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.api.toApiFailure
import com.sizzle.app.data.model.Category
import com.sizzle.app.data.model.GeneratedIngredient
import com.sizzle.app.data.model.GeneratedRecipe
import com.sizzle.app.data.model.GeneratedStep
import com.sizzle.app.data.model.GenerationEvent
import com.sizzle.app.data.model.IngredientChoice
import com.sizzle.app.data.model.IngredientUnit
import com.sizzle.app.data.model.Recipe
import com.sizzle.app.data.model.ReviewErrorBody
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import okhttp3.MultipartBody
import java.io.File
import kotlin.math.roundToInt

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

enum class ChatRole { User, Assistant, Error }

data class ChatMessage(
    val role: ChatRole,
    val text: String,
    /** A user message that was a video upload. */
    val video: Boolean = false,
    /** The answer came with a recipe that was put into the form. */
    val applied: Boolean = false,
)

/** What can be read from the half-written JSON while an answer streams in. */
data class StreamPreview(val name: String = "", val ingredients: Int = 0, val steps: Int = 0, val message: String = "")

/** Why the AI reviewer stopped a publish: it fixed the recipe, or rejected it. */
data class ReviewNotice(val corrected: Boolean, val reason: String)

enum class EditorMode { Loading, Choose, Form }

enum class Busy { Publish, Draft, Image }

/** Everything the user can change in the form; kept to restore earlier versions. */
private data class Snapshot(
    val name: String,
    val description: String,
    val cookingTime: Int,
    val categoryId: String,
    val cover: PickedMedia?,
    val coverIsAI: Boolean,
    val ingredients: List<IngredientDraft>,
    val newIngredients: List<NewIngredientDraft>,
    val steps: List<StepDraft>,
)

/**
 * Create or edit a recipe, alone or with the AI. Mirrors the website's recipe form: the recipe in
 * the form is the AI's context, every AI answer replaces it, and earlier versions can be restored
 * for as long as the editor is open. A new recipe first offers the user's drafts.
 */
class RecipeEditorViewModel(private val container: AppContainer, initialId: String?) : ViewModel() {
    /** The saved recipe this editor writes to; set by navigation or by the first "Save draft". */
    var recipeId by mutableStateOf(initialId)
        private set
    var isDraft by mutableStateOf(false)
        private set

    var mode by mutableStateOf(EditorMode.Loading)
        private set
    var drafts by mutableStateOf<List<Recipe>>(emptyList())
        private set

    var categories by mutableStateOf<List<Category>>(emptyList())
        private set
    var name by mutableStateOf("")
    var description by mutableStateOf("")
    var cookingTime by mutableStateOf(30)
    var categoryId by mutableStateOf("")
    var cover by mutableStateOf<PickedMedia?>(null)
        private set
    var coverIsAI by mutableStateOf(false)
        private set
    var currentCoverUrl by mutableStateOf<String?>(null)
        private set
    var ingredients by mutableStateOf<List<IngredientDraft>>(emptyList())
    var newIngredients by mutableStateOf<List<NewIngredientDraft>>(emptyList())
    var steps by mutableStateOf(listOf(StepDraft()))

    var busy by mutableStateOf<Busy?>(null)
        private set
    var failure by mutableStateOf<ApiFailure?>(null)
        private set
    /** A short confirmation, e.g. "Draft saved". */
    var notice by mutableStateOf<Int?>(null)
        private set

    /** A picked file the API would reject (format or size). */
    var mediaProblem by mutableStateOf<String?>(null)

    // AI

    private var history by mutableStateOf<List<Snapshot>>(emptyList())
    val canRestore: Boolean get() = history.isNotEmpty()

    /** Fields the AI changed in its last edit, highlighted until the user edits them. */
    var changed by mutableStateOf<Set<String>>(emptySet())
        private set
    var review by mutableStateOf<ReviewNotice?>(null)
        private set
    /** Whether the AI video and image tools are available; null while unknown. */
    var subscribed by mutableStateOf<Boolean?>(null)
        private set
    var messages by mutableStateOf<List<ChatMessage>>(emptyList())
        private set
    var preview by mutableStateOf<StreamPreview?>(null)
        private set
    private var generation: Job? = null

    val generating: Boolean get() = preview != null
    val canSaveDraft: Boolean get() = recipeId == null || isDraft

    /** Calories of the catalog ingredients; new ones get theirs when the recipe is published. */
    val calories: Int
        get() = ingredients.sumOf { container.catalog.caloriesOf(it.id, it.quantity.toIntOrNull() ?: 0) ?: 0.0 }.roundToInt()

    fun ingredientCalories(draft: IngredientDraft): Int? =
        container.catalog.caloriesOf(draft.id, draft.quantity.toIntOrNull() ?: 0)?.roundToInt()

    init {
        viewModelScope.launch { runApi { container.api.getCategories() }.onSuccess { categories = it } }
        viewModelScope.launch {
            subscribed = runApi { container.api.getSubscription() }.getOrNull()?.let { it.remainingSeconds > 0 }
        }
        viewModelScope.launch { container.catalog.ensureLoaded() }
        if (initialId != null) load(initialId) else loadDrafts()
    }

    /** What is still missing before the recipe can be published, or null when it is complete. */
    val missingHint: Int?
        get() = basicsMissing() ?: if (stepsMissing(allowEmpty = false)) R.string.editor_missing_steps else null

    /** Drafts need what the API checks on every save; empty steps are simply left out. */
    val draftMissingHint: Int?
        get() = basicsMissing() ?: if (stepsMissing(allowEmpty = true)) R.string.editor_missing_steps else null

    private fun basicsMissing(): Int? = when {
        name.isBlank() -> R.string.editor_missing_name
        description.isBlank() -> R.string.editor_missing_description
        categoryId.isEmpty() -> R.string.editor_missing_category
        cookingTime <= 0 -> R.string.editor_missing_time
        else -> null
    }

    private fun stepsMissing(allowEmpty: Boolean) = steps.any {
        val hasName = it.name.isNotBlank()
        val hasDescription = it.description.isNotBlank()
        if (allowEmpty) hasName != hasDescription else !hasName || !hasDescription
    }

    // Drafts

    fun startFresh() {
        mode = EditorMode.Form
    }

    fun deleteDraft(draft: Recipe) {
        viewModelScope.launch {
            val result = runApi { container.api.deleteRecipe(draft.id) }
            result.onSuccess {
                drafts = drafts.filterNot { it.id == draft.id }
                if (drafts.isEmpty()) mode = EditorMode.Form
                container.recipeEvents.emit(RecipeEvent.Deleted(draft.id))
            }
            failure = result.failure
        }
    }

    // Form editing

    fun adjustTime(delta: Int) {
        cookingTime = maxOf(1, cookingTime + delta)
        unmark("time")
    }

    fun pickCover(media: PickedMedia?) {
        cover = media
        coverIsAI = false
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
        ingredients.getOrNull(index)?.let { unmark("ingredient-${it.id}") }
    }

    fun removeIngredient(index: Int) {
        ingredients = ingredients.filterIndexed { i, _ -> i != index }
    }

    fun addNewIngredient() {
        newIngredients = newIngredients + NewIngredientDraft()
    }

    fun updateNewIngredient(index: Int, change: (NewIngredientDraft) -> NewIngredientDraft) {
        newIngredients = newIngredients.mapIndexed { i, it -> if (i == index) change(it) else it }
        unmark("new-$index")
    }

    /** A variant usually shares its parent's unit, so it is preselected. */
    fun setParent(index: Int, parent: IngredientChoice?) = updateNewIngredient(index) { draft ->
        val unit = parent?.let { IngredientUnit.parse(it.unit) } ?: draft.unit
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

    fun isChanged(key: String) = key in changed

    fun unmark(key: String) {
        if (key in changed) changed = changed - key
    }

    fun dismissReview() {
        review = null
    }

    // AI chat

    fun send(message: String) {
        val text = message.trim()
        if (text.isEmpty() || generating) return
        push(ChatMessage(ChatRole.User, text))
        run(container.ai.generate(text, currentRecipe()))
    }

    fun generateFromVideo(video: PickedMedia) {
        if (generating) return
        val problem = MediaFiles.problem(container.appContext, video, allowVideo = true)
        if (problem != null || !video.isVideo) {
            push(ChatMessage(ChatRole.Error, problem ?: container.appContext.getString(R.string.ai_unsupported_video, video.name)))
            return
        }
        push(ChatMessage(ChatRole.User, video.name, video = true))
        run(container.ai.generateFromVideo(container.appContext, video))
    }

    fun stop() {
        generation?.cancel()
        generation = null
        preview = null
        push(ChatMessage(ChatRole.Error, container.appContext.getString(R.string.ai_stopped)))
    }

    private fun run(stream: Flow<GenerationEvent>) {
        preview = StreamPreview()
        generation = viewModelScope.launch {
            val raw = StringBuilder()
            var finished = false
            try {
                stream.collect { event ->
                    when (event.type) {
                        "chunk" -> {
                            raw.append(event.text.orEmpty())
                            preview = readPreview(raw.toString())
                        }
                        "completed" -> event.result?.let { result ->
                            finished = true
                            preview = null
                            push(ChatMessage(ChatRole.Assistant, result.message, applied = true))
                            applyGenerated(result.recipe)
                        }
                        "error" -> {
                            finished = true
                            preview = null
                            val text = event.errors.orEmpty().map { it.message }.filter { it.isNotBlank() }.joinToString(". ")
                            push(ChatMessage(ChatRole.Error, text.ifEmpty { container.appContext.getString(R.string.ai_failed) }))
                        }
                    }
                }
                if (!finished) {
                    preview = null
                    push(ChatMessage(ChatRole.Error, container.appContext.getString(R.string.ai_failed)))
                }
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                preview = null
                push(ChatMessage(ChatRole.Error, describe(e.toApiFailure())))
            }
        }
    }

    private fun describe(failure: ApiFailure): String = when {
        failure.code == 403 -> container.appContext.getString(R.string.ai_subscription_required)
        else -> failure.message(container.appContext)
    }

    private fun push(message: ChatMessage) {
        messages = messages + message
    }

    /** Puts an AI-written recipe into the form, keeping the current one to restore. */
    fun applyGenerated(recipe: GeneratedRecipe) {
        val before = snapshot()
        history = history + before
        review = null
        failure = null

        name = recipe.name
        description = recipe.description
        if (recipe.cookingTimeMinutes > 0) cookingTime = recipe.cookingTimeMinutes
        if (recipe.categoryId != GeneratedRecipe.EMPTY_ID && recipe.categoryId.isNotEmpty()) categoryId = recipe.categoryId
        // Step media stays with the step at the same position.
        steps = recipe.steps.mapIndexed { i, step -> StepDraft(step.name, step.description, before.steps.getOrNull(i)?.files.orEmpty()) }
            .ifEmpty { listOf(StepDraft()) }
        val catalog = container.catalog
        // The reviewer can map a "new" ingredient onto one the recipe already has: merge them.
        val merged = recipe.ingredients.filter { it.id != null }.groupBy { it.id }
            .map { (_, same) -> same.first().copy(quantity = same.sumOf { it.quantity }) }
        ingredients = merged.map { item ->
            val known = catalog.find(item.id)
            IngredientDraft(
                id = item.id!!,
                name = item.name.ifBlank { known?.name.orEmpty() },
                parentName = known?.parentName,
                unit = known?.unit ?: IngredientUnit.parse(item.unit)?.short ?: item.unit,
                quantity = item.quantity.toString(),
            )
        }
        newIngredients = recipe.ingredients.filter { it.id == null }.map { item ->
            NewIngredientDraft(
                name = item.name,
                unit = IngredientUnit.parse(item.unit) ?: IngredientUnit.Grams,
                quantity = item.quantity.toString(),
                parent = catalog.find(item.parentId),
            )
        }
        changed = diff(before, snapshot())
    }

    fun restorePrevious() {
        val previous = history.lastOrNull() ?: return
        history = history.dropLast(1)
        restore(previous)
        changed = emptySet()
        review = null
    }

    fun generateCover() {
        if (busy != null) return
        if (subscribed == false) {
            failure = ApiFailure(localMessage = R.string.ai_subscription_required)
            return
        }
        if (name.isBlank()) {
            failure = ApiFailure(localMessage = R.string.ai_cover_needs_name)
            return
        }
        busy = Busy.Image
        failure = null
        viewModelScope.launch {
            val result = runApi {
                val body = container.api.generateRecipeImage(toGenerated())
                val extension = if (body.contentType()?.subtype == "jpeg") "jpg" else "png"
                val mime = body.contentType()?.toString() ?: "image/png"
                withContext(Dispatchers.IO) {
                    val file = File(container.appContext.cacheDir, "ai-cover-${System.currentTimeMillis()}.$extension")
                    body.byteStream().use { input -> file.outputStream().use { input.copyTo(it) } }
                    PickedMedia(Uri.fromFile(file), file.name, file.length(), mime)
                }
            }
            result.onSuccess { media ->
                history = history + snapshot()
                cover = media
                coverIsAI = true
            }
            result.failure?.let { failure = if (it.code == 403) ApiFailure(localMessage = R.string.ai_subscription_required) else it }
            busy = null
        }
    }

    /** The form as the AI reads it; null when it is still empty, so the AI writes a new recipe. */
    private fun currentRecipe(): GeneratedRecipe? {
        val empty = name.isBlank() && description.isBlank() && ingredients.isEmpty() &&
            newIngredients.none { it.name.isNotBlank() } && steps.none { it.name.isNotBlank() || it.description.isNotBlank() }
        return if (empty) null else toGenerated()
    }

    private fun toGenerated() = GeneratedRecipe(
        name = name.trim(),
        description = description.trim(),
        cookingTimeMinutes = cookingTime.coerceAtLeast(0),
        categoryId = categoryId.ifEmpty { GeneratedRecipe.EMPTY_ID },
        steps = steps.filter { it.name.isNotBlank() || it.description.isNotBlank() }
            .map { GeneratedStep(it.name.trim(), it.description.trim()) },
        ingredients = ingredients.map {
            GeneratedIngredient(
                id = it.id,
                name = it.name,
                parentId = container.catalog.find(it.id)?.parentId,
                unit = (IngredientUnit.parse(it.unit) ?: IngredientUnit.Grams).name,
                quantity = it.quantity.toIntOrNull() ?: 0,
            )
        } + newIngredients.filter { it.name.isNotBlank() }.map {
            GeneratedIngredient(null, it.name.trim(), it.parent?.id, it.unit.name, it.quantity.toIntOrNull() ?: 0)
        },
    )

    // Saving

    fun publish(onPublished: (String) -> Unit) {
        if (missingHint == null) save(asDraft = false, onPublished)
    }

    fun saveDraft() {
        if (draftMissingHint == null && canSaveDraft) save(asDraft = true) {}
    }

    private fun save(asDraft: Boolean, onPublished: (String) -> Unit) {
        if (busy != null) return
        busy = if (asDraft) Busy.Draft else Busy.Publish
        failure = null
        notice = null
        review = null
        viewModelScope.launch {
            val id = recipeId
            val parts = buildParts(asDraft)
            val result = runApi {
                if (id == null) container.api.createRecipe(parts) else container.api.updateRecipe(id, parts)
            }
            busy = null
            result.onSuccess { recipe ->
                container.recipeEvents.emit(RecipeEvent.Saved)
                val savedId = id ?: recipe.id
                if (asDraft) {
                    // Stay in the editor (with the chat and history); later saves update this draft.
                    recipeId = savedId
                    isDraft = true
                    notice = R.string.editor_draft_saved
                } else {
                    if (newIngredients.any { it.name.isNotBlank() }) container.catalog.ensureLoaded(force = true)
                    onPublished(savedId)
                }
            }
            result.failure?.let(::handleSaveFailure)
        }
    }

    /**
     * The AI reviewer either rejects a recipe (spam, unsafe, nonsense) or fixes small mistakes, e.g. a
     * "new" ingredient that already exists under another name. A fix comes back as correctedRecipe:
     * it is put into the form with the changes highlighted, for the user to check and publish again.
     */
    private fun handleSaveFailure(failure: ApiFailure) {
        val reason = failure.serverMessages.joinToString(". ")
        if (failure.code == 400) {
            val corrected = runCatching { ApiClient.json.decodeFromString(ReviewErrorBody.serializer(), failure.body) }
                .getOrNull()?.correctedRecipe
            if (corrected != null) {
                applyGenerated(corrected)
                review = ReviewNotice(corrected = true, reason = reason)
                return
            }
            if (ErrorKeys.RECIPE_REJECTED in failure.keys) {
                review = ReviewNotice(corrected = false, reason = reason)
                return
            }
        }
        this.failure = failure
    }

    /** Multipart fields in the shape ASP.NET model binding expects (Steps[0].Name, …). */
    private fun buildParts(asDraft: Boolean): List<MultipartBody.Part> = buildList {
        val context = container.appContext
        add(MediaFiles.field("Name", name.trim()))
        add(MediaFiles.field("Description", description.trim()))
        add(MediaFiles.field("CookingTimeMinutes", cookingTime.toString()))
        add(MediaFiles.field("CategoryId", categoryId))
        add(MediaFiles.field("IsDraft", asDraft.toString()))
        cover?.let {
            add(MediaFiles.part(context, "File", it))
            add(MediaFiles.field("IsAIGeneratedImage", coverIsAI.toString()))
        }
        val sent = if (asDraft) steps.filter { it.name.isNotBlank() || it.description.isNotBlank() } else steps
        sent.forEachIndexed { i, step ->
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
        mode = EditorMode.Loading
        viewModelScope.launch {
            container.catalog.ensureLoaded()
            val result = runApi { container.api.getRecipe(id) }
            result.onSuccess { recipe ->
                isDraft = recipe.isDraft
                name = recipe.name
                description = recipe.description
                cookingTime = recipe.cookingTimeMinutes
                categoryId = recipe.categoryId
                currentCoverUrl = recipe.fileUrl
                ingredients = recipe.ingredients.map {
                    IngredientDraft(it.id, it.name, container.catalog.find(it.id)?.parentName, it.unit, it.quantity.toString())
                }
                // The API replaces steps on save, so existing step media must be picked again.
                steps = recipe.steps.map { StepDraft(it.name, it.description) }.ifEmpty { listOf(StepDraft()) }
            }
            failure = result.failure
            mode = EditorMode.Form
        }
    }

    private fun loadDrafts() {
        viewModelScope.launch {
            val page = runApi { container.api.getDraftRecipes(pageIndex = 0, pageSize = DRAFTS_PAGE_SIZE) }.getOrNull()
            drafts = page?.data.orEmpty()
            mode = if (drafts.isEmpty()) EditorMode.Form else EditorMode.Choose
        }
    }

    private fun snapshot() = Snapshot(name, description, cookingTime, categoryId, cover, coverIsAI, ingredients, newIngredients, steps)

    private fun restore(s: Snapshot) {
        name = s.name
        description = s.description
        cookingTime = s.cookingTime
        categoryId = s.categoryId
        cover = s.cover
        coverIsAI = s.coverIsAI
        ingredients = s.ingredients
        newIngredients = s.newIngredients
        steps = s.steps
    }

    private companion object {
        const val DRAFTS_PAGE_SIZE = 24

        fun readPreview(raw: String): StreamPreview {
            val root = PartialJson.parse(raw) as? JsonObject ?: return StreamPreview()
            val recipe = root["recipe"] as? JsonObject
            return StreamPreview(
                name = (recipe?.get("name") as? JsonPrimitive)?.takeIf { it.isString }?.content.orEmpty(),
                ingredients = (recipe?.get("ingredients") as? JsonArray)?.size ?: 0,
                steps = (recipe?.get("steps") as? JsonArray)?.size ?: 0,
                message = (root["message"] as? JsonPrimitive)?.takeIf { it.isString }?.content.orEmpty(),
            )
        }

        /** Keys of the fields that differ between two versions, as checked by [isChanged]. */
        fun diff(before: Snapshot, after: Snapshot): Set<String> = buildSet {
            fun same(a: String, b: String) = a.trim() == b.trim()
            if (!same(before.name, after.name)) add("name")
            if (!same(before.description, after.description)) add("description")
            if (before.cookingTime != after.cookingTime) add("time")
            if (before.categoryId != after.categoryId) add("category")
            after.steps.forEachIndexed { i, step ->
                val old = before.steps.getOrNull(i)
                if (old == null || !same(old.name, step.name)) add("step-$i-name")
                if (old == null || !same(old.description, step.description)) add("step-$i-description")
            }
            after.ingredients.forEach { item ->
                val old = before.ingredients.firstOrNull { it.id == item.id }
                if (old == null || old.quantity != item.quantity) add("ingredient-${item.id}")
            }
            after.newIngredients.forEachIndexed { i, item ->
                val unchanged = before.newIngredients.any {
                    it.name.trim().equals(item.name.trim(), ignoreCase = true) && it.unit == item.unit &&
                        it.quantity == item.quantity && it.parent?.id == item.parent?.id
                }
                if (!unchanged) add("new-$i")
            }
        }
    }
}
