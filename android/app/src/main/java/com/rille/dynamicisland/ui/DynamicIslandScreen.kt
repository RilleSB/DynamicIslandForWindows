package com.rille.dynamicisland.ui

import android.content.Intent
import android.provider.Settings
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.scaleOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.GraphicEq
import androidx.compose.material.icons.rounded.NotificationsActive
import androidx.compose.material.icons.rounded.Pause
import androidx.compose.material.icons.rounded.PlayArrow
import androidx.compose.material.icons.rounded.SkipNext
import androidx.compose.material.icons.rounded.SkipPrevious
import androidx.compose.material3.Icon
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.blur
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.rille.dynamicisland.events.OverlayEvent
import com.rille.dynamicisland.media.IslandMode
import com.rille.dynamicisland.media.PlayerSnapshot
import com.rille.dynamicisland.media.TrackUiModel
import com.rille.dynamicisland.overlay.OverlaySettingsSnapshot
import com.rille.dynamicisland.ui.theme.IslandAmbientPurple
import com.rille.dynamicisland.ui.theme.IslandBlack
import com.rille.dynamicisland.ui.theme.IslandCardBorder
import com.rille.dynamicisland.ui.theme.IslandMuted
import com.rille.dynamicisland.ui.theme.IslandProgressGlow
import com.rille.dynamicisland.ui.theme.IslandTextPrimary
import com.rille.dynamicisland.ui.theme.IslandTextSecondary

@Composable
fun DynamicIslandApp(
    playerSnapshot: PlayerSnapshot?,
    overlayEvent: OverlayEvent?,
    hasNotificationAccess: Boolean,
    hasOverlayPermission: Boolean,
    isOverlayRunning: Boolean,
    settings: OverlaySettingsSnapshot,
    onOpenOverlaySettings: () -> Unit,
    onStartOverlay: () -> Unit,
    onStopOverlay: () -> Unit,
    onToggleLockPosition: () -> Unit,
    onToggleAutoStartInApp: () -> Unit,
    onToggleAutoStartOnBoot: () -> Unit,
    onCycleScale: () -> Unit,
    onCycleOpacity: () -> Unit,
    onPlayPause: () -> Unit,
    onNext: () -> Unit,
    onPrevious: () -> Unit
) {
    val context = LocalContext.current
    var mode by remember { mutableStateOf(IslandMode.Compact) }

    val fallbackTrack = remember {
        TrackUiModel(
            title = "Waiting for music",
            artist = "Open Spotify, Yandex Music or another player",
            source = "Android",
            progressLabel = "0:00",
            remainingLabel = "--:--",
            progress = 0f,
            accentSeed = 0xFF8E5DFF
        )
    }

    val track = playerSnapshot?.track ?: fallbackTrack
    val isPlaying = playerSnapshot?.isPlaying == true

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(
                brush = Brush.linearGradient(
                    colors = listOf(Color(0xFF180E27), Color(0xFF29133D), Color(0xFF120A1D))
                )
            )
    ) {
        AmbientGlow(track.accentSeed)

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 20.dp, vertical = 32.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            if (!hasNotificationAccess) {
                NotificationAccessCard(
                    onOpenSettings = {
                        context.startActivity(
                            Intent(Settings.ACTION_NOTIFICATION_LISTENER_SETTINGS)
                                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                        )
                    }
                )
                Spacer(modifier = Modifier.height(18.dp))
            }

            OverlayControlsCard(
                hasOverlayPermission = hasOverlayPermission,
                isOverlayRunning = isOverlayRunning,
                settings = settings,
                onOpenOverlaySettings = onOpenOverlaySettings,
                onStartOverlay = onStartOverlay,
                onStopOverlay = onStopOverlay,
                onToggleLockPosition = onToggleLockPosition,
                onToggleAutoStartInApp = onToggleAutoStartInApp,
                onToggleAutoStartOnBoot = onToggleAutoStartOnBoot,
                onCycleScale = onCycleScale,
                onCycleOpacity = onCycleOpacity
            )

            Spacer(modifier = Modifier.height(18.dp))

            overlayEvent?.let { event ->
                EventCard(event = event)
                Spacer(modifier = Modifier.height(18.dp))
            }

            DynamicIslandCard(
                track = track,
                mode = mode,
                isPlaying = isPlaying,
                canPlayPause = playerSnapshot?.canPlayPause == true,
                canSkipNext = playerSnapshot?.canSkipNext == true,
                canSkipPrevious = playerSnapshot?.canSkipPrevious == true,
                onPlayPause = onPlayPause,
                onNext = onNext,
                onPrevious = onPrevious,
                modifier = Modifier.widthIn(max = 520.dp)
            )

            Spacer(modifier = Modifier.height(28.dp))

            ModeControls(
                mode = mode,
                isPlaying = isPlaying,
                onModeChange = { mode = it },
                onPlayPause = onPlayPause,
                onNext = onNext,
                onPrevious = onPrevious
            )
        }
    }
}

@Composable
fun DynamicIslandOverlay(
    playerSnapshot: PlayerSnapshot?,
    overlayEvent: OverlayEvent?,
    mode: IslandMode,
    settings: OverlaySettingsSnapshot,
    onCycleMode: () -> Unit,
    onToggleLockPosition: () -> Unit,
    onDrag: (Float, Float) -> Unit,
    onDragEnd: () -> Unit,
    onPlayPause: () -> Unit,
    onNext: () -> Unit,
    onPrevious: () -> Unit,
    onClose: () -> Unit
) {
    val fallbackTrack = remember {
        TrackUiModel(
            title = "Waiting for music",
            artist = "Open a player app",
            source = "Android",
            progressLabel = "0:00",
            remainingLabel = "--:--",
            progress = 0f,
            accentSeed = 0xFF8E5DFF
        )
    }

    val track = playerSnapshot?.track ?: fallbackTrack
    val isPlaying = playerSnapshot?.isPlaying == true

    Column(
        modifier = Modifier
            .graphicsLayer(
                alpha = settings.overlayOpacity,
                scaleX = settings.overlayScale,
                scaleY = settings.overlayScale
            )
            .pointerInput(settings.lockPosition) {
                if (!settings.lockPosition) {
                    detectDragGestures(
                        onDragEnd = onDragEnd,
                        onDragCancel = onDragEnd
                    ) { change, dragAmount ->
                        change.consume()
                        onDrag(dragAmount.x, dragAmount.y)
                    }
                }
            }
            .padding(top = 18.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        overlayEvent?.let { event ->
            EventCard(event = event)
            Spacer(modifier = Modifier.height(10.dp))
        }
        Box(
            modifier = Modifier.clickable(
                interactionSource = remember { MutableInteractionSource() },
                indication = null,
                onClick = onCycleMode
            )
        ) {
            DynamicIslandCard(
                track = track,
                mode = mode,
                isPlaying = isPlaying,
                canPlayPause = playerSnapshot?.canPlayPause == true,
                canSkipNext = playerSnapshot?.canSkipNext == true,
                canSkipPrevious = playerSnapshot?.canSkipPrevious == true,
                onPlayPause = onPlayPause,
                onNext = onNext,
                onPrevious = onPrevious,
                modifier = Modifier.widthIn(max = 520.dp)
            )
        }

        Spacer(modifier = Modifier.height(10.dp))

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            ActionChip(text = mode.name, selected = true, onClick = onCycleMode)
            ActionChip(
                text = if (settings.lockPosition) "Locked" else "Drag",
                selected = settings.lockPosition,
                onClick = onToggleLockPosition
            )
            ActionChip(
                text = if (isPlaying) "Pause" else "Play",
                selected = isPlaying,
                onClick = onPlayPause
            )
            ActionChip(text = "Close", selected = false, onClick = onClose)
        }
    }
}

@Composable
private fun OverlayControlsCard(
    hasOverlayPermission: Boolean,
    isOverlayRunning: Boolean,
    settings: OverlaySettingsSnapshot,
    onOpenOverlaySettings: () -> Unit,
    onStartOverlay: () -> Unit,
    onStopOverlay: () -> Unit,
    onToggleLockPosition: () -> Unit,
    onToggleAutoStartInApp: () -> Unit,
    onToggleAutoStartOnBoot: () -> Unit,
    onCycleScale: () -> Unit,
    onCycleOpacity: () -> Unit
) {
    Surface(
        shape = RoundedCornerShape(24.dp),
        color = Color.White.copy(alpha = 0.06f),
        border = BorderStroke(1.dp, Color.White.copy(alpha = 0.10f))
    ) {
        Column(modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp)) {
            Text("Floating overlay", color = IslandTextPrimary, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                if (hasOverlayPermission) "Permission granted. You can launch the island over other apps."
                else "Grant overlay permission first, then start the floating island.",
                color = IslandTextSecondary,
                fontSize = 11.sp
            )
            Spacer(modifier = Modifier.height(10.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                ActionChip(
                    text = if (hasOverlayPermission) "Overlay OK" else "Grant",
                    selected = hasOverlayPermission,
                    onClick = onOpenOverlaySettings
                )
                ActionChip(
                    text = if (isOverlayRunning) "Running" else "Start",
                    selected = isOverlayRunning,
                    onClick = onStartOverlay
                )
                ActionChip(text = "Stop", selected = false, onClick = onStopOverlay)
            }
            Spacer(modifier = Modifier.height(8.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                ActionChip(
                    text = if (settings.lockPosition) "Lock On" else "Lock Off",
                    selected = settings.lockPosition,
                    onClick = onToggleLockPosition
                )
                ActionChip(
                    text = if (settings.autoStartInApp) "Auto In-App" else "Manual",
                    selected = settings.autoStartInApp,
                    onClick = onToggleAutoStartInApp
                )
                ActionChip(
                    text = if (settings.autoStartOnBoot) "Boot On" else "Boot Off",
                    selected = settings.autoStartOnBoot,
                    onClick = onToggleAutoStartOnBoot
                )
            }
            Spacer(modifier = Modifier.height(8.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                ActionChip(
                    text = "Scale ${"%.2f".format(settings.overlayScale)}",
                    selected = false,
                    onClick = onCycleScale
                )
                ActionChip(
                    text = "Opacity ${(settings.overlayOpacity * 100).toInt()}%",
                    selected = false,
                    onClick = onCycleOpacity
                )
            }
        }
    }
}

@Composable
private fun EventCard(event: OverlayEvent) {
    Surface(
        shape = RoundedCornerShape(24.dp),
        color = Color.White.copy(alpha = 0.07f),
        border = BorderStroke(1.dp, Color(event.accentSeed).copy(alpha = 0.35f))
    ) {
        Column(modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp)) {
            Text(event.title, color = IslandTextPrimary, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(2.dp))
            Text(event.subtitle, color = IslandTextSecondary, fontSize = 11.sp)
            Spacer(modifier = Modifier.height(4.dp))
            Text(event.source, color = Color(event.accentSeed), fontSize = 10.sp, fontWeight = FontWeight.Medium)
        }
    }
}

@Composable
private fun NotificationAccessCard(onOpenSettings: () -> Unit) {
    Surface(
        shape = RoundedCornerShape(24.dp),
        color = Color.White.copy(alpha = 0.07f),
        border = BorderStroke(1.dp, Color.White.copy(alpha = 0.12f))
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = Icons.Rounded.NotificationsActive,
                contentDescription = null,
                tint = Color.White,
                modifier = Modifier.size(18.dp)
            )
            Spacer(modifier = Modifier.width(10.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text("Enable notification access", color = IslandTextPrimary, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                Text("Needed to read active media sessions on Android.", color = IslandTextSecondary, fontSize = 11.sp)
            }
            Spacer(modifier = Modifier.width(12.dp))
            ActionChip(text = "Open", selected = true, onClick = onOpenSettings)
        }
    }
}

@Composable
private fun AmbientGlow(accentSeed: Long) {
    val accent = Color(accentSeed)
    Box(
        modifier = Modifier.fillMaxSize().drawBehind {
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(accent.copy(alpha = 0.28f), Color.Transparent),
                    center = Offset(size.width * 0.18f, size.height * 0.24f),
                    radius = size.minDimension * 0.36f
                ),
                radius = size.minDimension * 0.36f,
                center = Offset(size.width * 0.18f, size.height * 0.24f)
            )
            drawCircle(
                brush = Brush.radialGradient(
                    colors = listOf(IslandAmbientPurple.copy(alpha = 0.28f), Color.Transparent),
                    center = Offset(size.width * 0.82f, size.height * 0.76f),
                    radius = size.minDimension * 0.44f
                ),
                radius = size.minDimension * 0.44f,
                center = Offset(size.width * 0.82f, size.height * 0.76f)
            )
        }
    )
}

@Composable
fun DynamicIslandCard(
    track: TrackUiModel,
    mode: IslandMode,
    isPlaying: Boolean,
    canPlayPause: Boolean,
    canSkipNext: Boolean,
    canSkipPrevious: Boolean,
    onPlayPause: () -> Unit,
    onNext: () -> Unit,
    onPrevious: () -> Unit,
    modifier: Modifier = Modifier
) {
    val accent = Color(track.accentSeed)
    AnimatedContent(
        targetState = mode,
        transitionSpec = {
            (fadeIn(tween(240)) + scaleIn(initialScale = 0.96f, animationSpec = tween(240))) togetherWith
                (fadeOut(tween(180)) + scaleOut(targetScale = 0.98f, animationSpec = tween(180)))
        },
        label = "island-mode"
    ) { targetMode ->
        Surface(
            modifier = modifier,
            color = Color.Transparent,
            shape = RoundedCornerShape(if (targetMode == IslandMode.Expanded) 34.dp else 100.dp),
            border = BorderStroke(1.dp, IslandCardBorder.copy(alpha = 0.7f))
        ) {
            Box(
                modifier = Modifier
                    .clip(RoundedCornerShape(if (targetMode == IslandMode.Expanded) 34.dp else 100.dp))
                    .background(
                        brush = Brush.verticalGradient(
                            colors = listOf(Color(0xFF201826).copy(alpha = 0.98f), Color(0xFF110C17).copy(alpha = 0.96f))
                        )
                    )
                    .border(
                        width = 1.dp,
                        brush = Brush.verticalGradient(
                            colors = listOf(Color.White.copy(alpha = 0.12f), accent.copy(alpha = 0.10f), Color.Transparent)
                        ),
                        shape = RoundedCornerShape(if (targetMode == IslandMode.Expanded) 34.dp else 100.dp)
                    )
                    .drawBehind {
                        drawRoundRect(
                            brush = Brush.verticalGradient(colors = listOf(Color.White.copy(alpha = 0.08f), Color.Transparent)),
                            size = Size(size.width, size.height * 0.48f),
                            cornerRadius = CornerRadius(80f, 80f)
                        )
                        drawRoundRect(
                            color = accent.copy(alpha = 0.12f),
                            size = size,
                            cornerRadius = CornerRadius(80f, 80f)
                        )
                    }
                    .padding(if (targetMode == IslandMode.Expanded) 18.dp else 10.dp)
            ) {
                when (targetMode) {
                    IslandMode.Minimal -> MinimalIsland(track, accent, isPlaying)
                    IslandMode.Compact -> CompactIsland(track, accent, isPlaying)
                    IslandMode.Expanded -> ExpandedIsland(
                        track = track,
                        accent = accent,
                        isPlaying = isPlaying,
                        canPlayPause = canPlayPause,
                        canSkipNext = canSkipNext,
                        canSkipPrevious = canSkipPrevious,
                        onPlayPause = onPlayPause,
                        onNext = onNext,
                        onPrevious = onPrevious
                    )
                }
            }
        }
    }
}

@Composable
private fun MinimalIsland(track: TrackUiModel, accent: Color, isPlaying: Boolean) {
    Row(
        modifier = Modifier.width(152.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        AlbumArt(track = track, accent = accent, size = 44.dp, pulse = isPlaying)
        PlaybackBars(isPlaying = isPlaying)
        RightOrb(accent = accent)
    }
}

@Composable
private fun CompactIsland(track: TrackUiModel, accent: Color, isPlaying: Boolean) {
    Row(modifier = Modifier.width(352.dp), verticalAlignment = Alignment.CenterVertically) {
        AlbumArt(track = track, accent = accent, size = 50.dp, pulse = isPlaying)
        Spacer(modifier = Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            MarqueeText(
                text = track.title,
                style = TextStyle(fontSize = 16.sp, lineHeight = 18.sp, fontWeight = FontWeight.SemiBold, color = IslandTextPrimary)
            )
            Spacer(modifier = Modifier.height(2.dp))
            Text(track.artist, maxLines = 1, overflow = TextOverflow.Ellipsis, style = TextStyle(fontSize = 12.sp, lineHeight = 14.sp, color = IslandTextSecondary))
            Spacer(modifier = Modifier.height(2.dp))
            Text(track.source, style = TextStyle(fontSize = 10.sp, lineHeight = 12.sp, color = IslandMuted))
        }
        Spacer(modifier = Modifier.width(12.dp))
        PlaybackBars(isPlaying = isPlaying)
        Spacer(modifier = Modifier.width(12.dp))
        RightOrb(accent = accent)
    }
}

@Composable
private fun ExpandedIsland(
    track: TrackUiModel,
    accent: Color,
    isPlaying: Boolean,
    canPlayPause: Boolean,
    canSkipNext: Boolean,
    canSkipPrevious: Boolean,
    onPlayPause: () -> Unit,
    onNext: () -> Unit,
    onPrevious: () -> Unit
) {
    Column(modifier = Modifier.width(500.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            AlbumArt(track = track, accent = accent, size = 74.dp, pulse = isPlaying, rounded = 24.dp)
            Spacer(modifier = Modifier.width(14.dp))
            Column(modifier = Modifier.weight(1f)) {
                MarqueeText(
                    text = track.title,
                    style = TextStyle(fontSize = 19.sp, lineHeight = 21.sp, fontWeight = FontWeight.Bold, color = IslandTextPrimary)
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(track.artist, maxLines = 1, overflow = TextOverflow.Ellipsis, style = TextStyle(fontSize = 14.sp, color = IslandTextSecondary))
                Spacer(modifier = Modifier.height(3.dp))
                Text(track.source, style = TextStyle(fontSize = 11.sp, color = IslandMuted))
            }
            Spacer(modifier = Modifier.width(12.dp))
            Column(horizontalAlignment = Alignment.End) {
                PlaybackBars(isPlaying = isPlaying)
                Spacer(modifier = Modifier.height(10.dp))
                RightOrb(accent = accent, size = 42.dp)
            }
        }

        Spacer(modifier = Modifier.height(14.dp))

        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            Text(track.progressLabel, color = IslandTextSecondary, fontSize = 11.sp)
            Text(track.remainingLabel, color = IslandTextSecondary, fontSize = 11.sp)
        }

        Spacer(modifier = Modifier.height(8.dp))
        ThinProgress(progress = track.progress, accent = accent, active = isPlaying)
        Spacer(modifier = Modifier.height(16.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            SmallGhostButton(label = track.source.take(3).uppercase(), active = true)
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                ControlButton(enabled = canSkipPrevious, filled = false, onClick = onPrevious) {
                    Icon(Icons.Rounded.SkipPrevious, contentDescription = null)
                }
                ControlButton(enabled = canPlayPause, filled = true, onClick = onPlayPause) {
                    Icon(if (isPlaying) Icons.Rounded.Pause else Icons.Rounded.PlayArrow, contentDescription = null)
                }
                ControlButton(enabled = canSkipNext, filled = false, onClick = onNext) {
                    Icon(Icons.Rounded.SkipNext, contentDescription = null)
                }
            }
            SmallGhostButton(label = "NOW", active = isPlaying)
        }
    }
}

@Composable
private fun AlbumArt(track: TrackUiModel, accent: Color, size: Dp, pulse: Boolean, rounded: Dp = 100.dp) {
    val transition = rememberInfiniteTransition(label = "album-pulse")
    val scale by transition.animateFloat(
        initialValue = 1f,
        targetValue = if (pulse) 1.04f else 1f,
        animationSpec = infiniteRepeatable(animation = tween(1800, easing = FastOutSlowInEasing), repeatMode = RepeatMode.Reverse),
        label = "album-pulse-scale"
    )

    Box(
        modifier = Modifier
            .size(size)
            .drawBehind {
                drawCircle(color = accent.copy(alpha = if (pulse) 0.28f else 0.14f), radius = size.toPx() * 0.62f * scale)
            }
            .clip(RoundedCornerShape(rounded))
            .background(
                brush = Brush.linearGradient(
                    colors = listOf(accent.copy(alpha = 0.95f), accent.copy(alpha = 0.48f), Color.White.copy(alpha = 0.18f))
                )
            )
            .border(1.dp, Color.White.copy(alpha = 0.16f), RoundedCornerShape(rounded)),
        contentAlignment = Alignment.Center
    ) {
        if (track.artwork != null) {
            Image(bitmap = track.artwork.asImageBitmap(), contentDescription = null, modifier = Modifier.fillMaxSize())
        } else {
            Icon(Icons.Rounded.GraphicEq, contentDescription = null, tint = Color.White.copy(alpha = 0.9f), modifier = Modifier.size(size * 0.42f))
        }
    }
}

@Composable
private fun PlaybackBars(isPlaying: Boolean) {
    val transition = rememberInfiniteTransition(label = "bars")
    val barOne by transition.animateFloat(0.35f, if (isPlaying) 1f else 0.45f, infiniteRepeatable(tween(420), RepeatMode.Reverse), label = "bar1")
    val barTwo by transition.animateFloat(0.85f, if (isPlaying) 0.4f else 0.75f, infiniteRepeatable(tween(520), RepeatMode.Reverse), label = "bar2")
    val barThree by transition.animateFloat(0.55f, if (isPlaying) 0.95f else 0.55f, infiniteRepeatable(tween(610), RepeatMode.Reverse), label = "bar3")

    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(3.dp)) {
        listOf(barOne, barTwo, barThree).forEach { progress ->
            Box(
                modifier = Modifier
                    .width(4.dp)
                    .height((10 + (progress * 12)).dp)
                    .clip(RoundedCornerShape(100.dp))
                    .background(Color.White.copy(alpha = 0.9f))
            )
        }
    }
}

@Composable
private fun RightOrb(accent: Color, size: Dp = 40.dp) {
    Box(
        modifier = Modifier
            .size(size)
            .clip(CircleShape)
            .background(IslandBlack.copy(alpha = 0.7f))
            .border(1.dp, Color.White.copy(alpha = 0.12f), CircleShape),
        contentAlignment = Alignment.Center
    ) {
        Canvas(modifier = Modifier.size(size * 0.58f)) {
            drawArc(color = accent.copy(alpha = 0.85f), startAngle = -90f, sweepAngle = 220f, useCenter = false, style = Stroke(width = 5f, cap = StrokeCap.Round))
            drawArc(
                color = Color.White.copy(alpha = 0.75f),
                startAngle = 145f,
                sweepAngle = 58f,
                useCenter = false,
                style = Stroke(width = 3f, cap = StrokeCap.Round, pathEffect = PathEffect.cornerPathEffect(8f))
            )
        }
    }
}

@Composable
private fun ThinProgress(progress: Float, accent: Color, active: Boolean) {
    Box(modifier = Modifier.fillMaxWidth().height(8.dp)) {
        Box(
            modifier = Modifier
                .align(Alignment.CenterStart)
                .fillMaxWidth()
                .height(3.dp)
                .clip(RoundedCornerShape(100.dp))
                .background(Color.White.copy(alpha = 0.12f))
        )
        Box(
            modifier = Modifier
                .align(Alignment.CenterStart)
                .fillMaxWidth(progress.coerceIn(0f, 1f))
                .height(3.dp)
                .clip(RoundedCornerShape(100.dp))
                .background(
                    brush = Brush.horizontalGradient(
                        colors = listOf(
                            Color.White.copy(alpha = 0.95f),
                            accent.copy(alpha = if (active) 0.95f else 0.7f),
                            IslandProgressGlow.copy(alpha = if (active) 0.9f else 0.55f)
                        )
                    )
                )
                .blur(if (active) 0.6.dp else 0.dp)
        )
    }
}

@Composable
private fun ControlButton(enabled: Boolean, filled: Boolean, onClick: () -> Unit, icon: @Composable () -> Unit) {
    Surface(
        shape = CircleShape,
        color = if (filled) Color.White else Color.White.copy(alpha = 0.08f),
        contentColor = if (filled) IslandBlack else Color.White,
        border = if (filled) null else BorderStroke(1.dp, Color.White.copy(alpha = 0.10f))
    ) {
        Box(
            modifier = Modifier
                .size(if (filled) 48.dp else 42.dp)
                .alpha(if (enabled) 1f else 0.45f)
                .clickable(
                    enabled = enabled,
                    interactionSource = remember { MutableInteractionSource() },
                    indication = null,
                    onClick = onClick
                ),
            contentAlignment = Alignment.Center
        ) {
            icon()
        }
    }
}

@Composable
private fun SmallGhostButton(label: String, active: Boolean) {
    Surface(
        shape = CircleShape,
        color = if (active) Color.White.copy(alpha = 0.09f) else Color.White.copy(alpha = 0.05f),
        contentColor = Color.White.copy(alpha = if (active) 0.9f else 0.8f),
        border = BorderStroke(1.dp, Color.White.copy(alpha = if (active) 0.14f else 0.08f))
    ) {
        Box(modifier = Modifier.size(28.dp), contentAlignment = Alignment.Center) {
            Text(label, fontSize = 8.sp, fontWeight = FontWeight.Medium)
        }
    }
}

@Composable
private fun ModeControls(
    mode: IslandMode,
    isPlaying: Boolean,
    onModeChange: (IslandMode) -> Unit,
    onPlayPause: () -> Unit,
    onNext: () -> Unit,
    onPrevious: () -> Unit
) {
    Column(horizontalAlignment = Alignment.CenterHorizontally) {
        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            listOf(IslandMode.Minimal, IslandMode.Compact, IslandMode.Expanded).forEach { item ->
                ActionChip(text = item.name, selected = item == mode) { onModeChange(item) }
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            ActionChip(text = "Prev", selected = false, onClick = onPrevious)
            ActionChip(text = if (isPlaying) "Pause" else "Play", selected = true, onClick = onPlayPause)
            ActionChip(text = "Next", selected = false, onClick = onNext)
        }
    }
}

@Composable
private fun ActionChip(text: String, selected: Boolean, onClick: () -> Unit) {
    Surface(
        shape = CircleShape,
        color = if (selected) Color.White.copy(alpha = 0.14f) else Color.White.copy(alpha = 0.06f),
        border = BorderStroke(1.dp, Color.White.copy(alpha = if (selected) 0.18f else 0.10f))
    ) {
        Text(
            text = text,
            color = Color.White,
            fontSize = 12.sp,
            modifier = Modifier
                .clickable(
                    interactionSource = remember { MutableInteractionSource() },
                    indication = null,
                    onClick = onClick
                )
                .padding(horizontal = 14.dp, vertical = 9.dp)
        )
    }
}

@Composable
private fun MarqueeText(text: String, style: TextStyle) {
    Text(text = text, maxLines = 1, overflow = TextOverflow.Ellipsis, style = style)
}
