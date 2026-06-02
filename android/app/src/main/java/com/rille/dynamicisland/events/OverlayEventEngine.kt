package com.rille.dynamicisland.events

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class OverlayEventEngine(
    private val scope: CoroutineScope
) {
    private val queue = mutableListOf<OverlayEvent>()
    private val _activeEvent = MutableStateFlow<OverlayEvent?>(null)
    private var activeJob: Job? = null

    val activeEvent: StateFlow<OverlayEvent?> = _activeEvent.asStateFlow()

    fun submit(event: OverlayEvent) {
        val current = _activeEvent.value

        if (current == null) {
            activate(event)
            return
        }

        if (event.priority.ordinal <= current.priority.ordinal) {
            queue.add(event)
            queue.sortWith(
                compareBy<OverlayEvent> { it.priority.ordinal }
                    .thenBy { it.createdAt }
            )
            activate(event)
            return
        }

        queue.add(event)
        queue.sortWith(
            compareBy<OverlayEvent> { it.priority.ordinal }
                .thenBy { it.createdAt }
        )
    }

    fun clearCurrent() {
        activeJob?.cancel()
        _activeEvent.value = null
        promoteNext()
    }

    fun dismiss(id: String) {
        queue.removeAll { it.id == id }

        if (_activeEvent.value?.id == id) {
            activeJob?.cancel()
            _activeEvent.value = null
            promoteNext()
        }
    }

    private fun activate(event: OverlayEvent) {
        activeJob?.cancel()
        queue.removeAll { it.id == event.id }
        _activeEvent.value = event

        if (event.behavior == OverlayEventBehavior.Transient) {
            activeJob = scope.launch {
                delay(event.durationMs)
                if (_activeEvent.value?.id == event.id) {
                    _activeEvent.value = null
                    promoteNext()
                }
            }
        }
    }

    private fun promoteNext() {
        val next = queue.firstOrNull() ?: return
        queue.remove(next)
        activate(next)
    }
}
