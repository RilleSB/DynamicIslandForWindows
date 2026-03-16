using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        private MusicInfo lastMusicInfo = null;
        private bool isFetchingMusic = false;
        private bool isPaused = false;
        private Storyboard rotationStoryboard;
        private Storyboard glowStoryboard;
        private Storyboard _pauseInStoryboard;
        private Storyboard _pauseOutStoryboard;
        private DispatcherTimer _pauseDebounceTimer;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private bool isTopPosition = true;
        private double customX = -1;
        private double customY = -1;
        private bool isDarkTheme = true;
        private double scale = 1.0;
        private AppSettings _settings;
        private DispatcherTimer _progressTimer;
        
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
                _settings = SettingsService.Load();
                ApplySettings(_settings);
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

        private void ApplySettings(AppSettings s)
        {
            scale = s.Scale;
            isTopPosition = s.IsTopPosition;
            customX = s.CustomX;
            customY = s.CustomY;
            isDarkTheme = s.IsDarkTheme;
        }

        private void SaveSettings()
        {
            _settings.Scale = scale;
            _settings.IsTopPosition = isTopPosition;
            _settings.CustomX = customX;
            _settings.CustomY = customY;
            _settings.IsDarkTheme = isDarkTheme;
            SettingsService.Save(_settings);
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
            musicService = new MusicInfoService();
            musicService.MusicInfoChanged += info => Dispatcher.Invoke(() => ApplyMusicInfo(info));
            ApplyMusicInfo(musicService.GetCurrentMusicInfo());
            InitializeProgressTimer();
        }

        private void InitializeProgressTimer()
        {
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _progressTimer.Tick += (s, e) =>
            {
                if (lastMusicInfo == null || lastMusicInfo.Duration == TimeSpan.Zero) return;
                if (lastMusicInfo.IsPlaying)
                    lastMusicInfo.Position += TimeSpan.FromSeconds(1);
                SetProgressRatio(lastMusicInfo.Position.TotalSeconds / lastMusicInfo.Duration.TotalSeconds);
            };
            _progressTimer.Start();
        }

        private void SetProgressRatio(double ratio)
        {
            if (isPaused) return;
            ratio = Math.Min(1.0, Math.Max(0.0, ratio));
            void Apply(System.Windows.Shapes.Rectangle track, System.Windows.Shapes.Rectangle bar, System.Windows.Shapes.Ellipse dot)
            {
                var w = track.ActualWidth;
                if (w <= 0) return;
                var filled = w * ratio;
                bar.Width = filled;
                dot.Margin = new Thickness(Math.Max(0, filled - 3), 0, 0, 0);
            }
            Apply(ProgressTrackCompact, ProgressBarCompact, ProgressDotCompact);
            Apply(ProgressTrackExpanded, ProgressBarExpanded, ProgressDotExpanded);
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
            
            var (baseWidth, baseHeight) = GetModeSize(displayMode);
            double targetWidth = baseWidth * scale;
            double targetHeight = baseHeight * scale;
            
            double targetLeft;
            double targetTop;
            
            if (customX >= 0 && customY >= 0)
            {
                targetLeft = customX - targetWidth / 2;
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

        private void ApplyMusicInfo(MusicInfo musicInfo)
        {
            if (musicInfo == null) return;
            
            var currentTrackId = $"{musicInfo.Artist}|{musicInfo.Title}";
            var prevTrackId = lastMusicInfo != null ? $"{lastMusicInfo.Artist}|{lastMusicInfo.Title}" : "";
            
            bool trackChanged = currentTrackId != prevTrackId;

            if (trackChanged)
            {
                AnimateTrackChange();
                CrossfadeAlbumArt(musicInfo.AlbumArt);
                CrossfadeText(
                    musicInfo.Title ?? "Неизвестный трек",
                    musicInfo.Artist ?? "Неизвестный исполнитель");
                ShowTrackNotification(musicInfo.Artist, musicInfo.Title);
            }
            else
            {
                TrackTitle.Text = musicInfo.Title ?? "Неизвестный трек";
                ArtistName.Text = musicInfo.Artist ?? "Неизвестный исполнитель";
                CompactTitle.Text = musicInfo.Title ?? "Неизвестный трек";
                CompactArtist.Text = musicInfo.Artist ?? "Неизвестный исполнитель";
                AlbumArtMinimal.Source = musicInfo.AlbumArt;
                AlbumArt.Source = musicInfo.AlbumArt;
                AlbumArtExpanded.Source = musicInfo.AlbumArt;
            }

            lastMusicInfo = musicInfo;

            if (musicInfo.Duration > TimeSpan.Zero)
                SetProgressRatio(musicInfo.Position.TotalSeconds / musicInfo.Duration.TotalSeconds);
            
            var gifVisible = musicInfo.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
            GifMinimalContainer.Visibility = gifVisible;
            GifCompactContainer.Visibility = gifVisible;
            GifExpandedContainer.Visibility = gifVisible;
            
            if (!musicInfo.IsPlaying) StopRotation();
            
            PlayPauseButton.Content = musicInfo.IsPlaying ? "⏸" : "▶";
            this.Title = $"Dynamic Island PC - {musicInfo.Title} - {musicInfo.Artist}";
            
            // Переключаем режим паузы
            if (!musicInfo.IsPlaying && !isPaused)
            {
                _pauseDebounceTimer?.Stop();
                _pauseDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                _pauseDebounceTimer.Tick += (s, e) =>
                {
                    _pauseDebounceTimer.Stop();
                    if (!isPaused)
                    {
                        isPaused = true;
                        AnimateToPaused();
                    }
                };
                _pauseDebounceTimer.Start();
            }
            else if (musicInfo.IsPlaying && isPaused)
            {
                _pauseDebounceTimer?.Stop();
                isPaused = false;
                AnimateFromPaused();
            }
            else if (musicInfo.IsPlaying)
            {
                _pauseDebounceTimer?.Stop();
            }
        }
        
        private (double width, double height) GetModeSize(int mode) => mode switch
        {
            0 => (115, 60),
            1 => (335, 70),
            _ => (535, 110)
        };

        private double GetTargetLeft(double targetWidth)
        {
            if (customX >= 0)
                return customX - targetWidth / 2;
            return (SystemParameters.PrimaryScreenWidth - targetWidth) / 2;
        }

        private void AnimateToPaused()
        {
            var currentMode = displayMode == 0 ? MinimalMode : (displayMode == 1 ? CompactMode : ExpandedMode);

            _pauseOutStoryboard?.Stop();
            _pauseInStoryboard?.Stop();

            var fadeOut = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(300) };
            fadeOut.Completed += (s, e) =>
            {
                currentMode.Visibility = Visibility.Collapsed;
                PausedMode.Visibility = Visibility.Visible;
                PausedMode.Opacity = 0;

                _pauseInStoryboard = new Storyboard();

                var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(200) };
                Storyboard.SetTarget(fadeIn, PausedMode);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
                _pauseInStoryboard.Children.Add(fadeIn);

                var pausedSize = 60 * scale;
                var w = new DoubleAnimation { To = pausedSize, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(w, this);
                Storyboard.SetTargetProperty(w, new PropertyPath("Width"));
                _pauseInStoryboard.Children.Add(w);

                var h = new DoubleAnimation { To = pausedSize, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(h, this);
                Storyboard.SetTargetProperty(h, new PropertyPath("Height"));
                _pauseInStoryboard.Children.Add(h);

                var l = new DoubleAnimation { To = GetTargetLeft(pausedSize), Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(l, this);
                Storyboard.SetTargetProperty(l, new PropertyPath("Left"));
                _pauseInStoryboard.Children.Add(l);

                _pauseInStoryboard.Begin();
            };
            currentMode.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void AnimateFromPaused()
        {
            var targetMode = displayMode == 0 ? MinimalMode : (displayMode == 1 ? CompactMode : ExpandedMode);
            var (baseWidth, baseHeight) = GetModeSize(displayMode);

            _pauseInStoryboard?.Stop();
            _pauseOutStoryboard?.Stop();
            _pauseOutStoryboard = new Storyboard();

            var fadeOut = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(200) };
            fadeOut.Completed += (s, e) =>
            {
                PausedMode.Visibility = Visibility.Collapsed;
                targetMode.Visibility = Visibility.Visible;
                targetMode.Opacity = 0;

                var sb = new Storyboard();

                var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(300) };
                Storyboard.SetTarget(fadeIn, targetMode);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
                sb.Children.Add(fadeIn);

                var tw = baseWidth * scale;
                var w = new DoubleAnimation { To = tw, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(w, this);
                Storyboard.SetTargetProperty(w, new PropertyPath("Width"));
                sb.Children.Add(w);

                var h = new DoubleAnimation { To = baseHeight * scale, Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(h, this);
                Storyboard.SetTargetProperty(h, new PropertyPath("Height"));
                sb.Children.Add(h);

                var l = new DoubleAnimation { To = GetTargetLeft(tw), Duration = TimeSpan.FromMilliseconds(400), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(l, this);
                Storyboard.SetTargetProperty(l, new PropertyPath("Left"));
                sb.Children.Add(l);

                sb.Completed += (_, __) =>
                {
                    if (lastMusicInfo?.Duration > TimeSpan.Zero)
                        SetProgressRatio(lastMusicInfo.Position.TotalSeconds / lastMusicInfo.Duration.TotalSeconds);
                };
                sb.Begin();
            };
            PausedMode.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ShowTrackNotification(string artist, string title)
        {
            var text = $"{artist} — {title}";
            var notification = new TrackNotificationWindow(text, Left, Top, Width, Height);
            notification.Show();
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
        
        private void CrossfadeAlbumArt(System.Windows.Media.ImageSource newArt)
        {
            AlbumArtOld.Source = AlbumArt.Source;
            AlbumArtExpandedOld.Source = AlbumArtExpanded.Source;

            AlbumArt.Source = newArt;
            AlbumArtExpanded.Source = newArt;
            AlbumArtMinimal.Source = newArt;

            AlbumArtOld.Opacity = 1;
            AlbumArtExpandedOld.Opacity = 1;
            AlbumArt.Opacity = 0;
            AlbumArtExpanded.Opacity = 0;

            var dur = TimeSpan.FromMilliseconds(300);
            AlbumArt.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            AlbumArtExpanded.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            AlbumArtOld.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, dur));
            AlbumArtExpandedOld.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, dur));
        }

        private void CrossfadeText(string title, string artist)
        {
            CompactTitleOld.Text = CompactTitle.Text;
            CompactArtistOld.Text = CompactArtist.Text;
            TrackTitleOld.Text = TrackTitle.Text;
            ArtistNameOld.Text = ArtistName.Text;

            CompactTitle.Text = title;
            CompactArtist.Text = artist;
            TrackTitle.Text = title;
            ArtistName.Text = artist;

            CompactTextOld.Opacity = 1;
            CompactTextNew.Opacity = 0;
            ExpandedTextOld.Opacity = 1;
            ExpandedTextNew.Opacity = 0;

            var dur = TimeSpan.FromMilliseconds(300);
            CompactTextNew.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            ExpandedTextNew.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, dur));
            CompactTextOld.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, dur));
            ExpandedTextOld.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, dur));
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
            SaveSettings();
            var menu = (System.Windows.Forms.ToolStripMenuItem)trayIcon.ContextMenuStrip.Items[0];
            ((System.Windows.Forms.ToolStripMenuItem)menu.DropDownItems[0]).Checked = top;
            ((System.Windows.Forms.ToolStripMenuItem)menu.DropDownItems[1]).Checked = !top;
        }
        
        private void UpdateWindowPosition()
        {
            if (customX >= 0 && customY >= 0)
            {
                Left = customX - Width / 2;
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
            var currentX = customX >= 0 ? customX : Left + Width / 2;
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
            
            SaveSettings();
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
            _progressTimer?.Stop();
            _pauseDebounceTimer?.Stop();
            SaveSettings();
            updateTimer?.Stop();
            trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}
