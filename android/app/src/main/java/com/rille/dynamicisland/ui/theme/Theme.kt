package com.rille.dynamicisland.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable

private val IslandColorScheme = darkColorScheme(
    primary = IslandTextPrimary,
    onPrimary = IslandBlack,
    secondary = IslandTextSecondary,
    background = IslandBlack,
    surface = IslandBlack
)

@Composable
fun DynamicIslandTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = IslandColorScheme,
        typography = Typography,
        content = content
    )
}
