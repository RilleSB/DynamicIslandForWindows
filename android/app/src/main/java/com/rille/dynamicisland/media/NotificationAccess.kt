package com.rille.dynamicisland.media

import android.content.ComponentName
import android.content.Context
import android.provider.Settings

fun hasNotificationAccess(context: Context, component: ComponentName): Boolean {
    val flat = Settings.Secure.getString(
        context.contentResolver,
        "enabled_notification_listeners"
    ).orEmpty()

    return flat.split(':').any { item ->
        ComponentName.unflattenFromString(item) == component
    }
}
