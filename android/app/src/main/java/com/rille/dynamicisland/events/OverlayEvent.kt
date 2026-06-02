package com.rille.dynamicisland.events

enum class OverlayEventPriority {
    Critical,
    High,
    Medium,
    Low
}

enum class OverlayEventBehavior {
    Transient,
    Sticky
}

data class OverlayEvent(
    val id: String = "${System.currentTimeMillis()}-${(0..9999).random()}",
    val title: String,
    val subtitle: String,
    val accentSeed: Long,
    val source: String,
    val durationMs: Long = 4500L,
    val priority: OverlayEventPriority = OverlayEventPriority.Medium,
    val behavior: OverlayEventBehavior = OverlayEventBehavior.Transient,
    val createdAt: Long = System.currentTimeMillis()
)
