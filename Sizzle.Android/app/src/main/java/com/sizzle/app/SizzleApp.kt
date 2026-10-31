package com.sizzle.app

import android.app.Application
import coil.ImageLoader
import coil.ImageLoaderFactory
import com.sizzle.app.push.Notifications

class SizzleApp : Application(), ImageLoaderFactory {
    lateinit var container: AppContainer
        private set

    override fun onCreate() {
        super.onCreate()
        container = AppContainer(this)
        Notifications.createChannels(this)
        container.push.start()
    }

    override fun newImageLoader(): ImageLoader = container.imageLoader
}
