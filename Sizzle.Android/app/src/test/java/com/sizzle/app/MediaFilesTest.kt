package com.sizzle.app

import com.sizzle.app.data.MediaFiles
import com.sizzle.app.data.model.IngredientUnit
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class MediaFilesTest {

    @Test
    fun videoUrlsAreRecognisedByExtension() {
        assertTrue(MediaFiles.isVideoUrl("http://10.0.2.2:8080/uploads/a.mp4"))
        assertTrue(MediaFiles.isVideoUrl("/uploads/clip.MOV?v=2"))
        assertFalse(MediaFiles.isVideoUrl("http://host/uploads/photo.jpg"))
        assertFalse(MediaFiles.isVideoUrl("http://host/uploads/mp4"))
        assertFalse(MediaFiles.isVideoUrl(null))
    }

    @Test
    fun extensionIsLowercasedAndEmptyWithoutDot() {
        assertEquals("jpeg", MediaFiles.extensionOf("IMG_1.JPEG"))
        assertEquals("", MediaFiles.extensionOf("1000012345"))
    }

    @Test
    fun unitsMapFromTheCatalogShortCodes() {
        assertEquals(IngredientUnit.Grams, IngredientUnit.fromShort("g"))
        assertEquals(IngredientUnit.Pieces, IngredientUnit.fromShort("pcs"))
        assertNull(IngredientUnit.fromShort("kg"))
    }
}
