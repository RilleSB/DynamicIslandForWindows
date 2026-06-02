package com.rille.dynamicisland.overlay

import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.ViewModelStore
import androidx.lifecycle.ViewModelStoreOwner
import androidx.savedstate.SavedStateRegistry
import androidx.savedstate.SavedStateRegistryController
import androidx.savedstate.SavedStateRegistryOwner

class OverlayComposeOwner(
    parentLifecycleOwner: LifecycleOwner
) : SavedStateRegistryOwner, ViewModelStoreOwner, LifecycleOwner {
    private val controller = SavedStateRegistryController.create(this)
    private val store = ViewModelStore()

    override val lifecycle: Lifecycle = parentLifecycleOwner.lifecycle
    override val savedStateRegistry: SavedStateRegistry
        get() = controller.savedStateRegistry
    override val viewModelStore: ViewModelStore
        get() = store

    init {
        controller.performAttach()
        controller.performRestore(null)

        parentLifecycleOwner.lifecycle.addObserver(
            LifecycleEventObserver { _, event ->
                if (event == Lifecycle.Event.ON_DESTROY) {
                    store.clear()
                }
            }
        )
    }
}
