using System;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DynamicIslandPC
{
    public partial class MainWindow
    {
        private string backgroundColorHex = "#FF000000";
        private double backgroundOpacity = 0.7;
        private Color albumAccentColor = Color.FromRgb(88, 88, 96);

        private void ApplySettings(AppSettings s)
        {
            scale = s.Scale;
            isTopPosition = s.IsTopPosition;
            customX = s.CustomX;
            customY = s.CustomY;
            isDarkTheme = s.IsDarkTheme;
            backgroundColorHex = string.IsNullOrWhiteSpace(s.BackgroundColor) ? "#FF000000" : s.BackgroundColor;
            backgroundOpacity = Math.Clamp(s.BackgroundOpacity, 0.1, 1.0);
        }

        private void SaveSettings()
        {
            _settings.Scale = scale;
            _settings.IsTopPosition = isTopPosition;
            _settings.CustomX = customX;
            _settings.CustomY = customY;
            _settings.IsDarkTheme = isDarkTheme;
            _settings.BackgroundColor = backgroundColorHex;
            _settings.BackgroundOpacity = backgroundOpacity;
            SettingsService.Save(_settings);
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
            brush.Opacity = backgroundOpacity;
        }

        private void UpdateAlbumAccent(Color accentColor, bool animated = true)
        {
            albumAccentColor = accentColor;

            var target = MixBaseAndAccent(GetConfiguredBackgroundColor(), albumAccentColor, isDarkTheme ? 0.22 : 0.12);
            if (IslandBorder.Background is not SolidColorBrush brush)
            {
                ApplyIslandBackground(target, backgroundOpacity);
                return;
            }

            if (!animated)
            {
                brush.Color = target;
                brush.Opacity = backgroundOpacity;
                return;
            }

            var colorAnimation = new ColorAnimation
            {
                To = target,
                Duration = TimeSpan.FromMilliseconds(420),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
            brush.Opacity = backgroundOpacity;
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

        private Rect GetTargetWorkingArea(double anchorX, double anchorY)
        {
            var point = new System.Drawing.Point((int)Math.Round(anchorX), (int)Math.Round(anchorY));
            var screen = Screen.FromPoint(point);
            return new Rect(screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height);
        }

        private (double left, double top) CalculateWindowPosition(double targetWidth, double targetHeight)
        {
            if (customX >= 0 && customY >= 0)
            {
                var workingArea = GetTargetWorkingArea(customX, customY);
                var left = Math.Clamp(customX - targetWidth / 2, workingArea.Left, workingArea.Right - targetWidth);
                var top = Math.Clamp(customY, workingArea.Top, workingArea.Bottom - targetHeight);
                return (left, top);
            }

            var anchorX = Left + (Width / 2);
            if (double.IsNaN(anchorX) || double.IsInfinity(anchorX) || anchorX <= 0)
                anchorX = SystemParameters.PrimaryScreenWidth / 2;

            var working = GetTargetWorkingArea(anchorX, Top);
            var centeredLeft = working.Left + (working.Width - targetWidth) / 2;
            var topPosition = isTopPosition ? working.Top + 20 : working.Bottom - targetHeight - 60;
            return (centeredLeft, topPosition);
        }

        private double GetTargetLeft(double targetWidth)
        {
            return CalculateWindowPosition(targetWidth, Height).left;
        }

        private void SetPosition(bool top)
        {
            isTopPosition = top;
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
            var currentX = customX >= 0 ? customX : Left + Width / 2;
            var currentY = customY >= 0 ? customY : Top;

            var settingsWindow = new SettingsWindow(
                currentX,
                currentY,
                (x, y) =>
                {
                    customX = x;
                    customY = y;
                    AnimateToMode();
                    SaveSettings();
                },
                color =>
                {
                    ApplyIslandBackground(color, backgroundOpacity);
                    SaveSettings();
                    Logger.Log($"Background color changed to {color}");
                });

            settingsWindow.ShowDialog();
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

        private void SyncTrayMenuState()
        {
            if (trayIcon?.ContextMenuStrip == null)
                return;

            if (trayIcon.ContextMenuStrip.Items[0] is ToolStripMenuItem positionMenu)
            {
                ((ToolStripMenuItem)positionMenu.DropDownItems[0]).Checked = isTopPosition && customX < 0;
                ((ToolStripMenuItem)positionMenu.DropDownItems[1]).Checked = !isTopPosition && customX < 0;
                ((ToolStripMenuItem)positionMenu.DropDownItems[2]).Checked = customX >= 0 && customY >= 0;
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
