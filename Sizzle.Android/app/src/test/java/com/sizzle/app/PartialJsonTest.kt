package com.sizzle.app

import com.sizzle.app.data.PartialJson
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class PartialJsonTest {
    private fun parsed(text: String) = PartialJson.parse(text)
    private fun json(text: String) = Json.parseToJsonElement(text)

    @Test
    fun parsesCompleteJson() = assertEquals(json("""{"a":[1,2],"b":"x"}"""), parsed("""{"a":[1,2],"b":"x"}"""))

    @Test
    fun closesStringCutMidValue() =
        assertEquals(json("""{"recipe":{"name":"Whipped Co"}}"""), parsed("""{"recipe":{"name":"Whipped Co"""))

    @Test
    fun dropsDanglingKey() =
        assertEquals(json("""{"recipe":{"name":"Soup"}}"""), parsed("""{"recipe":{"name":"Soup","descr"""))

    @Test
    fun dropsKeyWithoutValue() =
        assertEquals(json("""{"name":"Soup"}"""), parsed("""{"name":"Soup","cookingTimeMinutes":"""))

    @Test
    fun keepsFinishedArrayItems() =
        assertEquals(json("""{"steps":[{"name":"Boil"},{}]}"""), parsed("""{"steps":[{"name":"Boil"},{"na"""))

    @Test
    fun handlesCutEscapes() {
        assertEquals(json("""{"message":"Say "}"""), parsed("""{"message":"Say \"""))
        assertEquals(json("""{"message":"Say "}"""), parsed("""{"message":"Say \u00"""))
        assertEquals(json("""{"message":"a \"b\" c"}"""), parsed("""{"message":"a \"b\" c"""))
    }

    @Test
    fun ignoresFenceAndEmptyInput() {
        assertEquals(json("""{"a":1}"""), parsed("```json\n{\"a\":1"))
        assertNull(parsed(""))
        assertNull(parsed("   "))
    }
}
