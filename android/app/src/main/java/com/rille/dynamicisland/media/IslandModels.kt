package com.rille.dynamicisland.media

import android.graphics.Bitmap

enum class IslandMode {
    Minimal,
    Compact,
    Expanded
}

data class TrackUiModel(
    val title: String,
    val artist: String,
    val source: String,
    val progressLabel: String,
    val remainingLabel: String,
    val progress: Float,
    val accentSeed: Long,
    val artwork: Bitmap? = null
)

data class PlayerSnapshot(
    val track: TrackUiModel,
    val isPlaying: Boolean,
    val canPlayPause: Boolean,
    val canSkipNext: Boolean,
    val canSkipPrevious: Boolean
)
