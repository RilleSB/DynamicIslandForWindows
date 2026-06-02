package com.rille.dynamicisland.overlay

import android.content.Context
import com.rille.dynamicisland.media.IslandMode

class OverlayPreferences(context: Context) {
    private val prefs = context.getSharedPreferences("dynamic_island_overlay", Context.MODE_PRIVATE)

    var offsetX: Int
        get() = prefs.getInt(KEY_OFFSET_X, Int.MIN_VALUE)
        set(value) = prefs.edit().putInt(KEY_OFFSET_X, value).apply()

    var offsetY: Int
        get() = prefs.getInt(KEY_OFFSET_Y, Int.MIN_VALUE)
        set(value) = prefs.edit().putInt(KEY_OFFSET_Y, value).apply()

    var mode: IslandMode
        get() = runCatching { IslandMode.valueOf(prefs.getString(KEY_MODE, IslandMode.Compact.name).orEmpty()) }
            .getOrDefault(IslandMode.Compact)
        set(value) = prefs.edit().putString(KEY_MODE, value.name).apply()

    var lockPosition: Boolean
        get() = prefs.getBoolean(KEY_LOCK_POSITION, false)
        set(value) = prefs.edit().putBoolean(KEY_LOCK_POSITION, value).apply()

    var autoStartInApp: Boolean
        get() = prefs.getBoolean(KEY_AUTO_START_IN_APP, false)
        set(value) = prefs.edit().putBoolean(KEY_AUTO_START_IN_APP, value).apply()

    var autoStartOnBoot: Boolean
        get() = prefs.getBoolean(KEY_AUTO_START_ON_BOOT, false)
        set(value) = prefs.edit().putBoolean(KEY_AUTO_START_ON_BOOT, value).apply()

    var overlayScale: Float
        get() = prefs.getFloat(KEY_OVERLAY_SCALE, 1f)
        set(value) = prefs.edit().putFloat(KEY_OVERLAY_SCALE, value.coerceIn(0.85f, 1.2f)).apply()

    var overlayOpacity: Float
        get() = prefs.getFloat(KEY_OVERLAY_OPACITY, 1f)
        set(value) = prefs.edit().putFloat(KEY_OVERLAY_OPACITY, value.coerceIn(0.55f, 1f)).apply()

    companion object {
        private const val KEY_OFFSET_X = "offset_x"
        private const val KEY_OFFSET_Y = "offset_y"
        private const val KEY_MODE = "mode"
        private const val KEY_LOCK_POSITION = "lock_position"
        private const val KEY_AUTO_START_IN_APP = "auto_start_in_app"
        private const val KEY_AUTO_START_ON_BOOT = "auto_start_on_boot"
        private const val KEY_OVERLAY_SCALE = "overlay_scale"
        private const val KEY_OVERLAY_OPACITY = "overlay_opacity"
    }
}
