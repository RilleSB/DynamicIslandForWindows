using System;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

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
        private bool vinylRotationEnabled = true;
        private bool audioReactiveBreathingEnabled = true;
        private bool fluidPhysicsEnabled = false;
        private bool vinylScratchEnabled = true;
        private int visualizerSkin = 0;

        private void ApplySettings(AppSettings s)
        {
            scale = (double.IsNaN(s.Scale) || s.Scale < 0.5 || s.Scale > 3.0) ? 1.0 : s.Scale;
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
            vinylRotationEnabled = s.VinylRotationEnabled;
            audioReactiveBreathingEnabled = s.AudioReactiveBreathingEnabled;
            fluidPhysicsEnabled = s.FluidPhysicsEnabled;
            vinylScratchEnabled = s.VinylScratchEnabled;
            visualizerSkin = Math.Clamp(s.VisualizerSkin, 0, 3);
            ApplyLiquidGlassStyle(liquidGlassEnabled);
            ApplyDisplayAffinity(excludeFromCapture);
            ApplyVisualizerSkin(visualizerSkin);
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
            _settings.VinylRotationEnabled = vinylRotationEnabled;
            _settings.AudioReactiveBreathingEnabled = audioReactiveBreathingEnabled;
            _settings.FluidPhysicsEnabled = fluidPhysicsEnabled;
            _settings.VinylScratchEnabled = vinylScratchEnabled;
            _settings.VisualizerSkin = visualizerSkin;
            SettingsService.Save(_settings);
        }

        private DispatcherTimer _saveSettingsDebounceTimer;

        private void DebounceSaveSettings()
        {
            if (_isClosing)
                return;

            if (_saveSettingsDebounceTimer == null)
            {
                _saveSettingsDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                _saveSettingsDebounceTimer.Tick += (s, e) =>
                {
                    _saveSettingsDebounceTimer.Stop();
                    SaveSettings();
                };
            }
            _saveSettingsDebounceTimer.Stop();
            _saveSettingsDebounceTimer.Start();
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
            if (hasCustomPosition)
            {
                var point = new System.Drawing.Point((int)Math.Round(customX), (int)Math.Round(customY));
                return Screen.FromPoint(point);
            }

            if (!string.IsNullOrWhiteSpace(targetMonitorDeviceName))
            {
                foreach (var s in Screen.AllScreens)
                {
                    if (string.Equals(s.DeviceName, targetMonitorDeviceName, StringComparison.OrdinalIgnoreCase))
                        return s;
                }
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

        private double GetTargetLeft(double targetWidth)
        {
            return _islandModeController.CalculateWindowPosition(targetWidth, Height).left;
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
            _islandModeController.UpdateWindowPosition();
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
                        var (targetLeft, targetTop) = _islandModeController.CalculateWindowPosition(Width, Height);
                        Left = targetLeft;
                        Top = targetTop;
                        DebounceSaveSettings();
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
                    },
                    vinyl =>
                    {
                        SetVinylRotation(vinyl);
                    },
                    breathing =>
                    {
                        SetAudioReactiveBreathing(breathing);
                    },
                    fluid =>
                    {
                        SetFluidPhysics(fluid);
                    },
                    vinylScratch =>
                    {
                        SetVinylScratch(vinylScratch);
                    },
                    skin =>
                    {
                        SetVisualizerSkin(skin);
                    });

                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();
                if (_saveSettingsDebounceTimer?.IsEnabled == true)
                {
                    _saveSettingsDebounceTimer.Stop();
                    SaveSettings();
                }
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
            _trackTextPresenter?.SetTheme(dark);
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
                _trackTextPresenter?.ApplySourceVisuals(lastMusicInfo.SourceApp);

            SyncTrayMenuState();
            SaveSettings();
            Logger.Log($"Theme changed to {(dark ? "dark" : "light")}");
        }

        private void SetScale(double newScale)
        {
            scale = newScale;

            _islandModeController.SetScaleAndPosition();

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

        private void SetVinylRotation(bool enabled)
        {
            vinylRotationEnabled = enabled;
            _settings.VinylRotationEnabled = enabled;
            SaveSettings();
            if (enabled)
            {
                if (lastMusicInfo != null && lastMusicInfo.IsPlaying)
                    StartRotation();
            }
            else
            {
                ResetVinylRotation();
            }
        }

        private void SetAudioReactiveBreathing(bool enabled)
        {
            audioReactiveBreathingEnabled = enabled;
            _settings.AudioReactiveBreathingEnabled = enabled;
            SaveSettings();
            if (!enabled)
            {
                ResetBreathing();
            }
        }

        private void SetExcludeFromCapture(bool exclude)
        {
            excludeFromCapture = exclude;
            ApplyDisplayAffinity(exclude);
            SaveSettings();
            Logger.Log($"Exclude from capture set to {excludeFromCapture}");
        }

        private void SetFluidPhysics(bool enabled)
        {
            fluidPhysicsEnabled = enabled;
            _settings.FluidPhysicsEnabled = enabled;
            SaveSettings();
            Logger.Log($"Fluid physics animation set to {fluidPhysicsEnabled}");
        }

        private void SetVinylScratch(bool enabled)
        {
            vinylScratchEnabled = enabled;
            _settings.VinylScratchEnabled = enabled;
            SaveSettings();
            Logger.Log($"Vinyl scratch seek set to {vinylScratchEnabled}");
        }

        private void SetVisualizerSkin(int skin)
        {
            visualizerSkin = Math.Clamp(skin, 0, 3);
            _settings.VisualizerSkin = visualizerSkin;
            ApplyVisualizerSkin(visualizerSkin);
            SaveSettings();
            Logger.Log($"Visualizer skin set to {visualizerSkin}");
        }

        private void ApplyDisplayAffinity(bool exclude)
        {
            _windowChromeController?.ApplySettings(gamingModeEnabled, exclude);
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

                    IslandBorder.Effect = null;
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

        private bool DisableAlbumArtForDiagnostics => musicService?.DisableAlbumArtForDiagnostics ?? false;

        private void SetDisableAlbumArtForDiagnostics(bool enabled)
        {
            if (musicService == null)
                return;

            musicService.DisableAlbumArtForDiagnostics = enabled;
            if (enabled)
                ClearAlbumArtLayersForDiagnostics();

            SyncTrayMenuState();
            ApplyMusicInfo(musicService.GetCurrentMusicInfo());
        }

        private void ApplyClickThroughMode()
        {
            _windowChromeController?.ApplySettings(gamingModeEnabled, excludeFromCapture);
        }

        private void SyncTrayMenuState()
        {
            _trayMenuController?.UpdateState(CreateTrayMenuState());
        }

        private void UpdateTrayController(MusicInfo musicInfo)
        {
            _trayMenuController?.UpdateState(CreateTrayMenuState(musicInfo));
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
