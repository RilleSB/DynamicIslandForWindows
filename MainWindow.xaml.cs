using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace DynamicIslandPC
{
    public partial class MainWindow : Window
    {
        private int displayMode = 0; // 0=minimal, 1=compact, 2=expanded
        private MusicInfoService musicService;
        private DispatcherTimer updateTimer;
        private DebugWindow debugWindow;
        private HttpServerService httpServer;
        private string lastTrackId = "";
        private Storyboard rotationStoryboard;
        private Storyboard glowStoryboard;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private bool isTopPosition = true;
        private double customX = -1;
        private double customY = -1;
        private bool isDarkTheme = true;
        private double scale = 1.0;
        
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_SPACE = 0x20;

        public MainWindow()
        {
            try
            {
                Logger.Log("Application starting...");
                InitializeComponent();
                Opacity = 0;
                InitializeWindow();
                InitializeMusicService();
                RegisterGlobalHotkey();
                InitializeTrayIcon();
                StartGlowAnimation();
                AnimateStartup();
                Logger.Log("Application started successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start application", ex);
                throw;
            }
        }

        private void InitializeWindow()
        {
            UpdateWindowPosition();
            
            MouseWheel += OnMouseWheel;
            MouseDown += OnMouseDown;
            
            MouseLeftButtonUp += (s, e) => {
                if (e.ChangedButton == MouseButton.Left)
                {
                    CycleDisplayMode();
                    e.Handled = true;
                }
            };
            
            // Контекстное меню
            var contextMenu = new System.Windows.Controls.ContextMenu();
            
            var settingsItem = new System.Windows.Controls.MenuItem { Header = "Настройки позиции..." };
            settingsItem.Click += (s, e) => OpenSettings();
            contextMenu.Items.Add(settingsItem);
            
            var exitItem = new System.Windows.Controls.MenuItem { Header = "Выход" };
            exitItem.Click += (s, e) => {
                trayIcon.Visible = false;
                Application.Current.Shutdown();
            };
            contextMenu.Items.Add(exitItem);
            
            this.ContextMenu = contextMenu;
            
            MouseDoubleClick += (s, e) => {
                if (e.ChangedButton == MouseButton.Right)
                {
                    Logger.OpenLogFile();
                    e.Handled = true;
                }
            };
        }

        private void InitializeMusicService()
        {
            httpServer = new HttpServerService();
            httpServer.Start();
            
            musicService = new MusicInfoService(httpServer);
            
            updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            updateTimer.Tick += UpdateMusicInfo;
            updateTimer.Start();
            
            UpdateMusicInfo(null, null);
        }

        private void RegisterGlobalHotkey()
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL, VK_SPACE);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
            source.AddHook(HwndHook);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                CycleDisplayMode();
                handled = true;
            }
            return IntPtr.Zero;
        }
        
        private void CycleDisplayMode()
        {
            displayMode = (displayMode + 1) % 3;
            AnimateToMode();
        }

        private void AnimateToMode()
        {
            var storyboard = new Storyboard();
            
            // Анимация прозрачности для старого режима
            var oldMode = displayMode == 0 ? (displayMode == 1 ? CompactMode : ExpandedMode) : 
                         (displayMode == 1 ? (MinimalMode.Visibility == Visibility.Visible ? MinimalMode : ExpandedMode) : 
                         (CompactMode.Visibility == Visibility.Visible ? CompactMode : MinimalMode));
            
            // Определяем текущий видимый режим
            Grid currentMode = MinimalMode.Visibility == Visibility.Visible ? MinimalMode :
                              CompactMode.Visibility == Visibility.Visible ? CompactMode : ExpandedMode;
            
            // Определяем целевой режим
            Grid targetMode = displayMode == 0 ? MinimalMode : (displayMode == 1 ? CompactMode : ExpandedMode);
            
            if (currentMode != targetMode)
            {
                // Показываем целевой режим с нулевой прозрачностью
                targetMode.Visibility = Visibility.Visible;
                targetMode.Opacity = 0;
                
                // Анимация исчезновения старого режима
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(fadeOut, currentMode);
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
                storyboard.Children.Add(fadeOut);
                
                // Анимация появления нового режима
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(250),
                    BeginTime = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                Storyboard.SetTarget(fadeIn, targetMode);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
                storyboard.Children.Add(fadeIn);
                
                // Скрываем старый режим после анимации
                storyboard.Completed += (s, e) =>
                {
                    currentMode.Visibility = Visibility.Collapsed;
                    currentMode.Opacity = 1;
                };
            }
            
            double baseWidth = displayMode == 0 ? 115 : (displayMode == 1 ? 335 : 535);
            double baseHeight = displayMode == 0 ? 60 : (displayMode == 1 ? 70 : 110);
            double targetWidth = baseWidth * scale;
            double targetHeight = baseHeight * scale;
            
            double targetLeft;
            double targetTop;
            
            if (customX >= 0 && customY >= 0)
            {
                targetLeft = customX;
                targetTop = customY;
            }
            else
            {
                targetLeft = (SystemParameters.PrimaryScreenWidth - targetWidth) / 2;
                targetTop = isTopPosition ? 20 : SystemParameters.PrimaryScreenHeight - targetHeight - 60;
            }
            
            var widthAnimation = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(widthAnimation, this);
            Storyboard.SetTargetProperty(widthAnimation, new PropertyPath("Width"));
            storyboard.Children.Add(widthAnimation);
            
            var heightAnimation = new DoubleAnimation
            {
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(600),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(heightAnimation, this);
            Storyboard.SetTargetProperty(heightAnimation, new PropertyPath("Height"));
            storyboard.Children.Add(heightAnimation);
            
            var leftAnimation = new DoubleAnimation
            {
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(leftAnimation, this);
            Storyboard.SetTargetProperty(leftAnimation, new PropertyPath("Left"));
            storyboard.Children.Add(leftAnimation);
            
            var topAnimation = new DoubleAnimation
            {
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(topAnimation, this);
            Storyboard.SetTargetProperty(topAnimation, new PropertyPath("Top"));
            storyboard.Children.Add(topAnimation);
            
            storyboard.Begin();
        }

        private void UpdateMusicInfo(object sender, EventArgs e)
        {
            var musicInfo = musicService.GetCurrentMusicInfo();
            
            if (musicInfo != null)
            {
                var currentTrackId = $"{musicInfo.Artist}|{musicInfo.Title}";
                
                // Анимация при смене трека
                if (currentTrackId != lastTrackId && !string.IsNullOrEmpty(musicInfo.Title))
                {
                    AnimateTrackChange();
                    lastTrackId = currentTrackId;
                }
                
                TrackTitle.Text = musicInfo.Title ?? "Неизвестный трек";
                ArtistName.Text = musicInfo.Artist ?? "Неизвестный исполнитель";
                CompactTitle.Text = musicInfo.Title ?? "Неизвестный трек";
                CompactArtist.Text = musicInfo.Artist ?? "Неизвестный исполнитель";
                
                AlbumArtMinimal.Source = musicInfo.AlbumArt;
                AlbumArt.Source = musicInfo.AlbumArt;
                AlbumArtExpanded.Source = musicInfo.AlbumArt;
                
                // Показываем GIF рядом с иконкой при воспроизведении
                if (musicInfo.IsPlaying)
                {
                    GifMinimalContainer.Visibility = Visibility.Visible;
                    GifCompactContainer.Visibility = Visibility.Visible;
                    GifExpandedContainer.Visibility = Visibility.Visible;
                    StopRotation();
                }
                else
                {
                    GifMinimalContainer.Visibility = Visibility.Collapsed;
                    GifCompactContainer.Visibility = Visibility.Collapsed;
                    GifExpandedContainer.Visibility = Visibility.Collapsed;
                    StopRotation();
                }
                
                PlayPauseButton.Content = musicInfo.IsPlaying ? "⏸" : "▶";
                
                this.Title = $"Dynamic Island PC - {musicInfo.Title} - {musicInfo.Artist}";
                
                if (debugWindow != null)
                {
                    debugWindow.ClearDebugInfo();
                    debugWindow.AddDebugInfo(musicService.GetDebugInfo());
                }
            }
        }
        
        private void StartRotation()
        {
            if (rotationStoryboard != null) return;
            
            rotationStoryboard = new Storyboard();
            rotationStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            
            var rotation0 = new DoubleAnimation { From = 0, To = 360, Duration = TimeSpan.FromSeconds(3) };
            Storyboard.SetTarget(rotation0, AlbumArtMinimalRotation);
            Storyboard.SetTargetProperty(rotation0, new PropertyPath("Angle"));
            rotationStoryboard.Children.Add(rotation0);
            
            var rotation1 = new DoubleAnimation { From = 0, To = 360, Duration = TimeSpan.FromSeconds(3) };
            Storyboard.SetTarget(rotation1, AlbumArtRotation);
            Storyboard.SetTargetProperty(rotation1, new PropertyPath("Angle"));
            rotationStoryboard.Children.Add(rotation1);
            
            var rotation2 = new DoubleAnimation { From = 0, To = 360, Duration = TimeSpan.FromSeconds(3) };
            Storyboard.SetTarget(rotation2, AlbumArtExpandedRotation);
            Storyboard.SetTargetProperty(rotation2, new PropertyPath("Angle"));
            rotationStoryboard.Children.Add(rotation2);
            
            rotationStoryboard.Begin();
        }
        
        private void StopRotation()
        {
            if (rotationStoryboard != null)
            {
                rotationStoryboard.Stop();
                rotationStoryboard = null;
                AlbumArtMinimalRotation.Angle = 0;
                AlbumArtRotation.Angle = 0;
                AlbumArtExpandedRotation.Angle = 0;
            }
        }
        
        private void AnimateTrackChange()
        {
            var storyboard = new Storyboard();
            
            // Плавное изменение прозрачности
            var opacityAnim = new DoubleAnimation
            {
                From = 1,
                To = 0.5,
                Duration = TimeSpan.FromMilliseconds(200),
                AutoReverse = true,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(opacityAnim, IslandBorder);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnim);
            
            storyboard.Begin();
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            musicService.TogglePlayPause();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            musicService.NextTrack();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            musicService.PreviousTrack();
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new System.Windows.Forms.NotifyIcon();
            
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                }
                else
                {
                    trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
            }
            catch
            {
                trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            
            trayIcon.Text = "Dynamic Island PC";
            trayIcon.Visible = true;
            
            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            
            var positionMenu = new System.Windows.Forms.ToolStripMenuItem("Позиция");
            var topItem = new System.Windows.Forms.ToolStripMenuItem("Вверху", null, (s, e) => SetPosition(true)) { Checked = true };
            var bottomItem = new System.Windows.Forms.ToolStripMenuItem("Внизу", null, (s, e) => SetPosition(false));
            var customItem = new System.Windows.Forms.ToolStripMenuItem("Настройки позиции...", null, (s, e) => OpenSettings());
            positionMenu.DropDownItems.Add(topItem);
            positionMenu.DropDownItems.Add(bottomItem);
            positionMenu.DropDownItems.Add(customItem);
            contextMenu.Items.Add(positionMenu);
            
            var themeMenu = new System.Windows.Forms.ToolStripMenuItem("Тема");
            var darkItem = new System.Windows.Forms.ToolStripMenuItem("Тёмная", null, (s, e) => SetTheme(true)) { Checked = true };
            var lightItem = new System.Windows.Forms.ToolStripMenuItem("Светлая", null, (s, e) => SetTheme(false));
            themeMenu.DropDownItems.Add(darkItem);
            themeMenu.DropDownItems.Add(lightItem);
            contextMenu.Items.Add(themeMenu);
            
            var scaleMenu = new System.Windows.Forms.ToolStripMenuItem("Масштаб");
            var scale100 = new System.Windows.Forms.ToolStripMenuItem("100%", null, (s, e) => SetScale(1.0)) { Checked = true };
            var scale125 = new System.Windows.Forms.ToolStripMenuItem("125%", null, (s, e) => SetScale(1.25));
            var scale150 = new System.Windows.Forms.ToolStripMenuItem("150%", null, (s, e) => SetScale(1.5));
            scaleMenu.DropDownItems.Add(scale100);
            scaleMenu.DropDownItems.Add(scale125);
            scaleMenu.DropDownItems.Add(scale150);
            contextMenu.Items.Add(scaleMenu);
            
            contextMenu.Items.Add("Выход", null, (s, e) => 
            {
                trayIcon.Visible = false;
                Application.Current.Shutdown();
            });
            trayIcon.ContextMenuStrip = contextMenu;
        }
        
        private void SetPosition(bool top)
        {
            isTopPosition = top;
            customX = -1;
            customY = -1;
            AnimateToMode();
            
            var menu = (System.Windows.Forms.ToolStripMenuItem)trayIcon.ContextMenuStrip.Items[0];
            ((System.Windows.Forms.ToolStripMenuItem)menu.DropDownItems[0]).Checked = top;
            ((System.Windows.Forms.ToolStripMenuItem)menu.DropDownItems[1]).Checked = !top;
        }
        
        private void UpdateWindowPosition()
        {
            if (customX >= 0 && customY >= 0)
            {
                Left = customX;
                Top = customY;
            }
            else
            {
                Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                Top = isTopPosition ? 20 : SystemParameters.PrimaryScreenHeight - Height - 60;
            }
        }
        
        private void OpenSettings()
        {
            var currentX = customX >= 0 ? customX : Left;
            var currentY = customY >= 0 ? customY : Top;
            
            var settingsWindow = new SettingsWindow(currentX, currentY, 
                (x, y) => {
                    customX = x;
                    customY = y;
                    AnimateToMode();
                },
                (color) => {
                    ((SolidColorBrush)IslandBorder.Background).Color = color;
                    Logger.Log($"Background color changed to {color}");
                });
            settingsWindow.ShowDialog();
        }
        
        private void SetTheme(bool dark)
        {
            isDarkTheme = dark;
            var color = dark ? Color.FromRgb(0, 0, 0) : Color.FromRgb(255, 255, 255);
            ((SolidColorBrush)IslandBorder.Background).Color = color;
            
            var textColor = dark ? Brushes.White : Brushes.Black;
            var subTextColor = dark ? new SolidColorBrush(Color.FromRgb(170, 170, 170)) : new SolidColorBrush(Color.FromRgb(100, 100, 100));
            
            TrackTitle.Foreground = textColor;
            ArtistName.Foreground = subTextColor;
            CompactTitle.Foreground = textColor;
            CompactArtist.Foreground = subTextColor;
            
            var menu = (System.Windows.Forms.ToolStripMenuItem)trayIcon.ContextMenuStrip.Items[1];
            ((System.Windows.Forms.ToolStripMenuItem)menu.DropDownItems[0]).Checked = dark;
            ((System.Windows.Forms.ToolStripMenuItem)menu.DropDownItems[1]).Checked = !dark;
            
            Logger.Log($"Theme changed to {(dark ? "dark" : "light")}");
        }
        
        private void SetScale(double newScale)
        {
            scale = newScale;
            
            var baseWidth = displayMode == 0 ? 115 : (displayMode == 1 ? 335 : 535);
            var baseHeight = displayMode == 0 ? 60 : (displayMode == 1 ? 70 : 110);
            
            Width = baseWidth * scale;
            Height = baseHeight * scale;
            UpdateWindowPosition();
            
            var menu = (System.Windows.Forms.ToolStripMenuItem)trayIcon.ContextMenuStrip.Items[2];
            for (int i = 0; i < menu.DropDownItems.Count; i++)
            {
                ((System.Windows.Forms.ToolStripMenuItem)menu.DropDownItems[i]).Checked = false;
            }
            
            if (Math.Abs(newScale - 1.0) < 0.01)
                ((System.Windows.Forms.ToolStripMenuItem)menu.DropDownItems[0]).Checked = true;
            else if (Math.Abs(newScale - 1.25) < 0.01)
                ((System.Windows.Forms.ToolStripMenuItem)menu.DropDownItems[1]).Checked = true;
            else if (Math.Abs(newScale - 1.5) < 0.01)
                ((System.Windows.Forms.ToolStripMenuItem)menu.DropDownItems[2]).Checked = true;
            
            Logger.Log($"Scale changed to {(int)(newScale * 100)}%");
        }
        
        private void StartGlowAnimation()
        {
            // Свечение отключено
        }
        
        private void AnimateStartup()
        {
            var storyboard = new Storyboard();
            
            var opacityAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(opacityAnim, this);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnim);
            
            storyboard.Begin();
        }
        
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                musicService.PreviousTrack();
            }
            else if (e.Delta < 0)
            {
                musicService.NextTrack();
            }
        }
        
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                musicService.TogglePlayPause();
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID);
            updateTimer?.Stop();
            httpServer?.Stop();
            trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}
