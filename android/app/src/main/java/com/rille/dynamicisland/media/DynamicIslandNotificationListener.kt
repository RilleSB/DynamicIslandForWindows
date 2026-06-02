package com.rille.dynamicisland.media

import android.service.notification.StatusBarNotification
import android.service.notification.NotificationListenerService
import com.rille.dynamicisland.events.OverlayEventBehavior
import com.rille.dynamicisland.events.OverlayEventPriority
import com.rille.dynamicisland.events.OverlayEvent
import com.rille.dynamicisland.events.OverlayEventBus

class DynamicIslandNotificationListener : NotificationListenerService() {
    override fun onNotificationPosted(sbn: StatusBarNotification?) {
        sbn ?: return
        if (sbn.packageName == packageName) return

        val extras = sbn.notification.extras
        val title = extras?.getCharSequence("android.title")?.toString()?.trim().orEmpty()
        val text = extras?.getCharSequence("android.text")?.toString()?.trim().orEmpty()
        val bigText = extras?.getCharSequence("android.bigText")?.toString()?.trim().orEmpty()

        val resolvedTitle = when {
            title.isNotBlank() -> title
            text.isNotBlank() -> text
            else -> return
        }

        val appName = sbn.packageName.substringAfterLast('.').replaceFirstChar {
            if (it.isLowerCase()) it.titlecase() else it.toString()
        }

        val category = sbn.notification.category.orEmpty()
        val bundleText = "$resolvedTitle $text $bigText ${sbn.packageName}".lowercase()
        val isCall = category == android.app.Notification.CATEGORY_CALL ||
            bundleText.contains("incoming call") ||
            bundleText.contains("call") && (
                sbn.packageName.contains("dialer", ignoreCase = true) ||
                    sbn.packageName.contains("phone", ignoreCase = true) ||
                    sbn.packageName.contains("call", ignoreCase = true)
                )
        val isAlarm = category == android.app.Notification.CATEGORY_ALARM ||
            bundleText.contains("alarm") ||
            sbn.packageName.contains("clock", ignoreCase = true)
        val isTimer = bundleText.contains("timer") ||
            bundleText.contains("stopwatch") ||
            bundleText.contains("time is up")

        val isImportantSystemEvent = isCall || isAlarm || isTimer
        if (!sbn.isClearable && !isImportantSystemEvent) return

        val priority = when {
            isCall -> OverlayEventPriority.Critical
            isAlarm || isTimer -> OverlayEventPriority.High
            else -> OverlayEventPriority.Medium
        }

        val accent = when {
            isCall -> 0xFF6AE3FF
            isAlarm -> 0xFFFF7A59
            isTimer -> 0xFFFFC857
            else -> 0xFF7DA8FF
        }

        val source = when {
            isCall -> "Call"
            isAlarm -> "Alarm"
            isTimer -> "Timer"
            else -> appName
        }

        val resolvedSubtitle = when {
            text.isNotBlank() && text != resolvedTitle -> text
            bigText.isNotBlank() && bigText != resolvedTitle -> bigText
            else -> appName
        }

        OverlayEventBus.publish(
            OverlayEvent(
                id = sbn.key,
                title = resolvedTitle.take(64),
                subtitle = resolvedSubtitle.take(96),
                accentSeed = accent,
                source = source,
                durationMs = when {
                    isCall -> 8500L
                    isAlarm || isTimer -> 6500L
                    else -> 4200L
                },
                priority = priority,
                behavior = if (isImportantSystemEvent) OverlayEventBehavior.Sticky else OverlayEventBehavior.Transient
            )
        )
    }

    override fun onNotificationRemoved(sbn: StatusBarNotification?) {
        sbn ?: return
        OverlayEventBus.dismiss(sbn.key)
    }
}
