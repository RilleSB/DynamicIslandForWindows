package com.rille.dynamicisland.overlay

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.ComponentName
import android.content.Intent
import android.graphics.PixelFormat
import android.os.Build
import android.os.IBinder
import android.util.DisplayMetrics
import android.view.Gravity
import android.view.WindowManager
import android.widget.Toast
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.platform.ComposeView
import androidx.core.app.NotificationCompat
import androidx.lifecycle.LifecycleService
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.setViewTreeLifecycleOwner
import androidx.lifecycle.setViewTreeViewModelStoreOwner
import androidx.savedstate.setViewTreeSavedStateRegistryOwner
import com.rille.dynamicisland.MainActivity
import com.rille.dynamicisland.events.AlarmEventSource
import com.rille.dynamicisland.events.BatteryEventSource
import com.rille.dynamicisland.events.HeadsetEventSource
import com.rille.dynamicisland.events.OverlayEvent
import com.rille.dynamicisland.events.OverlayEventBus
import com.rille.dynamicisland.events.OverlayEventEngine
import com.rille.dynamicisland.media.DynamicIslandNotificationListener
import com.rille.dynamicisland.media.IslandMode
import com.rille.dynamicisland.media.MediaSessionRepository
import com.rille.dynamicisland.media.PlayerSnapshot
import com.rille.dynamicisland.ui.DynamicIslandOverlay
import com.rille.dynamicisland.ui.theme.DynamicIslandTheme
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class OverlayService : LifecycleService() {
    private lateinit var windowManager: WindowManager
    private var overlayView: ComposeView? = null
    private var overlayLayoutParams: WindowManager.LayoutParams? = null
    private lateinit var repository: MediaSessionRepository
    private lateinit var preferences: OverlayPreferences
    private lateinit var composeOwner: OverlayComposeOwner
    private lateinit var alarmEventSource: AlarmEventSource
    private lateinit var batteryEventSource: BatteryEventSource
    private lateinit var headsetEventSource: HeadsetEventSource
    private lateinit var eventEngine: OverlayEventEngine
    private var relaxModeJob: Job? = null

    private val playerState = MutableStateFlow<PlayerSnapshot?>(null)
    private val modeState = MutableStateFlow(IslandMode.Compact)
    private val eventState = MutableStateFlow<OverlayEvent?>(null)
    private val settingsState = MutableStateFlow(
        OverlaySettingsSnapshot(
            lockPosition = false,
            autoStartInApp = false,
            autoStartOnBoot = false,
            overlayScale = 1f,
            overlayOpacity = 1f
        )
    )

    override fun onCreate() {
        super.onCreate()
        isRunning = true
        if (!canDrawOverlaysCompat(this)) {
            stopSelf()
            return
        }
        windowManager = getSystemService(WINDOW_SERVICE) as WindowManager
        preferences = OverlayPreferences(this)
        composeOwner = OverlayComposeOwner(this)
        alarmEventSource = AlarmEventSource(this)
        batteryEventSource = BatteryEventSource(this)
        headsetEventSource = HeadsetEventSource(this)
        eventEngine = OverlayEventEngine(lifecycleScope)
        repository = MediaSessionRepository(
            context = this,
            notificationListener = ComponentName(this, DynamicIslandNotificationListener::class.java)
        )
        modeState.value = preferences.mode
        settingsState.value = currentSettingsSnapshot()

        createNotificationChannel()
        startForeground(NOTIFICATION_ID, buildNotification())
        runCatching { createOverlay() }.onFailure {
            Toast.makeText(this, "Overlay start failed", Toast.LENGTH_SHORT).show()
            stopSelf()
            return
        }

        lifecycleScope.launch {
            repository.playerSnapshots().collect { snapshot ->
                playerState.value = snapshot
                scheduleModeRelaxation(snapshot)
            }
        }

        lifecycleScope.launch {
            eventEngine.activeEvent.collect { event -> eventState.value = event }
        }

        lifecycleScope.launch {
            headsetEventSource.events().collect { event ->
                eventEngine.submit(event)
            }
        }

        lifecycleScope.launch {
            alarmEventSource.events().collect { event ->
                eventEngine.submit(event)
            }
        }

        lifecycleScope.launch {
            OverlayEventBus.events.collect { event ->
                eventEngine.submit(event)
            }
        }

        lifecycleScope.launch {
            OverlayEventBus.dismissals.collect { id ->
                eventEngine.dismiss(id)
            }
        }

        lifecycleScope.launch {
            batteryEventSource.events().collect { event ->
                eventEngine.submit(event)
            }
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> stopSelf()
            ACTION_REFRESH_SETTINGS -> {
                settingsState.value = currentSettingsSnapshot()
                preferences.mode = modeState.value
            }
            ACTION_TOGGLE_MODE -> {
                modeState.value = nextMode(modeState.value)
                preferences.mode = modeState.value
                scheduleModeRelaxation(playerState.value)
            }
        }
        return Service.START_STICKY
    }

    private fun nextMode(current: IslandMode): IslandMode {
        return when (current) {
            IslandMode.Minimal -> IslandMode.Compact
            IslandMode.Compact -> IslandMode.Expanded
            IslandMode.Expanded -> IslandMode.Minimal
        }
    }

    private fun scheduleModeRelaxation(snapshot: PlayerSnapshot?) {
        relaxModeJob?.cancel()
        relaxModeJob = lifecycleScope.launch {
            val delayMs = when {
                snapshot == null -> 4000L
                modeState.value == IslandMode.Expanded -> 6000L
                else -> return@launch
            }

            delay(delayMs)

            modeState.value = when {
                snapshot == null -> IslandMode.Minimal
                modeState.value == IslandMode.Expanded -> IslandMode.Compact
                else -> modeState.value
            }
            preferences.mode = modeState.value
        }
    }

    private fun currentSettingsSnapshot(): OverlaySettingsSnapshot {
        return OverlaySettingsSnapshot(
            lockPosition = preferences.lockPosition,
            autoStartInApp = preferences.autoStartInApp,
            autoStartOnBoot = preferences.autoStartOnBoot,
            overlayScale = preferences.overlayScale,
            overlayOpacity = preferences.overlayOpacity
        )
    }

    private fun nudgePosition(deltaX: Int, deltaY: Int) {
        if (settingsState.value.lockPosition) return
        val params = overlayLayoutParams ?: return
        val metrics = currentDisplayMetrics()
        val view = overlayView ?: return

        params.x = (params.x + deltaX).coerceIn(0, (metrics.widthPixels - view.width).coerceAtLeast(0))
        params.y = (params.y + deltaY).coerceIn(0, (metrics.heightPixels - view.height).coerceAtLeast(0))
        windowManager.updateViewLayout(view, params)
        preferences.offsetX = params.x
        preferences.offsetY = params.y
    }

    private fun persistOverlayPosition() {
        overlayLayoutParams?.let { params ->
            preferences.offsetX = params.x
            preferences.offsetY = params.y
        }
    }

    private fun currentDisplayMetrics(): DisplayMetrics {
        val metrics = DisplayMetrics()
        @Suppress("DEPRECATION")
        windowManager.defaultDisplay.getMetrics(metrics)
        return metrics
    }

    override fun onDestroy() {
        isRunning = false
        relaxModeJob?.cancel()
        overlayView?.let { windowManager.removeView(it) }
        overlayView = null
        super.onDestroy()
    }

    override fun onBind(intent: Intent): IBinder? {
        return super.onBind(intent)
    }

    private fun createOverlay() {
        if (overlayView != null) return

        val metrics = currentDisplayMetrics()
        val defaultX = ((metrics.widthPixels * 0.5f) - (220.dpToPx())).toInt().coerceAtLeast(0)

        val view = ComposeView(this).apply {
            setViewTreeLifecycleOwner(composeOwner)
            setViewTreeViewModelStoreOwner(composeOwner)
            setViewTreeSavedStateRegistryOwner(composeOwner)
            setContent {
                DynamicIslandTheme {
                    val snapshot by playerState.asStateFlow().collectAsState()
                    val mode by modeState.asStateFlow().collectAsState()
                    val event by eventState.asStateFlow().collectAsState()
                    val settings by settingsState.asStateFlow().collectAsState()

                    DynamicIslandOverlay(
                        playerSnapshot = snapshot,
                        overlayEvent = event,
                        mode = mode,
                        settings = settings,
                        onCycleMode = {
                            modeState.value = nextMode(modeState.value)
                            preferences.mode = modeState.value
                            scheduleModeRelaxation(playerState.value)
                        },
                        onToggleLockPosition = {
                            preferences.lockPosition = !preferences.lockPosition
                            settingsState.value = currentSettingsSnapshot()
                        },
                        onDrag = { dx, dy ->
                            nudgePosition(dx.toInt(), dy.toInt())
                        },
                        onDragEnd = {
                            persistOverlayPosition()
                        },
                        onPlayPause = {
                            repository.playPause()
                            scheduleModeRelaxation(playerState.value)
                        },
                        onNext = {
                            repository.skipNext()
                            scheduleModeRelaxation(playerState.value)
                        },
                        onPrevious = {
                            repository.skipPrevious()
                            scheduleModeRelaxation(playerState.value)
                        },
                        onClose = { stopSelf() }
                    )
                }
            }
        }

        val layoutParams = WindowManager.LayoutParams(
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.WRAP_CONTENT,
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY
            } else {
                @Suppress("DEPRECATION")
                WindowManager.LayoutParams.TYPE_PHONE
            },
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or
                WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS or
                WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL,
            PixelFormat.TRANSLUCENT
        ).apply {
            gravity = Gravity.TOP or Gravity.START
            x = if (preferences.offsetX == Int.MIN_VALUE) defaultX else preferences.offsetX
            y = if (preferences.offsetY == Int.MIN_VALUE) 24.dpToPx() else preferences.offsetY
        }

        windowManager.addView(view, layoutParams)
        overlayView = view
        overlayLayoutParams = layoutParams
    }

    private fun buildNotification(): Notification {
        val openIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val stopIntent = PendingIntent.getService(
            this,
            1,
            Intent(this, OverlayService::class.java).setAction(ACTION_STOP),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setContentTitle("Dynamic Island overlay")
            .setContentText("Floating island is active")
            .setOngoing(true)
            .setContentIntent(openIntent)
            .addAction(0, "Stop", stopIntent)
            .build()
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return

        val manager = getSystemService(NotificationManager::class.java)
        val channel = NotificationChannel(
            CHANNEL_ID,
            "Dynamic Island Overlay",
            NotificationManager.IMPORTANCE_LOW
        )
        manager.createNotificationChannel(channel)
    }

    private fun Int.dpToPx(): Int {
        return (this * resources.displayMetrics.density).toInt()
    }
    companion object {
        private const val CHANNEL_ID = "dynamic_island_overlay"
        private const val NOTIFICATION_ID = 1201

        @Volatile
        var isRunning: Boolean = false

        const val ACTION_STOP = "com.rille.dynamicisland.overlay.STOP"
        const val ACTION_REFRESH_SETTINGS = "com.rille.dynamicisland.overlay.REFRESH_SETTINGS"
        const val ACTION_TOGGLE_MODE = "com.rille.dynamicisland.overlay.TOGGLE_MODE"
    }
}
