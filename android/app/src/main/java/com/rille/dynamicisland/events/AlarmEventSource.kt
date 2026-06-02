package com.rille.dynamicisland.events

import android.app.AlarmManager
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import kotlinx.coroutines.channels.awaitClose

class AlarmEventSource(private val context: Context) {
    fun events() = kotlinx.coroutines.flow.callbackFlow {
        var lastTrigger: Long? = null

        fun currentEvent(): OverlayEvent? {
            val alarmManager = context.getSystemService(AlarmManager::class.java)
            val nextAlarm = alarmManager?.nextAlarmClock
            val triggerTime = nextAlarm?.triggerTime ?: return null
            val formatter = SimpleDateFormat("HH:mm", Locale.getDefault())
            return OverlayEvent(
                id = "alarm-$triggerTime",
                title = "Next alarm updated",
                subtitle = "Scheduled for ${formatter.format(Date(triggerTime))}",
                accentSeed = 0xFFFF7A59,
                source = "Alarm",
                durationMs = 6000L,
                priority = OverlayEventPriority.High
            )
        }

        val receiver = object : BroadcastReceiver() {
            override fun onReceive(context: Context?, intent: Intent?) {
                val alarmManager = this@AlarmEventSource.context.getSystemService(AlarmManager::class.java)
                val nextTrigger = alarmManager?.nextAlarmClock?.triggerTime

                if (lastTrigger == nextTrigger) {
                    return
                }

                if (nextTrigger == null && lastTrigger != null) {
                    trySend(
                        OverlayEvent(
                            id = "alarm-cleared",
                            title = "Alarm cleared",
                            subtitle = "No next alarm is scheduled",
                            accentSeed = 0xFFB18CFF,
                            source = "Alarm",
                            durationMs = 4500L,
                            priority = OverlayEventPriority.Medium
                        )
                    )
                } else {
                    currentEvent()?.let { trySend(it) }
                }

                lastTrigger = nextTrigger
            }
        }

        context.registerReceiver(receiver, IntentFilter(AlarmManager.ACTION_NEXT_ALARM_CLOCK_CHANGED))

        awaitClose {
            context.unregisterReceiver(receiver)
        }
    }
}
