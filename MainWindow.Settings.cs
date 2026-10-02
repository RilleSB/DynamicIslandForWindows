using System;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Runtime.InteropServices;

namespace DynamicIslandPC
{
    public partial class MainWindow
    {
        private string backgroundColorHex = "#FF000000";
        private double backgroundOpacity = 0.7;
        private Color albumAccentColor = Color.FromRgb(88, 88, 96);
        private AlbumColorPalette albumPalette = MusicVisualHelper.GetAlbumPalette(null);
        private bool adaptiveAlbumThemeEnabled = true;
        private bool liquidGlassEnabled = false;
        private string targetMonitorDeviceName = "";
        private bool excludeFromCapture = false;

        private void ApplySettings(AppSettings s)
        {
            scale = s.Scale;
            displayMode = Math.Clamp(s.DisplayMode, 0, 2);
            isTopPosition = s.IsTopPosition;
            hasCustomPosition = s.HasCustomPosition || (s.CustomX >= 0 && s.CustomY >= 0);
            customX = s.CustomX;
            customY = s.CustomY;
            isDarkTheme = s.IsDarkTheme;
            backgroundColorHex = string.IsNullOrWhiteSpace(s.BackgroundColor) ? "#FF000000" : s.BackgroundColor;
            backgroundOpacity = Math.Clamp(s.BackgroundOpacity, 0.1, 1.0);
            adaptiveAlbumThemeEnabled = s.AdaptiveAlbumThemeEnabled;
            decorationEnabled = s.DecorationEnabled;
            decorationMediaPath = s.DecorationMediaPath ?? string.Empty;
            gamingModeEnabled = s.GamingModeEnabled;
            lockModeEnabled = s.LockModeEnabled;
            startWithWindowsEnabled = s.StartWithWindowsEnabled || StartupHelper.IsEnabled();
            browserSourceEnabled = s.BrowserSourceEnabled;
            liquidGlassEnabled = s.LiquidGlassEnabled;
            targetMonitorDeviceName = s.TargetMonitorDeviceName ?? string.Empty;
            excludeFromCapture = s.ExcludeFromCapture;
            ApplyLiquidGlassStyle(liquidGlassEnabled);
            ApplyDisplayAffinity(excludeFromCapture);
        }

        private void SaveSettings()
        {
            _settings.Scale = scale;
            _settings.DisplayMode = displayMode;
            _settings.IsTopPosition = isTopPosition;
            _settings.HasCustomPosition = hasCustomPosition;
            _settings.CustomX = customX;
            _settings.CustomY = customY;
            _settings.IsDarkTheme = isDarkTheme;
            _settings.BackgroundColor = backgroundColorHex;
            _settings.BackgroundOpacity = backgroundOpacity;
            _settings.AdaptiveAlbumThemeEnabled = adaptiveAlbumThemeEnabled;
            _settings.DecorationEnabled = decorationEnabled;
            _settings.DecorationMediaPath = decorationMediaPath ?? string.Empty;
            _settings.GamingModeEnabled = gamingModeEnabled;
            _settings.LockModeEnabled = lockModeEnabled;
            _settings.StartWithWindowsEnabled = startWithWindowsEnabled;
            _settings.BrowserSourceEnabled = browserSourceEnabled;
            _settings.LiquidGlassEnabled = liquidGlassEnabled;
            _settings.TargetMonitorDeviceName = targetMonitorDeviceName;
            _settings.ExcludeFromCapture = excludeFromCapture;
            SettingsService.Save(_settings);
        }

        private double GetEffectiveBackgroundOpacity()
        {
            return liquidGlassEnabled ? 0.40 : backgroundOpacity;
        }

        private void ApplyIslandBackground(Color color, double opacity)
        {
            backgroundColorHex = color.ToString(CultureInfo.InvariantCulture);
            backgroundOpacity = Math.Clamp(opacity, 0.1, 1.0);

            if (IslandBorder.Background is not SolidColorBrush brush)
            {
                brush = new SolidColorBrush(color);
                IslandBorder.Background = brush;
            }

            brush.Color = color;
            brush.Opacity = GetEffectiveBackgroundOpacity();
        }

        private void UpdateAlbumAccent(Color accentColor, bool animated = true)
        {
            UpdateAlbumPalette(new AlbumColorPalette
            {
                Primary = accentColor,
                Secondary = MixBaseAndAccent(accentColor, Color.FromRgb(42, 42, 50), 0.45),
                Tertiary = MixBaseAndAccent(accentColor, Color.FromRgb(20, 20, 26), 0.7)
            }, animated);
        }

        private void UpdateAlbumPalette(AlbumColorPalette palette, bool animated = true)
        {
            albumPalette = palette ?? MusicVisualHelper.GetAlbumPalette(null);
            albumAccentColor = albumPalette.Primary;
            UpdateEqualizerColors(albumPalette.Primary);

            if (!adaptiveAlbumThemeEnabled)
            {
                ApplyStaticBackground(animated);
                return;
            }

            var target = MixBaseAndAccent(GetConfiguredBackgroundColor(), albumPalette.Primary, isDarkTheme ? 0.24 : 0.14);
            if (IslandBorder.Background is not SolidColorBrush brush)
            {
                ApplyIslandBackground(target, backgroundOpacity);
                UpdateAlbumAura(animated: false);
                return;
            }

            if (!animated)
            {
                brush.Color = target;
                brush.Opacity = GetEffectiveBackgroundOpacity();
                UpdateAlbumAura(animated: false);
                return;
            }

            var colorAnimation = new ColorAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(420),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
            brush.Opacity = GetEffectiveBackgroundOpacity();
            UpdateAlbumAura(animated: true);
        }

        private void ApplyStaticBackground(bool animated)
        {
            var target = GetConfiguredBackgroundColor();
            if (IslandBorder.Background is not SolidColorBrush brush)
            {
                ApplyIslandBackground(target, backgroundOpacity);
            }
            else if (animated)
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
                {
                    To = target,
                    Duration = TimeSpan.FromMilliseconds(360),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
                brush.Opacity = GetEffectiveBackgroundOpacity();
            }
            else
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                brush.Color = target;
                brush.Opacity = GetEffectiveBackgroundOpacity();
            }

            ClearAlbumAura(animated);
        }

        private void ClearAlbumAura(bool animated)
        {
            SetGradientStopColor(AlbumAuraPrimaryStop, Colors.Transparent, animated);
            SetGradientStopColor(AlbumAuraSecondaryStop, Colors.Transparent, animated);
            SetGradientStopColor(AlbumAuraTertiaryStop, Colors.Transparent, animated);
        }

        private void UpdateAlbumAura(bool animated)
        {
            SetGradientStopColor(AlbumAuraPrimaryStop, WithAlpha(albumPalette.Primary, isDarkTheme ? (byte)88 : (byte)46), animated);
            SetGradientStopColor(AlbumAuraSecondaryStop, WithAlpha(albumPalette.Secondary, isDarkTheme ? (byte)58 : (byte)34), animated);
            SetGradientStopColor(AlbumAuraTertiaryStop, WithAlpha(albumPalette.Tertiary, isDarkTheme ? (byte)32 : (byte)18), animated);
        }

        private static void SetGradientStopColor(GradientStop stop, Color color, bool animated)
        {
            if (!animated)
            {
                stop.Color = color;
                return;
            }

            stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation
            {
                To = color,
                Duration = TimeSpan.FromMilliseconds(520),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }

        private static Color WithAlpha(Color color, byte alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private Color GetConfiguredBackgroundColor()
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(backgroundColorHex);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to parse configured background color", ex);
                return Colors.Black;
            }
        }

        private Screen GetTargetScreen()
        {
            if (!string.IsNullOrWhiteSpace(targetMonitorDeviceName))
            {
                foreach (var s in Screen.AllScreens)
                {
                    if (string.Equals(s.DeviceName, targetMonitorDeviceName, StringComparison.OrdinalIgnoreCase))
                        return s;
                }
            }

            if (hasCustomPosition)
            {
                var point = new System.Drawing.Point((int)Math.Round(customX), (int)Math.Round(customY));
                return Screen.FromPoint(point);
            }

            return Screen.PrimaryScreen ?? (Screen.AllScreens.Length > 0 ? Screen.AllScreens[0] : null);
        }

        private Rect GetTargetWorkingArea(double anchorX, double anchorY)
        {
            var targetScreen = GetTargetScreen();
            if (targetScreen != null)
                return new Rect(targetScreen.WorkingArea.Left, targetScreen.WorkingArea.Top, targetScreen.WorkingArea.Width, targetScreen.WorkingArea.Height);

            var point = new System.Drawing.Point((int)Math.Round(anchorX), (int)Math.Round(anchorY));
            var screen = Screen.FromPoint(point);
            return new Rect(screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height);
        }

        private (double left, double top) CalculateWindowPosition(double targetWidth, double targetHeight)
        {
            var workingArea = GetTargetWorkingArea(customX, customY);
            if (hasCustomPosition)
            {
                var left = Math.Clamp(customX - targetWidth / 2, workingArea.Left, workingArea.Right - targetWidth);
                var top = Math.Clamp(customY, workingArea.Top, workingArea.Bottom - targetHeight);
                return (left, top);
            }

            var centeredLeft = workingArea.Left + (workingArea.Width - targetWidth) / 2;
            var topPosition = isTopPosition ? workingArea.Top + 20 : workingArea.Bottom - targetHeight - 60;
            return (centeredLeft, topPosition);
        }

        private double GetTargetLeft(double targetWidth)
        {
            return CalculateWindowPosition(targetWidth, Height).left;
        }

        private void SetPosition(bool top)
        {
            isTopPosition = top;
            hasCustomPosition = false;
            customX = -1;
            customY = -1;
            AnimateToMode();
            SaveSettings();
            SyncTrayMenuState();
        }

        private void UpdateWindowPosition()
        {
            var position = CalculateWindowPosition(Width, Height);
            Left = position.left;
            Top = position.top;
        }

        private void OpenSettings()
        {
            try
            {
                var targetScreen = GetTargetScreen();
                var currentX = hasCustomPosition
                    ? customX
                    : (targetScreen != null ? targetScreen.WorkingArea.Left + targetScreen.WorkingArea.Width / 2.0 : Left + Width / 2);
                var currentY = hasCustomPosition
                    ? customY
                    : (targetScreen != null ? (isTopPosition ? targetScreen.WorkingArea.Top + 20 : targetScreen.WorkingArea.Bottom - Height - 60) : Top);

                var settingsWindow = new SettingsWindow(
                    currentX,
                    currentY,
                    _settings,
                    (x, y) =>
                    {
                        hasCustomPosition = true;
                        customX = x;
                        customY = y;
                        AnimateToMode();
                        SaveSettings();
                    },
                    dark =>
                    {
                        SetTheme(dark);
                    },
                    (color, opacity) =>
                    {
                        ApplyIslandBackground(color, opacity);
                        UpdateAlbumPalette(albumPalette);
                        SaveSettings();
                        Logger.Log($"Background changed to {color} with opacity {opacity:0.00}");
                    },
                    enabled =>
                    {
                        SetAdaptiveAlbumTheme(enabled);
                    },
                    (enabled, mediaPath) =>
                    {
                        SetDecoration(enabled, mediaPath);
                    },
                    enabled =>
                    {
                        SetLiquidGlass(enabled);
                    },
                    monitor =>
                    {
                        SetTargetMonitor(monitor);
                    },
                    exclude =>
                    {
                        SetExcludeFromCapture(exclude);
                    });

                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open settings window", ex);
                System.Windows.MessageBox.Show(
                    "Не получилось открыть настройки. Я записал ошибку в лог.",
                    "Dynamic Island PC",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void SetTheme(bool dark, bool applyBackground = true)
        {
            isDarkTheme = dark;
            if (applyBackground)
                ApplyIslandBackground(dark ? Colors.Black : Colors.White, backgroundOpacity);

            var textColor = dark ? Brushes.White : Brushes.Black;
            var subTextColor = dark
                ? new SolidColorBrush(Color.FromRgb(196, 196, 204))
                : new SolidColorBrush(Color.FromRgb(90, 90, 98));

            UpdateAlbumAccent(albumAccentColor, animated: false);
            TrackTitle.Foreground = textColor;
            ArtistName.Foreground = subTextColor;
            CompactTitle.Foreground = textColor;
            CompactArtist.Foreground = subTextColor;
            TrackTitleOld.Foreground = textColor;
            ArtistNameOld.Foreground = subTextColor;
            CompactTitleOld.Foreground = textColor;
            CompactArtistOld.Foreground = subTextColor;
            CompactSourceTextOld.Foreground = subTextColor;
            CompactSourceText.Foreground = subTextColor;
            ExpandedSourceTextOld.Foreground = subTextColor;
            ExpandedSourceText.Foreground = subTextColor;
            ProgressBarCompact.Fill = dark ? new SolidColorBrush(Color.FromRgb(234, 246, 255)) : new SolidColorBrush(Color.FromRgb(30, 30, 35));
            ProgressBarExpanded.Fill = dark ? new SolidColorBrush(Color.FromRgb(234, 246, 255)) : new SolidColorBrush(Color.FromRgb(30, 30, 35));
            ProgressDotCompact.Fill = dark ? Brushes.White : Brushes.Black;
            ProgressDotExpanded.Fill = dark ? Brushes.White : Brushes.Black;
            ProgressTrackCompact.Fill = dark ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(38, 0, 0, 0));
            ProgressTrackExpanded.Fill = dark ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(38, 0, 0, 0));
            CurrentTimeText.Foreground = dark ? new SolidColorBrush(Color.FromRgb(168, 168, 178)) : new SolidColorBrush(Color.FromRgb(96, 96, 104));
            TotalTimeText.Foreground = dark ? new SolidColorBrush(Color.FromRgb(168, 168, 178)) : new SolidColorBrush(Color.FromRgb(96, 96, 104));
            FavoriteButton.Foreground = dark ? new SolidColorBrush(Color.FromRgb(199, 199, 208)) : new SolidColorBrush(Color.FromRgb(48, 48, 54));
            OutputButton.Foreground = dark ? new SolidColorBrush(Color.FromRgb(199, 199, 208)) : new SolidColorBrush(Color.FromRgb(48, 48, 54));
            if (lastMusicInfo != null)
                ApplySourceVisuals(lastMusicInfo.SourceApp);

            SyncTrayMenuState();
            SaveSettings();
            Logger.Log($"Theme changed to {(dark ? "dark" : "light")}");
        }

        private void SetScale(double newScale)
        {
            scale = newScale;

            var (baseWidth, baseHeight) = GetModeSize(displayMode);
            Width = baseWidth * scale;
            Height = baseHeight * scale;
            UpdateWindowPosition();

            SyncTrayMenuState();
            SaveSettings();
            Logger.Log($"Scale changed to {(int)(newScale * 100)}%");
        }

        private void SetDecoration(bool enabled, string mediaPath)
        {
            decorationEnabled = enabled;
            decorationMediaPath = mediaPath ?? string.Empty;
            ApplyDecorationMedia();
            UpdateDecorationVisibility(lastMusicInfo?.IsPlaying == true);
            SaveSettings();
            Logger.Log(string.IsNullOrWhiteSpace(decorationMediaPath)
                ? $"Decoration {(enabled ? "enabled" : "disabled")} with default media"
                : $"Decoration {(enabled ? "enabled" : "disabled")} with {decorationMediaPath}");
        }

        private void SetAdaptiveAlbumTheme(bool enabled)
        {
            adaptiveAlbumThemeEnabled = enabled;
            UpdateAlbumPalette(lastMusicInfo?.AlbumArt != null
                ? MusicVisualHelper.GetAlbumPalette(lastMusicInfo.AlbumArt)
                : albumPalette);
            SaveSettings();
            Logger.Log($"Adaptive album theme {(adaptiveAlbumThemeEnabled ? "enabled" : "disabled")}");
        }

        private void SetGamingMode(bool enabled)
        {
            gamingModeEnabled = enabled;
            ApplyClickThroughMode();
            SyncTrayMenuState();
            SaveSettings();
            Logger.Log($"Gaming mode {(gamingModeEnabled ? "enabled" : "disabled")}");
        }

        private void SetTargetMonitor(string monitorDeviceName)
        {
            targetMonitorDeviceName = monitorDeviceName ?? string.Empty;
            SaveSettings();
            AnimateToMode();
            Logger.Log($"Target monitor changed to {targetMonitorDeviceName}");
        }

        private void SetExcludeFromCapture(bool exclude)
        {
            excludeFromCapture = exclude;
            ApplyDisplayAffinity(exclude);
            SaveSettings();
            Logger.Log($"Exclude from capture set to {excludeFromCapture}");
        }

        private void ApplyDisplayAffinity(bool exclude)
        {
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                IntPtr hwnd = helper.Handle;
                if (hwnd == IntPtr.Zero)
                {
                    try
                    {
                        hwnd = helper.EnsureHandle();
                    }
                    catch { }
                }

                if (hwnd == IntPtr.Zero)
                {
                    Logger.Log($"ApplyDisplayAffinity deferred: Handle is IntPtr.Zero, waiting for Loaded event (exclude={exclude})");
                    RoutedEventHandler onLoaded = null;
                    onLoaded = (s, e) =>
                    {
                        Loaded -= onLoaded;
                        ApplyDisplayAffinity(exclude);
                    };
                    Loaded += onLoaded;
                    return;
                }

                const uint WDA_NONE = 0x00000000;
                const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
                const uint WDA_MONITOR = 0x00000001;

                uint affinity = exclude ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE;
                bool ok = SetWindowDisplayAffinity(hwnd, affinity);
                int err = Marshal.GetLastWin32Error();

                if (!ok && exclude)
                {
                    Logger.Error($"SetWindowDisplayAffinity(0x{affinity:X}) failed (err={err}), attempting fallback to WDA_MONITOR");
                    bool fallbackOk = SetWindowDisplayAffinity(hwnd, WDA_MONITOR);
                    int fallbackErr = Marshal.GetLastWin32Error();
                    Logger.Log($"Fallback SetWindowDisplayAffinity(WDA_MONITOR) result: {fallbackOk}, err={fallbackErr}");
                }

                GetWindowDisplayAffinity(hwnd, out uint currentAffinity);
                Logger.Log($"ApplyDisplayAffinity finished for hWnd=0x{hwnd:X}: requested=0x{affinity:X}, ok={ok}, err={err}, actualAffinity=0x{currentAffinity:X}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply window display affinity", ex);
            }
        }

        private void SetLiquidGlass(bool enabled)
        {
            liquidGlassEnabled = enabled;
            ApplyLiquidGlassStyle(enabled);
            SaveSettings();
            Logger.Log($"Liquid Glass mode {(liquidGlassEnabled ? "enabled" : "disabled")}");
        }

        private void ApplyLiquidGlassStyle(bool enabled)
        {
            if (LiquidGlassLayer != null)
                LiquidGlassLayer.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

            if (AlbumAuraBorder != null)
                AlbumAuraBorder.Opacity = enabled ? 0.85 : 0.55;

            if (IslandBorder != null)
            {
                if (IslandBorder.Background is SolidColorBrush bgBrush)
                {
                    bgBrush.Opacity = GetEffectiveBackgroundOpacity();
                }

                if (enabled)
                {
                    IslandBorder.BorderThickness = new Thickness(1.5);
                    IslandBorder.BorderBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(235, 255, 255, 255), 0),
                            new GradientStop(Color.FromArgb(70, 255, 255, 255), 0.25),
                            new GradientStop(Color.FromArgb(20, 255, 255, 255), 0.65),
                            new GradientStop(Color.FromArgb(90, 255, 255, 255), 1.0)
                        }
                    };

                    IslandBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 36,
                        ShadowDepth = 6,
                        Direction = 270,
                        Color = Colors.Black,
                        Opacity = 0.60
                    };
                }
                else
                {
                    IslandBorder.BorderThickness = new Thickness(1);
                    IslandBorder.BorderBrush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(46, 255, 255, 255), 0),
                            new GradientStop(Color.FromArgb(18, 255, 255, 255), 1)
                        }
                    };

                    IslandBorder.Effect = null;
                }
            }

            UpdateAlbumPalette(albumPalette, animated: false);
        }

        private void SetLockMode(bool enabled)
        {
            lockModeEnabled = enabled;
            SyncTrayMenuState();
            SaveSettings();
            Logger.Log($"Lock mode {(lockModeEnabled ? "enabled" : "disabled")}");
        }

        private void SetStartWithWindows(bool enabled)
        {
            try
            {
                StartupHelper.SetEnabled(enabled);
                startWithWindowsEnabled = StartupHelper.IsEnabled();
                SyncTrayMenuState();
                SaveSettings();
                Logger.Log($"Startup with Windows {(startWithWindowsEnabled ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update startup setting", ex);
            }
        }

        private void ApplyStartupPreference()
        {
            if (!startWithWindowsEnabled)
                return;

            try
            {
                StartupHelper.SetEnabled(true);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply startup preference", ex);
            }
        }

        private void SetBrowserSourceEnabled(bool enabled)
        {
            browserSourceEnabled = enabled;
            musicService?.SetBrowserSourceEnabled(enabled);
            SyncTrayMenuState();
            SaveSettings();
            Logger.Log($"Browser source {(browserSourceEnabled ? "enabled" : "disabled")}");
        }

        private void ApplyClickThroughMode()
        {
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                if (helper.Handle == IntPtr.Zero)
                    return;

                var style = GetWindowLong(helper.Handle, GWL_EXSTYLE);
                var newStyle = gamingModeEnabled
                    ? style | WS_EX_TRANSPARENT
                    : style & ~WS_EX_TRANSPARENT;

                if (newStyle != style)
                    SetWindowLong(helper.Handle, GWL_EXSTYLE, newStyle);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply click-through gaming mode", ex);
            }
        }

        private void SyncTrayMenuState()
        {
            if (trayIcon?.ContextMenuStrip == null)
                return;

            if (trayIcon.ContextMenuStrip.Items[0] is ToolStripMenuItem positionMenu)
            {
                ((ToolStripMenuItem)positionMenu.DropDownItems[0]).Checked = isTopPosition && !hasCustomPosition;
                ((ToolStripMenuItem)positionMenu.DropDownItems[1]).Checked = !isTopPosition && !hasCustomPosition;
                ((ToolStripMenuItem)positionMenu.DropDownItems[2]).Checked = hasCustomPosition;
            }

            if (trayIcon.ContextMenuStrip.Items[1] is ToolStripMenuItem themeMenu)
            {
                ((ToolStripMenuItem)themeMenu.DropDownItems[0]).Checked = isDarkTheme;
                ((ToolStripMenuItem)themeMenu.DropDownItems[1]).Checked = !isDarkTheme;
            }

            if (trayIcon.ContextMenuStrip.Items[2] is ToolStripMenuItem scaleMenu)
            {
                for (int i = 0; i < scaleMenu.DropDownItems.Count; i++)
                    ((ToolStripMenuItem)scaleMenu.DropDownItems[i]).Checked = false;

                if (Math.Abs(scale - 1.0) < 0.01)
                    ((ToolStripMenuItem)scaleMenu.DropDownItems[0]).Checked = true;
                else if (Math.Abs(scale - 1.25) < 0.01)
                    ((ToolStripMenuItem)scaleMenu.DropDownItems[1]).Checked = true;
                else if (Math.Abs(scale - 1.5) < 0.01)
                    ((ToolStripMenuItem)scaleMenu.DropDownItems[2]).Checked = true;
            }

            if (trayIcon.ContextMenuStrip.Items[3] is ToolStripMenuItem gamingItem)
                gamingItem.Checked = gamingModeEnabled;

            if (trayIcon.ContextMenuStrip.Items[4] is ToolStripMenuItem lockItem)
                lockItem.Checked = lockModeEnabled;

            if (trayIcon.ContextMenuStrip.Items[5] is ToolStripMenuItem startupItem)
                startupItem.Checked = startWithWindowsEnabled;

            if (trayIcon.ContextMenuStrip.Items[6] is ToolStripMenuItem sourcesMenu &&
                sourcesMenu.DropDownItems.Count > 0 &&
                sourcesMenu.DropDownItems[0] is ToolStripMenuItem browserItem)
            {
                browserItem.Checked = browserSourceEnabled;
            }
        }

        private void UpdateTrayController(MusicInfo musicInfo)
        {
            if (trayTrackItem != null)
            {
                var title = string.IsNullOrWhiteSpace(musicInfo?.Title) ? "Нет медиа" : musicInfo.Title;
                var artist = string.IsNullOrWhiteSpace(musicInfo?.Artist) ? musicInfo?.SourceApp : musicInfo.Artist;
                var text = string.IsNullOrWhiteSpace(artist) ? title : $"{title} - {artist}";
                trayTrackItem.Text = text.Length > 60 ? text.Substring(0, 57) + "..." : text;
            }

            if (trayPlayPauseItem != null)
                trayPlayPauseItem.Text = musicInfo?.IsPlaying == true ? "Пауза" : "Воспроизведение";
        }

        private static Color MixBaseAndAccent(Color baseColor, Color accentColor, double accentAmount)
        {
            return Color.FromRgb(
                (byte)(baseColor.R + (accentColor.R - baseColor.R) * accentAmount),
                (byte)(baseColor.G + (accentColor.G - baseColor.G) * accentAmount),
                (byte)(baseColor.B + (accentColor.B - baseColor.B) * accentAmount));
        }
    }
}
