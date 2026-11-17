package com.sizzle.app.data

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonElement

/**
 * Parses JSON that is still being streamed, e.g. `{"recipe":{"name":"Whipped Co`.
 * Open strings, arrays and objects are closed; a dangling key, `:` or `,` is cut off.
 * Returns null when not even a partial value can be read yet.
 */
object PartialJson {
    private val json = Json { isLenient = false }
    private val fence = Regex("^```(?:json)?\\s*", RegexOption.IGNORE_CASE)
    private val cutUnicode = Regex("\\\\u[0-9a-fA-F]{0,3}$")
    private val cutEscape = Regex("(^|[^\\\\])(\\\\\\\\)*\\\\$")

    fun parse(text: String): JsonElement? {
        val source = text.trim().replace(fence, "")
        if (source.isEmpty()) return null
        tryClose(source, source.length)?.let { return it }
        // Fall back to the last place a value ended or a container opened.
        val cuts = cutPoints(source)
        for (i in cuts.indices.reversed()) {
            tryClose(source, cuts[i])?.let { return it }
        }
        return null
    }

    /** Positions outside strings where the text can be cut and still close into valid JSON. */
    private fun cutPoints(text: String): List<Int> {
        val cuts = mutableListOf<Int>()
        var inString = false
        var i = 0
        while (i < text.length) {
            val c = text[i]
            if (inString) {
                if (c == '\\') i++ else if (c == '"') inString = false
            } else when (c) {
                '"' -> inString = true
                '{', '[', '}', ']' -> cuts += i + 1
                ',' -> cuts += i
            }
            i++
        }
        return cuts
    }

    private fun tryClose(text: String, end: Int): JsonElement? {
        var prefix = text.substring(0, end)
        val closers = ArrayDeque<Char>()
        var inString = false
        var i = 0
        while (i < prefix.length) {
            val c = prefix[i]
            if (inString) {
                if (c == '\\') i++ else if (c == '"') inString = false
            } else when (c) {
                '"' -> inString = true
                '{' -> closers.addLast('}')
                '[' -> closers.addLast(']')
                '}', ']' -> closers.removeLastOrNull()
            }
            i++
        }
        if (inString) {
            // A string cut inside an escape sequence would not parse once closed.
            prefix = prefix.replace(cutUnicode, "").replace(cutEscape, "$1$2") + '"'
        }
        val closed = prefix + closers.reversed().joinToString("")
        return runCatching { json.parseToJsonElement(closed) }.getOrNull()
    }
}
