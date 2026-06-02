package com.rille.dynamicisland.events

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.BatteryManager
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow

class BatteryEventSource(private val context: Context) {
    fun events(): Flow<OverlayEvent> = callbackFlow {
        var lastChargingState: Boolean? = null

        val receiver = object : BroadcastReceiver() {
            override fun onReceive(context: Context?, intent: Intent?) {
                val status = intent?.getIntExtra(BatteryManager.EXTRA_STATUS, -1) ?: -1
                val level = intent?.getIntExtra(BatteryManager.EXTRA_LEVEL, -1) ?: -1
                val scale = intent?.getIntExtra(BatteryManager.EXTRA_SCALE, -1) ?: -1
                val percent = if (level >= 0 && scale > 0) (level * 100) / scale else -1
                val charging = status == BatteryManager.BATTERY_STATUS_CHARGING ||
                    status == BatteryManager.BATTERY_STATUS_FULL

                if (lastChargingState == null) {
                    lastChargingState = charging
                    return
                }

                when {
                    charging && lastChargingState == false -> {
                        trySend(
                            OverlayEvent(
                                title = "Charging started",
                                subtitle = if (percent >= 0) "$percent% battery" else "Power connected",
                                accentSeed = 0xFF63D471,
                                source = "Battery",
                                priority = OverlayEventPriority.Low
                            )
                        )
                    }
                    !charging && lastChargingState == true -> {
                        trySend(
                            OverlayEvent(
                                title = "Charging stopped",
                                subtitle = if (percent >= 0) "$percent% battery remaining" else "Power disconnected",
                                accentSeed = 0xFFE0A946,
                                source = "Battery",
                                priority = OverlayEventPriority.Low
                            )
                        )
                    }
                    !charging && percent in 1..15 -> {
                        trySend(
                            OverlayEvent(
                                title = "Low battery",
                                subtitle = "$percent% remaining",
                                accentSeed = 0xFFFF6B6B,
                                source = "Battery",
                                durationMs = 5500L,
                                priority = OverlayEventPriority.High
                            )
                        )
                    }
                }

                lastChargingState = charging
            }
        }

        val filter = IntentFilter(Intent.ACTION_BATTERY_CHANGED)
        context.registerReceiver(receiver, filter)

        awaitClose {
            context.unregisterReceiver(receiver)
        }
    }
}
