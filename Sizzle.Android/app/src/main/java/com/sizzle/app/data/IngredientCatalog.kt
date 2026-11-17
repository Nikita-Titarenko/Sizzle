package com.sizzle.app.data

import com.sizzle.app.data.api.SizzleApi
import com.sizzle.app.data.api.runApi
import com.sizzle.app.data.model.Ingredient
import com.sizzle.app.data.model.IngredientChoice
import com.sizzle.app.data.model.IngredientUnit
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

/**
 * The whole ingredient catalog, loaded once: AI recipes and corrections name ingredients by id,
 * and the editor needs their parents, units and calories.
 */
class IngredientCatalog(private val api: SizzleApi) {
    private val mutex = Mutex()
    @Volatile private var byId: Map<String, IngredientChoice> = emptyMap()

    /** Loads the catalog unless it is already loaded; a failure leaves it empty for the next try. */
    suspend fun ensureLoaded(force: Boolean = false) = mutex.withLock {
        if (byId.isNotEmpty() && !force) return@withLock
        runApi { api.getIngredients(search = null, pageIndex = 0, pageSize = CATALOG_SIZE) }
            .onSuccess { tree -> byId = flatten(tree).associateBy { it.id } }
    }

    fun find(id: String?): IngredientChoice? = id?.let { byId[it] }

    /** Calories of [quantity] of an ingredient: stored per 100 g / 100 ml, or per piece. */
    fun caloriesOf(id: String, quantity: Int): Double? {
        val ingredient = find(id) ?: return null
        if (ingredient.calories <= 0 || quantity <= 0) return null
        return if (IngredientUnit.parse(ingredient.unit) == IngredientUnit.Pieces) {
            quantity.toDouble() * ingredient.calories
        } else {
            quantity.toDouble() * ingredient.calories / 100
        }
    }

    companion object {
        /** Large enough for the whole catalog (paging counts parents and variants together). */
        private const val CATALOG_SIZE = 10000

        fun flatten(tree: List<Ingredient>): List<IngredientChoice> = tree.flatMap { parent ->
            listOf(IngredientChoice(parent.id, parent.name, parent.unit, null, null, parent.calories)) +
                parent.children.map { IngredientChoice(it.id, it.name, it.unit, parent.name, parent.id, it.calories) }
        }
    }
}
