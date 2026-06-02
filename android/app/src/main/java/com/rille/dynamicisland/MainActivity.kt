package com.rille.dynamicisland

import android.content.ComponentName
import android.content.Intent
import android.os.Bundle
import android.provider.Settings
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.tooling.preview.Preview
import androidx.core.content.ContextCompat
import com.rille.dynamicisland.events.OverlayEvent
import com.rille.dynamicisland.events.OverlayEventBus
import com.rille.dynamicisland.media.DynamicIslandNotificationListener
import com.rille.dynamicisland.media.MediaSessionRepository
import com.rille.dynamicisland.media.hasNotificationAccess
import com.rille.dynamicisland.overlay.OverlayPreferences
import com.rille.dynamicisland.overlay.OverlaySettingsSnapshot
import com.rille.dynamicisland.overlay.OverlayService
import com.rille.dynamicisland.overlay.canDrawOverlaysCompat
import com.rille.dynamicisland.ui.DynamicIslandApp
import com.rille.dynamicisland.ui.theme.DynamicIslandTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val preferences = OverlayPreferences(this)

        if (preferences.autoStartInApp && canDrawOverlaysCompat(this)) {
            ContextCompat.startForegroundService(this, Intent(this, OverlayService::class.java))
        }

        setContent {
            DynamicIslandTheme {
                val listenerComponent = remember {
                    ComponentName(this, DynamicIslandNotificationListener::class.java)
                }
                var settingsSnapshot by remember {
                    mutableStateOf(
                        OverlaySettingsSnapshot(
                            lockPosition = preferences.lockPosition,
                            autoStartInApp = preferences.autoStartInApp,
                            autoStartOnBoot = preferences.autoStartOnBoot,
                            overlayScale = preferences.overlayScale,
                            overlayOpacity = preferences.overlayOpacity
                        )
                    )
                }
                val overlayEvent by OverlayEventBus.events.collectAsState(initial = null)
                val repository = remember {
                    MediaSessionRepository(this, listenerComponent)
                }
                val playerSnapshot by repository.playerSnapshots().collectAsState(initial = null)

                DynamicIslandApp(
                    playerSnapshot = playerSnapshot,
                    overlayEvent = overlayEvent,
                    hasNotificationAccess = hasNotificationAccess(this, listenerComponent),
                    hasOverlayPermission = canDrawOverlaysCompat(this),
                    isOverlayRunning = OverlayService.isRunning,
                    settings = settingsSnapshot,
                    onOpenOverlaySettings = {
                        startActivity(Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION))
                    },
                    onStartOverlay = {
                        val intent = Intent(this, OverlayService::class.java)
                        runCatching {
                            ContextCompat.startForegroundService(this, intent)
                        }.onFailure {
                            Toast.makeText(this, "Overlay start failed", Toast.LENGTH_SHORT).show()
                        }
                    },
                    onStopOverlay = {
                        stopService(Intent(this, OverlayService::class.java))
                    },
                    onToggleLockPosition = {
                        preferences.lockPosition = !preferences.lockPosition
                        settingsSnapshot = settingsSnapshot.copy(lockPosition = preferences.lockPosition)
                        refreshOverlaySettings()
                    },
                    onToggleAutoStartInApp = {
                        preferences.autoStartInApp = !preferences.autoStartInApp
                        settingsSnapshot = settingsSnapshot.copy(autoStartInApp = preferences.autoStartInApp)
                        refreshOverlaySettings()
                    },
                    onToggleAutoStartOnBoot = {
                        preferences.autoStartOnBoot = !preferences.autoStartOnBoot
                        settingsSnapshot = settingsSnapshot.copy(autoStartOnBoot = preferences.autoStartOnBoot)
                        refreshOverlaySettings()
                    },
                    onCycleScale = {
                        val next = when (preferences.overlayScale) {
                            in 0.0f..0.9f -> 1.0f
                            in 0.91f..1.0f -> 1.1f
                            in 1.01f..1.1f -> 1.2f
                            else -> 0.9f
                        }
                        preferences.overlayScale = next
                        settingsSnapshot = settingsSnapshot.copy(overlayScale = preferences.overlayScale)
                        refreshOverlaySettings()
                    },
                    onCycleOpacity = {
                        val next = when (preferences.overlayOpacity) {
                            in 0.0f..0.64f -> 0.75f
                            in 0.65f..0.84f -> 0.9f
                            in 0.85f..0.94f -> 1.0f
                            else -> 0.6f
                        }
                        preferences.overlayOpacity = next
                        settingsSnapshot = settingsSnapshot.copy(overlayOpacity = preferences.overlayOpacity)
                        refreshOverlaySettings()
                    },
                    onPlayPause = repository::playPause,
                    onNext = repository::skipNext,
                    onPrevious = repository::skipPrevious
                )
            }
        }
    }

    private fun refreshOverlaySettings() {
        if (!OverlayService.isRunning) return
        startService(
            Intent(this, OverlayService::class.java)
                .setAction(OverlayService.ACTION_REFRESH_SETTINGS)
        )
    }
}

@Preview(showBackground = true, backgroundColor = 0xFF120C1D)
@Composable
private fun AppPreview() {
    DynamicIslandTheme {
        DynamicIslandApp(
            playerSnapshot = null,
            overlayEvent = OverlayEvent(
                title = "Charging started",
                subtitle = "84% battery",
                accentSeed = 0xFF63D471,
                source = "Battery"
            ),
            hasNotificationAccess = false,
            hasOverlayPermission = false,
            isOverlayRunning = false,
            settings = OverlaySettingsSnapshot(
                lockPosition = false,
                autoStartInApp = false,
                autoStartOnBoot = false,
                overlayScale = 1f,
                overlayOpacity = 1f
            ),
            onOpenOverlaySettings = {},
            onStartOverlay = {},
            onStopOverlay = {},
            onToggleLockPosition = {},
            onToggleAutoStartInApp = {},
            onToggleAutoStartOnBoot = {},
            onCycleScale = {},
            onCycleOpacity = {},
            onPlayPause = {},
            onNext = {},
            onPrevious = {}
        )
    }
}
