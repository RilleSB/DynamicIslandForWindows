package com.rille.dynamicisland.overlay

data class OverlaySettingsSnapshot(
    val lockPosition: Boolean,
    val autoStartInApp: Boolean,
    val autoStartOnBoot: Boolean,
    val overlayScale: Float,
    val overlayOpacity: Float
)
