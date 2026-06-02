package com.rille.dynamicisland.media

import android.content.ComponentName
import android.content.Context
import android.media.MediaMetadata
import android.media.session.MediaController
import android.media.session.MediaSessionManager
import android.media.session.PlaybackState
import androidx.core.graphics.ColorUtils
import androidx.palette.graphics.Palette
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.util.concurrent.TimeUnit

class MediaSessionRepository(
    private val context: Context,
    private val notificationListener: ComponentName,
    private val dispatcher: CoroutineDispatcher = Dispatchers.Main
) {
    private val sessionManager =
        context.getSystemService(MediaSessionManager::class.java)

    fun playerSnapshots(): Flow<PlayerSnapshot?> = callbackFlow {
        if (!hasNotificationAccess(context, notificationListener)) {
            trySend(null)
            awaitClose { }
            return@callbackFlow
        }

        var activeController: MediaController? = null
        var tickerJob: Job? = null

        fun restartTicker() {
            tickerJob?.cancel()

            if (activeController?.playbackState?.state != PlaybackState.STATE_PLAYING) {
                return
            }

            tickerJob = launch(dispatcher) {
                while (isActive) {
                    trySend(buildSnapshot(activeController))
                    delay(1000)
                }
            }
        }

        val controllerCallback = object : MediaController.Callback() {
            override fun onPlaybackStateChanged(state: PlaybackState?) {
                trySend(buildSnapshot(activeController))
                restartTicker()
            }

            override fun onMetadataChanged(metadata: MediaMetadata?) {
                trySend(buildSnapshot(activeController))
                restartTicker()
            }

            override fun onSessionDestroyed() {
                trySend(buildSnapshot(selectPreferredController()))
                restartTicker()
            }
        }

        fun updateController() {
            val nextController = selectPreferredController()
            if (nextController?.sessionToken != activeController?.sessionToken) {
                activeController?.unregisterCallback(controllerCallback)
                activeController = nextController
                activeController?.registerCallback(controllerCallback)
            }
            trySend(buildSnapshot(activeController))
            restartTicker()
        }

        val sessionsChangedListener =
            MediaSessionManager.OnActiveSessionsChangedListener {
                launch(dispatcher) {
                    updateController()
                }
            }

        sessionManager.addOnActiveSessionsChangedListener(
            sessionsChangedListener,
            notificationListener
        )

        updateController()

        awaitClose {
            tickerJob?.cancel()
            activeController?.unregisterCallback(controllerCallback)
            sessionManager.removeOnActiveSessionsChangedListener(sessionsChangedListener)
        }
    }

    fun playPause() {
        val controller = selectPreferredController() ?: return
        val state = controller.playbackState?.state
        val controls = controller.transportControls
        if (state == PlaybackState.STATE_PLAYING) {
            controls.pause()
        } else {
            controls.play()
        }
    }

    fun skipNext() {
        selectPreferredController()?.transportControls?.skipToNext()
    }

    fun skipPrevious() {
        selectPreferredController()?.transportControls?.skipToPrevious()
    }

    private fun selectPreferredController(): MediaController? {
        val sessions = sessionManager.getActiveSessions(notificationListener)
        if (sessions.isNullOrEmpty()) {
            return null
        }

        return sessions.firstOrNull { controller ->
            controller.playbackState?.state == PlaybackState.STATE_PLAYING
        } ?: sessions.firstOrNull { controller ->
            controller.metadata?.description?.title != null
        } ?: sessions.first()
    }

    private fun buildSnapshot(controller: MediaController?): PlayerSnapshot? {
        controller ?: return null

        val metadata = controller.metadata
        val description = metadata?.description
        val playbackState = controller.playbackState

        val title = description?.title?.toString()?.takeIf { it.isNotBlank() } ?: "Nothing playing"
        val artist = sequenceOf(
            description?.subtitle?.toString(),
            metadata?.getString(MediaMetadata.METADATA_KEY_ARTIST),
            metadata?.getString(MediaMetadata.METADATA_KEY_ALBUM_ARTIST),
            metadata?.getString(MediaMetadata.METADATA_KEY_ALBUM)
        ).firstOrNull { !it.isNullOrBlank() } ?: "Open a player app"

        val source = controller.packageName
            ?.substringAfterLast('.')
            ?.replaceFirstChar { if (it.isLowerCase()) it.titlecase() else it.toString() }
            ?: "Android"

        val duration = metadata?.getLong(MediaMetadata.METADATA_KEY_DURATION)?.takeIf { it > 0L } ?: 0L
        val statePosition = playbackState?.position?.takeIf { it >= 0L } ?: 0L
        val lastUpdate = playbackState?.lastPositionUpdateTime ?: 0L
        val speed = playbackState?.playbackSpeed ?: 1f
        val now = System.currentTimeMillis()

        val effectivePosition = when {
            playbackState?.state == PlaybackState.STATE_PLAYING && lastUpdate > 0L -> {
                val delta = now - lastUpdate
                (statePosition + (delta * speed).toLong()).coerceAtLeast(0L)
            }
            else -> statePosition
        }
        val clampedPosition = if (duration > 0) effectivePosition.coerceAtMost(duration) else effectivePosition

        val artwork = description?.iconBitmap ?: metadata?.getBitmap(MediaMetadata.METADATA_KEY_ALBUM_ART)
        val accentSeed = extractAccentColor(artwork)
        val actions = playbackState?.actions ?: 0L

        return PlayerSnapshot(
            track = TrackUiModel(
                title = title,
                artist = artist,
                source = source,
                progressLabel = formatDuration(clampedPosition),
                remainingLabel = if (duration > 0) "-${formatDuration((duration - clampedPosition).coerceAtLeast(0L))}" else "--:--",
                progress = if (duration > 0) (clampedPosition.toFloat() / duration.toFloat()).coerceIn(0f, 1f) else 0f,
                accentSeed = accentSeed,
                artwork = artwork
            ),
            isPlaying = playbackState?.state == PlaybackState.STATE_PLAYING,
            canPlayPause = actions and
                (PlaybackState.ACTION_PLAY or PlaybackState.ACTION_PAUSE or PlaybackState.ACTION_PLAY_PAUSE) != 0L,
            canSkipNext = actions and PlaybackState.ACTION_SKIP_TO_NEXT != 0L,
            canSkipPrevious = actions and PlaybackState.ACTION_SKIP_TO_PREVIOUS != 0L
        )
    }

    private fun extractAccentColor(bitmap: android.graphics.Bitmap?): Long {
        bitmap ?: return 0xFF8E5DFF

        return runCatching {
            val palette = Palette.from(bitmap).clearFilters().generate()
            val swatch = palette.vibrantSwatch ?: palette.dominantSwatch
            val baseColor = swatch?.rgb ?: 0xFF8E5DFF.toInt()
            ColorUtils.blendARGB(baseColor, 0xFF8E5DFF.toInt(), 0.22f).toLong() and 0xFFFFFFFFL
        }.getOrElse {
            0xFF8E5DFF
        }
    }

    private fun formatDuration(valueMs: Long): String {
        val totalSeconds = TimeUnit.MILLISECONDS.toSeconds(valueMs).coerceAtLeast(0L)
        val minutes = totalSeconds / 60
        val seconds = totalSeconds % 60
        return "%d:%02d".format(minutes, seconds)
    }
}
