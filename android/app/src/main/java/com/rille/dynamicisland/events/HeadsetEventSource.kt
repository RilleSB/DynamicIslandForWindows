package com.rille.dynamicisland.events

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.media.AudioDeviceInfo
import android.media.AudioManager
import android.os.Build
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow

class HeadsetEventSource(private val context: Context) {
    fun events(): Flow<OverlayEvent> = callbackFlow {
        var initialized = false

        val receiver = object : BroadcastReceiver() {
            override fun onReceive(context: Context?, intent: Intent?) {
                val action = intent?.action ?: return
                val event = when (action) {
                    Intent.ACTION_HEADSET_PLUG -> {
                        val state = intent.getIntExtra("state", -1)
                        when (state) {
                            1 -> pluggedEvent(resolveAudioLabel())
                            0 -> unpluggedEvent()
                            else -> null
                        }
                    }
                    AudioManager.ACTION_AUDIO_BECOMING_NOISY -> unpluggedEvent()
                    else -> null
                }

                if (!initialized) {
                    initialized = true
                    return
                }

                event?.let { trySend(it) }
            }
        }

        val filter = IntentFilter().apply {
            addAction(Intent.ACTION_HEADSET_PLUG)
            addAction(AudioManager.ACTION_AUDIO_BECOMING_NOISY)
        }
        context.registerReceiver(receiver, filter)

        awaitClose {
            context.unregisterReceiver(receiver)
        }
    }

    private fun resolveAudioLabel(): String {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            val audioManager = context.getSystemService(AudioManager::class.java)
            val outputs = audioManager.getDevices(AudioManager.GET_DEVICES_OUTPUTS)
            return when {
                outputs.any { it.type == AudioDeviceInfo.TYPE_BLUETOOTH_A2DP || it.type == AudioDeviceInfo.TYPE_BLUETOOTH_SCO } -> "Bluetooth audio"
                outputs.any { it.type == AudioDeviceInfo.TYPE_WIRED_HEADPHONES || it.type == AudioDeviceInfo.TYPE_WIRED_HEADSET } -> "Wired headphones"
                else -> "Audio device"
            }
        }
        return "Headphones"
    }

    private fun pluggedEvent(label: String) = OverlayEvent(
        title = "Audio connected",
        subtitle = label,
        accentSeed = 0xFF6BB6FF,
        source = "Audio",
        priority = OverlayEventPriority.Medium
    )

    private fun unpluggedEvent() = OverlayEvent(
        title = "Audio disconnected",
        subtitle = "Playback may switch to speakers",
        accentSeed = 0xFFFFB454,
        source = "Audio",
        priority = OverlayEventPriority.Medium
    )
}
