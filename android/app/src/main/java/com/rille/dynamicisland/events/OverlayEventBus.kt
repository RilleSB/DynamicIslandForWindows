package com.rille.dynamicisland.events

import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow

object OverlayEventBus {
    private val _events = MutableSharedFlow<OverlayEvent>(
        replay = 0,
        extraBufferCapacity = 16
    )
    private val _dismissals = MutableSharedFlow<String>(
        replay = 0,
        extraBufferCapacity = 16
    )

    val events: SharedFlow<OverlayEvent> = _events.asSharedFlow()
    val dismissals: SharedFlow<String> = _dismissals.asSharedFlow()

    fun publish(event: OverlayEvent) {
        _events.tryEmit(event)
    }

    fun dismiss(id: String) {
        _dismissals.tryEmit(id)
    }
}
