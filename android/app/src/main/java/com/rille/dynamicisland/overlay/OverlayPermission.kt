package com.rille.dynamicisland.overlay

import android.content.Context
import android.os.Build
import android.provider.Settings

fun canDrawOverlaysCompat(context: Context): Boolean {
    return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
        Settings.canDrawOverlays(context)
    } else {
        true
    }
}
