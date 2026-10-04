using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DynamicIslandPC
{
    public partial class MainWindow : Window
    {
        private int displayMode = 0; // 0=minimal, 1=compact, 2=expanded
        private MusicInfoService musicService;
        private MusicInfo lastMusicInfo = null;
        private bool isPaused = false;
        private Storyboard rotationStoryboard;
        private int _slideDirection = -1;
        private Storyboard _pauseInStoryboard;
        private Storyboard _pauseOutStoryboard;
        private DispatcherTimer _pauseDebounceTimer;
        private System.Windows.Forms.NotifyIcon trayIcon;
        private Storyboard _revealStoryboard;
        private Storyboard _compactMarqueeStoryboard;
        private Storyboard _expandedMarqueeStoryboard;
        private Storyboard _modeStoryboard;
        private DateTime _lastModeSwitchAt = DateTime.MinValue;
        private bool isTopPosition = true;
        private double customX = -1;
        private double customY = -1;
        private bool hasCustomPosition = false;
        private bool isDarkTheme = true;
        private double scale = 1.0;
        private bool decorationEnabled = true;
        private string decorationMediaPath = "";
        private bool decorationIsVideo = false;
        private bool gamingModeEnabled = false;
        private bool lockModeEnabled = false;
        private bool startWithWindowsEnabled = false;
        private bool browserSourceEnabled = true;
        private System.Windows.Forms.ToolStripMenuItem trayTrackItem;
        private System.Windows.Forms.ToolStripMenuItem trayPlayPauseItem;
        private AppSettings _settings;
        private DispatcherTimer _progressTimer;
        private DispatcherTimer _topmostWatchdogTimer;
        private bool _isSmartHidden;
        private AudioVisualizerService _audioVisualizer;
        private volatile bool _visualizerUpdatePending = false;
        private volatile bool _musicInfoUpdatePending = false;
        private bool _isScrubbing = false;
        private Grid _activeScrubberContainer = null;
        private Point _dragStartPoint;
        private bool _isDraggingReady = false;
        
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowDisplayAffinity(IntPtr hWnd, out uint pdwAffinity);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint GW_HWNDPREV = 3;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_SPACE = 0x20;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        public MainWindow()
        {
            try
            {
                Logger.Log("Application starting...");
                InitializeComponent();
                Opacity = 0;
                _settings = SettingsService.Load();
                ApplySettings(_settings);
                ApplyStartupPreference();
                ApplyInitialDisplayMode();
                InitializeWindow();
                ApplyDisplayAffinity(excludeFromCapture);
                ApplyIslandBackground(GetConfiguredBackgroundColor(), backgroundOpacity);
                SetTheme(isDarkTheme, applyBackground: false);
                ApplyDecorationMedia();
                InitializeMusicService();
                RegisterGlobalHotkey();
                InitializeTrayIcon();
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
            PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            PreviewMouseMove += OnPreviewMouseMove;
            PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            
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

            Deactivated += (s, e) => EnsureTopmost();
            IsVisibleChanged += (s, e) => { if (IsVisible) EnsureTopmost(); };
            StateChanged += (s, e) => { if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; EnsureTopmost(); };
            InitializeTopmostWatchdog();
        }

        private MusicInfo _pendingMusicInfo = null;

        private void InitializeMusicService()
        {
            musicService = new MusicInfoService();
            musicService.SetBrowserSourceEnabled(browserSourceEnabled);
            musicService.MusicInfoChanged += info =>
            {
                _pendingMusicInfo = info;
                if (_musicInfoUpdatePending) return;
                _musicInfoUpdatePending = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    _musicInfoUpdatePending = false;
                    var latest = _pendingMusicInfo;
                    if (latest != null)
                    {
                        ApplyMusicInfo(latest);
                    }
                }));
            };

            _audioVisualizer = new AudioVisualizerService();
            _audioVisualizer.BandsUpdated += bands =>
            {
                if (_visualizerUpdatePending) return;
                _visualizerUpdatePending = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
                {
                    _visualizerUpdatePending = false;
                    UpdateVisualizerBars(bands);
                });
            };

            ApplyMusicInfo(musicService.GetCurrentMusicInfo());
            InitializeProgressTimer();
        }

        private void InitializeProgressTimer()
        {
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _progressTimer.Tick += (s, e) =>
            {
                if (_isScrubbing) return;
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
                dot.Margin = new Thickness(Math.Max(0, filled - (dot.Width / 2)), 0, 0, 0);
            }
            Apply(ProgressTrackCompact, ProgressBarCompact, ProgressDotCompact);
            Apply(ProgressTrackExpanded, ProgressBarExpanded, ProgressDotExpanded);

            if (lastMusicInfo != null)
            {
                CurrentTimeText.Text = FormatTime(lastMusicInfo.Position);
                var remaining = lastMusicInfo.Duration > TimeSpan.Zero
                    ? lastMusicInfo.Duration - lastMusicInfo.Position
                    : TimeSpan.Zero;
                if (remaining < TimeSpan.Zero)
                    remaining = TimeSpan.Zero;
                TotalTimeText.Text = "-" + FormatTime(remaining);
            }

        }

        private static string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return time.ToString(@"h\:mm\:ss");
            return time.ToString(@"m\:ss");
        }

        private void RegisterGlobalHotkey()
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            if (!RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL, VK_SPACE))
                Logger.Error("Failed to register global hotkey Ctrl+Space");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
            source.AddHook(HwndHook);
            ApplyDisplayAffinity(excludeFromCapture);
            ApplyClickThroughMode();
            WindowBlurHelper.SetAcrylicBlur(helper.Handle, false);
            EnsureTopmost();
        }

        public void EnsureTopmost()
        {
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                var hwnd = helper.Handle;
                if (hwnd == IntPtr.Zero || !IsVisible || _isSmartHidden)
                    return;

                IntPtr prevHwnd = GetWindow(hwnd, GW_HWNDPREV);
                if (prevHwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }

                if (!Topmost)
                {
                    Topmost = true;
                }
            }
            catch { }
        }

        private void InitializeTopmostWatchdog()
        {
            _topmostWatchdogTimer = new DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            _topmostWatchdogTimer.Tick += (s, e) => EnsureTopmost();
            _topmostWatchdogTimer.Start();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (_isSmartHidden || !IsVisible)
                {
                    ForceShowIsland();
                }
                else
                {
                    CycleDisplayMode(force: true);
                }
                handled = true;
            }
            return IntPtr.Zero;
        }
        
        private void CycleDisplayMode()
        {
            CycleDisplayMode(force: false);
        }

        private void CycleDisplayMode(bool force)
        {
            if (lockModeEnabled && !force)
                return;

            var now = DateTime.UtcNow;
            if ((now - _lastModeSwitchAt).TotalMilliseconds < 250)
                return;

            _lastModeSwitchAt = now;

            if (isPaused)
            {
                isPaused = false;
                _pauseDebounceTimer?.Stop();
                _pauseDebounceTimer = null;
                AnimateFromPaused();
                musicService?.TogglePlayPause();
                SaveSettings();
                return;
            }

            displayMode = (displayMode + 1) % 3;
            AnimateToMode();
            SaveSettings();
        }

        private void ApplyInitialDisplayMode()
        {
            displayMode = Math.Clamp(displayMode, 0, 2);
            var (baseWidth, baseHeight) = GetModeSize(displayMode);
            Width = baseWidth * scale;
            Height = baseHeight * scale;
            IslandBorder.CornerRadius = new CornerRadius(GetModeCornerRadius(displayMode) * scale);

            EnsureActiveDisplayModeVisible();
        }

        private void EnsureActiveDisplayModeVisible()
        {
            PausedMode.Visibility = Visibility.Collapsed;
            PausedMode.Opacity = 0;

            MinimalMode.Visibility = displayMode == 0 ? Visibility.Visible : Visibility.Collapsed;
            CompactMode.Visibility = displayMode == 1 ? Visibility.Visible : Visibility.Collapsed;
            ExpandedMode.Visibility = displayMode == 2 ? Visibility.Visible : Visibility.Collapsed;

            var activeGrid = GetDisplayModeGrid();
            activeGrid.Opacity = 1;
        }

        private void AnimateToMode()
        {
            _pauseInStoryboard?.Stop();
            _pauseOutStoryboard?.Stop();
            _pauseDebounceTimer?.Stop();
            _pauseDebounceTimer = null;
            isPaused = false;
            PausedMode.Visibility = Visibility.Collapsed;
            PausedMode.Opacity = 0;

            Grid currentMode = MinimalMode.Visibility == Visibility.Visible ? MinimalMode :
                              CompactMode.Visibility == Visibility.Visible ? CompactMode :
                              ExpandedMode.Visibility == Visibility.Visible ? ExpandedMode : null;
            Grid targetMode = displayMode == 0 ? MinimalMode : (displayMode == 1 ? CompactMode : ExpandedMode);

            var (baseWidth, baseHeight) = GetModeSize(displayMode);
            double targetWidth = baseWidth * scale;
            double targetHeight = baseHeight * scale;
            var (targetLeft, targetTop) = CalculateWindowPosition(targetWidth, targetHeight);

            if (currentMode == null)
            {
                EnsureActiveDisplayModeVisible();
                Width = targetWidth;
                Height = targetHeight;
                Left = targetLeft;
                Top = targetTop;
                IslandBorder.CornerRadius = new CornerRadius(GetModeCornerRadius(displayMode) * scale);
                return;
            }

            if (currentMode == targetMode &&
                Math.Abs(Left - targetLeft) < 1.0 &&
                Math.Abs(Top - targetTop) < 1.0 &&
                Math.Abs(Width - targetWidth) < 1.0 &&
                Math.Abs(Height - targetHeight) < 1.0)
            {
                EnsureActiveDisplayModeVisible();
                return;
            }

            double currentLeft = Left;
            double currentTop = Top;
            double currentWidth = Width;
            double currentHeight = Height;

            _modeStoryboard?.Stop();
            ResetModeAnimationState(currentMode);

            Left = currentLeft;
            Top = currentTop;
            Width = currentWidth;
            Height = currentHeight;

            var storyboard = new Storyboard();
            _modeStoryboard = storyboard;

            IslandBorder.CornerRadius = new CornerRadius(GetModeCornerRadius(displayMode) * scale);
            
            if (currentMode != targetMode)
            {
                targetMode.Visibility = Visibility.Visible;
                targetMode.Opacity = 0;
                
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(160),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(fadeOut, currentMode);
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
                storyboard.Children.Add(fadeOut);
                
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(220),
                    BeginTime = TimeSpan.FromMilliseconds(80),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(fadeIn, targetMode);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
                storyboard.Children.Add(fadeIn);
            }
            else
            {
                EnsureActiveDisplayModeVisible();
            }
            
            var widthAnimation = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(widthAnimation, this);
            Storyboard.SetTargetProperty(widthAnimation, new PropertyPath("Width"));
            storyboard.Children.Add(widthAnimation);
            
            var heightAnimation = new DoubleAnimation
            {
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(heightAnimation, this);
            Storyboard.SetTargetProperty(heightAnimation, new PropertyPath("Height"));
            storyboard.Children.Add(heightAnimation);
            
            var leftAnimation = new DoubleAnimation
            {
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(leftAnimation, this);
            Storyboard.SetTargetProperty(leftAnimation, new PropertyPath("Left"));
            storyboard.Children.Add(leftAnimation);
            
            var topAnimation = new DoubleAnimation
            {
                To = targetTop,
                Duration = TimeSpan.FromMilliseconds(450),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(topAnimation, this);
            Storyboard.SetTargetProperty(topAnimation, new PropertyPath("Top"));
            storyboard.Children.Add(topAnimation);
            
            storyboard.Completed += (s, e) =>
            {
                Left = targetLeft;
                Top = targetTop;
                Width = targetWidth;
                Height = targetHeight;
                BeginAnimation(LeftProperty, null);
                BeginAnimation(TopProperty, null);
                BeginAnimation(WidthProperty, null);
                BeginAnimation(HeightProperty, null);

                EnsureActiveDisplayModeVisible();
                _modeStoryboard = null;
                EnsureTopmost();
            };
            
            storyboard.Begin();
        }

        private void ResetModeAnimationState(Grid currentMode)
        {
            MinimalMode.BeginAnimation(OpacityProperty, null);
            CompactMode.BeginAnimation(OpacityProperty, null);
            ExpandedMode.BeginAnimation(OpacityProperty, null);
            PausedMode.BeginAnimation(OpacityProperty, null);

            PausedMode.Opacity = 0;
            PausedMode.Visibility = Visibility.Collapsed;

            MinimalMode.Opacity = currentMode == MinimalMode ? 1 : 0;
            CompactMode.Opacity = currentMode == CompactMode ? 1 : 0;
            ExpandedMode.Opacity = currentMode == ExpandedMode ? 1 : 0;

            MinimalMode.Visibility = currentMode == MinimalMode ? Visibility.Visible : Visibility.Collapsed;
            CompactMode.Visibility = currentMode == CompactMode ? Visibility.Visible : Visibility.Collapsed;
            ExpandedMode.Visibility = currentMode == ExpandedMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyMusicInfo(MusicInfo musicInfo)
        {
            if (musicInfo == null) return;
            
            bool hasDisplayableMusic = HasDisplayableMusic(musicInfo);
            if (!hasDisplayableMusic)
            {
                if (string.IsNullOrWhiteSpace(musicInfo.Title))
                    musicInfo.Title = "Нет воспроизведения";
                if (string.IsNullOrWhiteSpace(musicInfo.Artist))
                    musicInfo.Artist = "Запусти музыкальный плеер";
                if (string.IsNullOrWhiteSpace(musicInfo.SourceApp))
                    musicInfo.SourceApp = "Media";
            }

            musicInfo.Title = musicInfo.Title?.Trim();
            musicInfo.Artist = musicInfo.Artist?.Trim();

            var currentTrackId = $"{musicInfo.Artist}|{musicInfo.Title}";
            var prevTrackId = lastMusicInfo != null ? $"{lastMusicInfo.Artist}|{lastMusicInfo.Title}" : "";
            
            bool trackChanged = hasDisplayableMusic && currentTrackId != prevTrackId;

            if (trackChanged)
            {
                SlideContent(musicInfo);
                TrackRevealOverlay.Visibility = Visibility.Collapsed;
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

            AlbumArtPaused.Source = musicInfo.AlbumArt;
            ApplySourceVisuals(musicInfo.SourceApp);
            bool artUpdated = lastMusicInfo != null && 
                              lastMusicInfo.AlbumArt == MusicInfoService.DefaultAlbumArt && 
                              musicInfo.AlbumArt != null && 
                              musicInfo.AlbumArt != MusicInfoService.DefaultAlbumArt;

            if (trackChanged || lastMusicInfo == null || artUpdated)
            {
                UpdateAlbumPalette(MusicVisualHelper.GetAlbumPaletteCached(musicInfo.AlbumArt, $"{musicInfo.Title}|{musicInfo.Artist}|{musicInfo.SourceApp}"));
            }
            UpdateTrayController(musicInfo);
            lastMusicInfo = musicInfo;
            UpdateSmartVisibility(musicInfo, trackChanged);


            if (!_isScrubbing && musicInfo.Duration > TimeSpan.Zero)
                SetProgressRatio(musicInfo.Position.TotalSeconds / musicInfo.Duration.TotalSeconds);
            
            UpdateDecorationVisibility(musicInfo.IsPlaying);
            var gifVisible = musicInfo.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
            PlaybackIndicatorMinimal.Visibility = gifVisible;
            PlaybackIndicatorCompact.Visibility = gifVisible;
            PlaybackIndicatorExpanded.Visibility = gifVisible;

            if (musicInfo.IsPlaying)
            {
                StartRotation();
                PulseAlbumArt();
                SetProgressGlowStrength(0.9);
                _audioVisualizer?.Start();
            }
            else
            {
                StopRotation();
                SetProgressGlowStrength(0.45);
                _audioVisualizer?.Stop();
            }

            UpdatePlayPauseIcon(musicInfo.IsPlaying);
            this.Title = $"Dynamic Island PC - {musicInfo.Title} - {musicInfo.Artist}";
            UpdateTitleMarquee();

            if (!hasDisplayableMusic)
            {
                _pauseDebounceTimer?.Stop();
                _pauseDebounceTimer = null;
                if (isPaused)
                {
                    isPaused = false;
                    AnimateFromPaused();
                }
                else
                {
                    EnsureActiveDisplayModeVisible();
                }
                return;
            }

            if (musicInfo.IsPlaying)
            {
                _pauseDebounceTimer?.Stop();
                _pauseDebounceTimer = null;

                if (isPaused)
                {
                    isPaused = false;
                    AnimateFromPaused();
                }
                else
                {
                    EnsureActiveDisplayModeVisible();
                }
            }
            else
            {
                if (!isPaused)
                {
                    if (_pauseDebounceTimer == null)
                    {
                        _pauseDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
                        _pauseDebounceTimer.Tick += (s, e) =>
                        {
                            _pauseDebounceTimer.Stop();
                            _pauseDebounceTimer = null;
                            if (!isPaused && (lastMusicInfo == null || !lastMusicInfo.IsPlaying))
                            {
                                isPaused = true;
                                AnimateToPaused();
                            }
                        };
                        _pauseDebounceTimer.Start();
                    }
                }
            }
        }
        
        private (double width, double height) GetModeSize(int mode) => mode switch
        {
            0 => (138, 60),
            1 => (335, 70),
            _ => (500, 176)
        };

        private double GetModeCornerRadius(int mode) => mode switch
        {
            0 => 30,
            1 => 35,
            _ => 35
        };

        private Grid GetDisplayModeGrid() => displayMode switch
        {
            0 => MinimalMode,
            1 => CompactMode,
            _ => ExpandedMode
        };

        private void ShowDisplayModeWithoutAnimation()
        {
            EnsureActiveDisplayModeVisible();
        }

        private static bool HasDisplayableMusic(MusicInfo musicInfo)
        {
            return musicInfo?.HasMedia == true && !string.IsNullOrWhiteSpace(musicInfo.Title);
        }

        private void ApplySourceVisuals(string sourceApp)
        {
            var sourceName = MusicVisualHelper.NormalizeSource(sourceApp);
            var badgeLabel = MusicVisualHelper.GetSourceBadgeLabel(sourceName);
            var sourceColor = MusicVisualHelper.GetSourceColor(sourceName);

            SetSourceBadge(CompactSourceBadge, CompactSourceDot, CompactSourceText, badgeLabel, sourceColor);
            SetSourceBadge(ExpandedSourceBadge, ExpandedSourceDot, ExpandedSourceText, sourceName, sourceColor);
        }

        private void SetSourceBadge(Border badge, System.Windows.Shapes.Ellipse dot, TextBlock textBlock, string label, Color sourceColor)
        {
            var backgroundAlpha = isDarkTheme ? (byte)36 : (byte)28;
            var borderAlpha = isDarkTheme ? (byte)64 : (byte)58;
            badge.Background = new SolidColorBrush(Color.FromArgb(backgroundAlpha, sourceColor.R, sourceColor.G, sourceColor.B));
            badge.BorderBrush = new SolidColorBrush(Color.FromArgb(borderAlpha, sourceColor.R, sourceColor.G, sourceColor.B));
            dot.Fill = new SolidColorBrush(sourceColor);
            textBlock.Text = string.IsNullOrWhiteSpace(label) ? "Media" : label;
            textBlock.Foreground = isDarkTheme
                ? new SolidColorBrush(Color.FromRgb(230, 230, 238))
                : new SolidColorBrush(Color.FromRgb(28, 28, 34));
        }

        private void ApplyDecorationMedia()
        {
            var customMediaAvailable = !string.IsNullOrWhiteSpace(decorationMediaPath)
                && File.Exists(decorationMediaPath)
                && IsSupportedDecorationPath(decorationMediaPath);
            decorationIsVideo = customMediaAvailable && IsVideoDecorationPath(decorationMediaPath);

            StopDecorationVideos();

            if (decorationIsVideo)
            {
                var uri = new Uri(decorationMediaPath, UriKind.Absolute);
                SetDecorationVideo(DecorVideoMinimal, uri);
                SetDecorationVideo(DecorVideoCompact, uri);
                SetDecorationVideo(DecorVideoExpanded, uri);

                GifMinimal.Visibility = Visibility.Collapsed;
                GifCompact.Visibility = Visibility.Collapsed;
                GifExpanded.Visibility = Visibility.Collapsed;
                return;
            }

            var imageUri = customMediaAvailable
                ? new Uri(decorationMediaPath, UriKind.Absolute)
                : new Uri("pack://application:,,,/flex.gif", UriKind.Absolute);
            var isAnimatedGif = !customMediaAvailable || Path.GetExtension(decorationMediaPath).Equals(".gif", StringComparison.OrdinalIgnoreCase);

            try
            {
                SetDecorationImage(GifMinimal, imageUri, isAnimatedGif);
                SetDecorationImage(GifCompact, imageUri, isAnimatedGif);
                SetDecorationImage(GifExpanded, imageUri, isAnimatedGif);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load custom decoration image, falling back to flex.gif", ex);
                var fallbackUri = new Uri("pack://application:,,,/flex.gif", UriKind.Absolute);
                SetDecorationImage(GifMinimal, fallbackUri, true);
                SetDecorationImage(GifCompact, fallbackUri, true);
                SetDecorationImage(GifExpanded, fallbackUri, true);
            }

            DecorVideoMinimal.Visibility = Visibility.Collapsed;
            DecorVideoCompact.Visibility = Visibility.Collapsed;
            DecorVideoExpanded.Visibility = Visibility.Collapsed;
            GifMinimal.Visibility = Visibility.Visible;
            GifCompact.Visibility = Visibility.Visible;
            GifExpanded.Visibility = Visibility.Visible;
        }

        private static bool IsVideoDecorationPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".mp4" or ".m4v" or ".mov" or ".wmv" or ".avi" or ".webm" or ".mkv";
        }

        private static bool IsSupportedDecorationPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif"
                or ".mp4" or ".m4v" or ".mov" or ".wmv" or ".avi" or ".webm" or ".mkv";
        }

        private static void SetDecorationImage(Image image, Uri uri, bool isAnimatedGif)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = uri;
            bitmap.EndInit();

            if (isAnimatedGif)
            {
                WpfAnimatedGif.ImageBehavior.SetAnimatedSource(image, bitmap);
            }
            else
            {
                WpfAnimatedGif.ImageBehavior.SetAnimatedSource(image, null);
                image.Source = bitmap;
            }
        }

        private static void SetDecorationVideo(MediaElement video, Uri uri)
        {
            video.Source = uri;
            video.Position = TimeSpan.Zero;
        }

        private void UpdateDecorationVisibility(bool isPlaying)
        {
            var isVisible = decorationEnabled && isPlaying;
            var visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            GifMinimalContainer.Visibility = visibility;
            GifCompactContainer.Visibility = visibility;
            GifExpandedContainer.Visibility = visibility;

            if (decorationIsVideo)
            {
                GifMinimal.Visibility = Visibility.Collapsed;
                GifCompact.Visibility = Visibility.Collapsed;
                GifExpanded.Visibility = Visibility.Collapsed;
                SetDecorationVideoVisibility(DecorVideoMinimal, isVisible);
                SetDecorationVideoVisibility(DecorVideoCompact, isVisible);
                SetDecorationVideoVisibility(DecorVideoExpanded, isVisible);
            }
            else
            {
                StopDecorationVideos();
                GifMinimal.Visibility = Visibility.Visible;
                GifCompact.Visibility = Visibility.Visible;
                GifExpanded.Visibility = Visibility.Visible;
                DecorVideoMinimal.Visibility = Visibility.Collapsed;
                DecorVideoCompact.Visibility = Visibility.Collapsed;
                DecorVideoExpanded.Visibility = Visibility.Collapsed;
            }
        }

        private static void SetDecorationVideoVisibility(MediaElement video, bool isVisible)
        {
            video.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            try
            {
                if (isVisible)
                    video.Play();
                else
                    video.Pause();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update decoration video playback", ex);
            }
        }

        private void StopDecorationVideos()
        {
            StopDecorationVideo(DecorVideoMinimal);
            StopDecorationVideo(DecorVideoCompact);
            StopDecorationVideo(DecorVideoExpanded);
        }

        private static void StopDecorationVideo(MediaElement video)
        {
            try
            {
                video.Stop();
                video.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to stop decoration video", ex);
            }
        }

        private void DecorationVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (sender is not MediaElement video || !decorationIsVideo)
                return;

            video.Position = TimeSpan.Zero;
            video.Play();
        }

        private void UpdateSmartVisibility(MusicInfo musicInfo, bool trackChanged)
        {
            if (!IsVisible)
            {
                Opacity = 1;
                Show();
            }
            EnsureTopmost();
        }

        private void EnsureSmartHideTimer()
        {
        }

        private void ScheduleSmartHide(TimeSpan delay)
        {
        }

        private void ForceShowIsland()
        {
            if (!IsVisible)
            {
                Opacity = 1;
                Show();
            }
            EnsureTopmost();
            CycleDisplayMode(force: true);
        }

        private void ShowIslandFromSmartHide(bool trackChanged)
        {
            _isSmartHidden = false;
            if (!IsVisible)
            {
                Opacity = 1;
                Show();
            }

            Topmost = true;
            EnsureTopmost();
            if (trackChanged)
                PulseAlbumArt();
        }

        private void HideIslandForSmartState()
        {
            _isSmartHidden = false;
        }

        private void AnimateToPaused()
        {
            _audioVisualizer?.Stop();
            _modeStoryboard?.Stop();
            _modeStoryboard = null;
            _pauseOutStoryboard?.Stop();
            _pauseInStoryboard?.Stop();

            var pausedSize = 60 * scale;
            var (targetLeft, targetTop) = CalculateWindowPosition(pausedSize, pausedSize);

            var currentMode = MinimalMode.Visibility == Visibility.Visible ? MinimalMode :
                              CompactMode.Visibility == Visibility.Visible ? CompactMode :
                              ExpandedMode.Visibility == Visibility.Visible ? ExpandedMode : null;

            if (currentMode == null)
            {
                MinimalMode.Visibility = Visibility.Collapsed;
                CompactMode.Visibility = Visibility.Collapsed;
                ExpandedMode.Visibility = Visibility.Collapsed;
                MinimalMode.Opacity = 0;
                CompactMode.Opacity = 0;
                ExpandedMode.Opacity = 0;

                PausedMode.Visibility = Visibility.Visible;
                PausedMode.Opacity = 1;
                IslandBorder.CornerRadius = new CornerRadius(30 * scale);
                Width = pausedSize;
                Height = pausedSize;
                Left = targetLeft;
                Top = targetTop;
                return;
            }

            var fadeOut = new DoubleAnimation { From = currentMode.Opacity, To = 0, Duration = TimeSpan.FromMilliseconds(200) };
            fadeOut.Completed += (s, e) =>
            {
                MinimalMode.Visibility = Visibility.Collapsed;
                CompactMode.Visibility = Visibility.Collapsed;
                ExpandedMode.Visibility = Visibility.Collapsed;
                MinimalMode.Opacity = 0;
                CompactMode.Opacity = 0;
                ExpandedMode.Opacity = 0;

                PausedMode.Visibility = Visibility.Visible;
                PausedMode.Opacity = 0;

                _pauseInStoryboard = new Storyboard();

                var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(200) };
                Storyboard.SetTarget(fadeIn, PausedMode);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
                _pauseInStoryboard.Children.Add(fadeIn);

                IslandBorder.CornerRadius = new CornerRadius(30 * scale);
                var w = new DoubleAnimation { To = pausedSize, Duration = TimeSpan.FromMilliseconds(380), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(w, this);
                Storyboard.SetTargetProperty(w, new PropertyPath("Width"));
                _pauseInStoryboard.Children.Add(w);

                var h = new DoubleAnimation { To = pausedSize, Duration = TimeSpan.FromMilliseconds(380), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(h, this);
                Storyboard.SetTargetProperty(h, new PropertyPath("Height"));
                _pauseInStoryboard.Children.Add(h);

                var l = new DoubleAnimation { To = targetLeft, Duration = TimeSpan.FromMilliseconds(380), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(l, this);
                Storyboard.SetTargetProperty(l, new PropertyPath("Left"));
                _pauseInStoryboard.Children.Add(l);

                var t = new DoubleAnimation { To = targetTop, Duration = TimeSpan.FromMilliseconds(380), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(t, this);
                Storyboard.SetTargetProperty(t, new PropertyPath("Top"));
                _pauseInStoryboard.Children.Add(t);

                _pauseInStoryboard.Begin();
            };
            currentMode.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void AnimateFromPaused()
        {
            if (lastMusicInfo != null && lastMusicInfo.IsPlaying)
            {
                _audioVisualizer?.Start();
            }
            var targetMode = GetDisplayModeGrid();
            var (baseWidth, baseHeight) = GetModeSize(displayMode);
            var tw = baseWidth * scale;
            var th = baseHeight * scale;
            var (targetLeft, targetTop) = CalculateWindowPosition(tw, th);

            _modeStoryboard?.Stop();
            _modeStoryboard = null;
            _pauseInStoryboard?.Stop();
            _pauseOutStoryboard?.Stop();
            _pauseOutStoryboard = new Storyboard();

            var fadeOut = new DoubleAnimation { From = PausedMode.Opacity, To = 0, Duration = TimeSpan.FromMilliseconds(180) };
            fadeOut.Completed += (s, e) =>
            {
                PausedMode.Visibility = Visibility.Collapsed;
                PausedMode.Opacity = 0;

                MinimalMode.Visibility = targetMode == MinimalMode ? Visibility.Visible : Visibility.Collapsed;
                CompactMode.Visibility = targetMode == CompactMode ? Visibility.Visible : Visibility.Collapsed;
                ExpandedMode.Visibility = targetMode == ExpandedMode ? Visibility.Visible : Visibility.Collapsed;
                targetMode.Opacity = 0;

                IslandBorder.CornerRadius = new CornerRadius(GetModeCornerRadius(displayMode) * scale);

                var sb = new Storyboard();

                var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(250) };
                Storyboard.SetTarget(fadeIn, targetMode);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
                sb.Children.Add(fadeIn);

                var w = new DoubleAnimation { To = tw, Duration = TimeSpan.FromMilliseconds(380), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(w, this);
                Storyboard.SetTargetProperty(w, new PropertyPath("Width"));
                sb.Children.Add(w);

                var h = new DoubleAnimation { To = th, Duration = TimeSpan.FromMilliseconds(380), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(h, this);
                Storyboard.SetTargetProperty(h, new PropertyPath("Height"));
                sb.Children.Add(h);

                var l = new DoubleAnimation { To = targetLeft, Duration = TimeSpan.FromMilliseconds(380), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(l, this);
                Storyboard.SetTargetProperty(l, new PropertyPath("Left"));
                sb.Children.Add(l);

                var t = new DoubleAnimation { To = targetTop, Duration = TimeSpan.FromMilliseconds(380), EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut } };
                Storyboard.SetTarget(t, this);
                Storyboard.SetTargetProperty(t, new PropertyPath("Top"));
                sb.Children.Add(t);

                sb.Completed += (_, __) =>
                {
                    EnsureActiveDisplayModeVisible();
                    if (lastMusicInfo?.Duration > TimeSpan.Zero)
                        SetProgressRatio(lastMusicInfo.Position.TotalSeconds / lastMusicInfo.Duration.TotalSeconds);
                };
                sb.Begin();
            };
            PausedMode.BeginAnimation(OpacityProperty, fadeOut);
        }


        private void ShowTrackReveal(MusicInfo info)
        {
            RevealAlbumArt.Source = info.AlbumArt;
            RevealTitleText.Text = info.Title ?? "Неизвестный трек";
            RevealSubtitleText.Text = info.Artist ?? "Неизвестный исполнитель";
            RevealSourceText.Text = !string.IsNullOrWhiteSpace(info.SourceApp) ? info.SourceApp : string.Empty;
            RevealSourceText.Visibility = string.IsNullOrWhiteSpace(RevealSourceText.Text) ? Visibility.Collapsed : Visibility.Visible;

            _revealStoryboard?.Stop();
            TrackRevealOverlay.Visibility = Visibility.Visible;
            TrackRevealOverlay.Opacity = 0;
            TrackRevealOverlay.RenderTransform = new TranslateTransform(0, 6);

            _revealStoryboard = new Storyboard();
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
            var hold = new DoubleAnimation(1, 1, TimeSpan.FromMilliseconds(900)) { BeginTime = TimeSpan.FromMilliseconds(180) };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(1080) };
            var slideIn = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var slideOut = new DoubleAnimation(0, -4, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(1080), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
            fadeOut.Completed += (_, __) => TrackRevealOverlay.Visibility = Visibility.Collapsed;

            Storyboard.SetTarget(fadeIn, TrackRevealOverlay);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
            Storyboard.SetTarget(hold, TrackRevealOverlay);
            Storyboard.SetTargetProperty(hold, new PropertyPath("Opacity"));
            Storyboard.SetTarget(fadeOut, TrackRevealOverlay);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
            Storyboard.SetTarget(slideIn, TrackRevealOverlay);
            Storyboard.SetTargetProperty(slideIn, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            Storyboard.SetTarget(slideOut, TrackRevealOverlay);
            Storyboard.SetTargetProperty(slideOut, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            _revealStoryboard.Children.Add(fadeIn);
            _revealStoryboard.Children.Add(hold);
            _revealStoryboard.Children.Add(fadeOut);
            _revealStoryboard.Children.Add(slideIn);
            _revealStoryboard.Children.Add(slideOut);
            _revealStoryboard.Begin();
        }

        private void PulseAlbumArt()
        {
            void ApplyPulse(UIElement element)
            {
                element.RenderTransformOrigin = new Point(0.5, 0.5);
                if (element.RenderTransform is not ScaleTransform scale)
                {
                    scale = new ScaleTransform(1, 1);
                    element.RenderTransform = scale;
                }

                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

                var pulseX = new DoubleAnimationUsingKeyFrames();
                pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(1.03, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(460)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });

                var pulseY = pulseX.Clone();
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseX);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseY);
            }

            ApplyPulse(AlbumArtMinimal);
            ApplyPulse(AlbumArt);
            ApplyPulse(AlbumArtExpanded);
        }

        private void SetProgressGlowStrength(double opacity)
        {
            if (ProgressBarCompact.Effect is System.Windows.Media.Effects.DropShadowEffect compactGlow)
                compactGlow.Opacity = opacity * 0.65;
            if (ProgressBarExpanded.Effect is System.Windows.Media.Effects.DropShadowEffect expandedGlow)
                expandedGlow.Opacity = opacity * 0.55;
        }

        private string _lastCompactMarqueeText;
        private string _lastExpandedMarqueeText;

        private void UpdateTitleMarquee()
        {
            UpdateSingleMarquee(CompactTitle, CompactTitleTranslate, ref _compactMarqueeStoryboard, ref _lastCompactMarqueeText, 124);
            UpdateSingleMarquee(TrackTitle, TrackTitleTranslate, ref _expandedMarqueeStoryboard, ref _lastExpandedMarqueeText, 246);
            CompactTitleOldTranslate.X = 0;
            TrackTitleOldTranslate.X = 0;
        }

        private void StopTitleMarquee()
        {
            _compactMarqueeStoryboard?.Stop();
            _compactMarqueeStoryboard = null;
            _expandedMarqueeStoryboard?.Stop();
            _expandedMarqueeStoryboard = null;
            CompactTitleTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            TrackTitleTranslate.BeginAnimation(TranslateTransform.XProperty, null);
            CompactTitleTranslate.X = 0;
            TrackTitleTranslate.X = 0;
            _lastCompactMarqueeText = null;
            _lastExpandedMarqueeText = null;
            CompactTitleOldTranslate.X = 0;
            TrackTitleOldTranslate.X = 0;
        }

        private void UpdateSingleMarquee(TextBlock textBlock, TranslateTransform translate, ref Storyboard storyboard, ref string lastText, double visibleWidth)
        {
            var text = textBlock.Text;
            if (string.Equals(text, lastText, StringComparison.Ordinal) && storyboard != null)
                return;

            lastText = text;
            storyboard?.Stop();
            storyboard = null;
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.X = 0;

            var estimatedWidth = (text?.Length ?? 0) * textBlock.FontSize * 0.58;
            if (estimatedWidth <= visibleWidth + 18)
                return;

            var shift = Math.Min(estimatedWidth - visibleWidth + 12, visibleWidth * 0.85);
            storyboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1100))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(-shift, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3900))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } });
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(-shift, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(5100))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(5110))));

            Storyboard.SetTarget(animation, translate);
            Storyboard.SetTargetProperty(animation, new PropertyPath(TranslateTransform.XProperty));
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        private void StartRotation()
        {
            // Dynamic Island album covers stay upright and sharp
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

        private static void StopSlideAnimations(TranslateTransform oldTransform, TranslateTransform newTransform)
        {
            oldTransform.BeginAnimation(TranslateTransform.XProperty, null);
            newTransform.BeginAnimation(TranslateTransform.XProperty, null);
        }

        private static void ResetSlideLayerState(Grid oldLayer, Grid newLayer, TranslateTransform oldTransform, TranslateTransform newTransform)
        {
            StopSlideAnimations(oldTransform, newTransform);
            oldLayer.BeginAnimation(OpacityProperty, null);
            newLayer.BeginAnimation(OpacityProperty, null);
            oldTransform.X = 0;
            newTransform.X = 0;
            oldLayer.Opacity = 0;
            newLayer.Opacity = 1;
        }
        
        private void SlideContent(MusicInfo info)
        {
            var enterDur = TimeSpan.FromMilliseconds(460);
            var exitDur = TimeSpan.FromMilliseconds(340);
            var smoothEase = new CubicEase { EasingMode = EasingMode.EaseOut };

            var dir = _slideDirection == 0 ? -1 : _slideDirection;
            var compactExitX = dir * 20.0;
            var compactEnterX = -dir * 24.0;

            var expandedExitX = dir * 24.0;
            var expandedEnterX = -dir * 28.0;

            ResetSlideLayerState(CompactContentOld, CompactContentNew, CompactOldTranslate, CompactNewTranslate);
            ResetSlideLayerState(ExpandedContentOld, ExpandedContentNew, ExpandedOldTranslate, ExpandedNewTranslate);

            // Заполняем старый контент текущими данными
            AlbumArtOld.Source = AlbumArt.Source;
            AlbumArtExpandedOld.Source = AlbumArtExpanded.Source;
            CompactTitleOld.Text = CompactTitle.Text;
            CompactArtistOld.Text = CompactArtist.Text;
            CompactSourceTextOld.Text = CompactSourceText.Text;
            TrackTitleOld.Text = TrackTitle.Text;
            ArtistNameOld.Text = ArtistName.Text;
            ExpandedSourceTextOld.Text = ExpandedSourceText.Text;
            CompactTextOldContainer.Opacity = 1;
            ExpandedTextOldContainer.Opacity = 1;

            // Заполняем новый контент
            AlbumArt.Source = info.AlbumArt;
            AlbumArtExpanded.Source = info.AlbumArt;
            AlbumArtMinimal.Source = info.AlbumArt;
            CompactTitle.Text = info.Title ?? "Неизвестный трек";
            CompactArtist.Text = info.Artist ?? "Неизвестный исполнитель";
            TrackTitle.Text = info.Title ?? "Неизвестный трек";
            ArtistName.Text = info.Artist ?? "Неизвестный исполнитель";

            void Slide(Grid oldLayer, Grid newLayer, TranslateTransform oldTransform, TranslateTransform newTransform, double exitX, double enterX)
            {
                oldLayer.Opacity = 1;
                newLayer.Opacity = 0;
                oldTransform.X = 0;
                newTransform.X = enterX;

                // Старый трек мягко уплывает со сглаженным затуханием
                var exitAnim = new DoubleAnimation(0, exitX, exitDur) { EasingFunction = smoothEase };
                var fadeOut = new DoubleAnimation(1, 0, exitDur) { EasingFunction = smoothEase };

                // Новый трек плавно вплывает на место
                var enterAnim = new DoubleAnimation(enterX, 0, enterDur) { EasingFunction = smoothEase };
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380)) { EasingFunction = smoothEase };

                enterAnim.Completed += (_, __) =>
                {
                    ResetSlideLayerState(oldLayer, newLayer, oldTransform, newTransform);
                    AlbumArtOld.Source = null;
                    AlbumArtExpandedOld.Source = null;
                    _slideDirection = -1;
                };

                oldTransform.BeginAnimation(TranslateTransform.XProperty, exitAnim);
                newTransform.BeginAnimation(TranslateTransform.XProperty, enterAnim);
                oldLayer.BeginAnimation(OpacityProperty, fadeOut);
                newLayer.BeginAnimation(OpacityProperty, fadeIn);
            }

            Slide(CompactContentOld, CompactContentNew, CompactOldTranslate, CompactNewTranslate, compactExitX, compactEnterX);
            Slide(ExpandedContentOld, ExpandedContentNew, ExpandedOldTranslate, ExpandedNewTranslate, expandedExitX, expandedEnterX);

            PulseAlbumArt();
        }
        
        private const string PlayPathData = "M 5 3 L 16 10 L 5 17 Z";
        private const string PausePathData = "M 4 3 L 7 3 L 7 15 L 4 15 Z M 11 3 L 14 3 L 14 15 L 11 15 Z";

        private void UpdatePlayPauseIcon(bool isPlaying)
        {
            if (PlayPauseIcon != null)
            {
                PlayPauseIcon.Data = Geometry.Parse(isPlaying ? PausePathData : PlayPathData);
                PlayPauseIcon.Margin = isPlaying ? new Thickness(0) : new Thickness(2, 0, 0, 0);
            }
        }

        private void UpdateEqualizerColors(Color accentColor)
        {
            double luminance = (0.299 * accentColor.R + 0.587 * accentColor.G + 0.114 * accentColor.B) / 255.0;
            Color eqColor = luminance < 0.35
                ? Color.FromRgb(110, 225, 127) // #6EE17F vibrant live green
                : accentColor;

            var brush = new SolidColorBrush(eqColor);
            SetEqualizerFill(PlaybackIndicatorMinimal, brush);
            SetEqualizerFill(PlaybackIndicatorCompact, brush);
            SetEqualizerFill(PlaybackIndicatorExpanded, brush);
        }

        private static void SetEqualizerFill(Panel panel, Brush brush)
        {
            if (panel == null) return;
            foreach (var child in panel.Children)
            {
                if (child is System.Windows.Shapes.Rectangle rect)
                {
                    rect.Fill = brush;
                }
            }
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            musicService.TogglePlayPause();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            _slideDirection = -1;
            musicService.NextTrack();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            _slideDirection = 1;
            musicService.PreviousTrack();
        }

        private void DiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            OpenDiagnostics();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            OpenSettings();
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

            var gamingModeItem = new System.Windows.Forms.ToolStripMenuItem("Игровой режим (клики насквозь)", null, (s, e) => SetGamingMode(!gamingModeEnabled))
            {
                CheckOnClick = false,
                Checked = gamingModeEnabled
            };
            contextMenu.Items.Add(gamingModeItem);

            var lockModeItem = new System.Windows.Forms.ToolStripMenuItem("Зафиксировать размер (без клика)", null, (s, e) => SetLockMode(!lockModeEnabled))
            {
                CheckOnClick = false,
                Checked = lockModeEnabled
            };
            contextMenu.Items.Add(lockModeItem);

            var startupItem = new System.Windows.Forms.ToolStripMenuItem("Автозапуск с Windows", null, (s, e) => SetStartWithWindows(!startWithWindowsEnabled))
            {
                CheckOnClick = false,
                Checked = startWithWindowsEnabled
            };
            contextMenu.Items.Add(startupItem);

            var sourcesMenu = new System.Windows.Forms.ToolStripMenuItem("Источники");
            var browserSourceItem = new System.Windows.Forms.ToolStripMenuItem("Браузер", null, (s, e) => SetBrowserSourceEnabled(!browserSourceEnabled))
            {
                CheckOnClick = false,
                Checked = browserSourceEnabled
            };
            sourcesMenu.DropDownItems.Add(browserSourceItem);
            contextMenu.Items.Add(sourcesMenu);

            var controllerMenu = new System.Windows.Forms.ToolStripMenuItem("Управление музыкой");
            trayTrackItem = new System.Windows.Forms.ToolStripMenuItem("Нет медиа") { Enabled = false };
            var prevItem = new System.Windows.Forms.ToolStripMenuItem("Предыдущий трек", null, (s, e) => musicService?.PreviousTrack());
            trayPlayPauseItem = new System.Windows.Forms.ToolStripMenuItem("Воспроизведение / Пауза", null, (s, e) => musicService?.TogglePlayPause());
            var nextItem = new System.Windows.Forms.ToolStripMenuItem("Следующий трек", null, (s, e) => musicService?.NextTrack());
            controllerMenu.DropDownItems.Add(trayTrackItem);
            controllerMenu.DropDownItems.Add(new System.Windows.Forms.ToolStripSeparator());
            controllerMenu.DropDownItems.Add(prevItem);
            controllerMenu.DropDownItems.Add(trayPlayPauseItem);
            controllerMenu.DropDownItems.Add(nextItem);
            contextMenu.Items.Add(controllerMenu);

            contextMenu.Items.Add("Диагностика...", null, (s, e) => OpenDiagnostics());
            contextMenu.Items.Add("Проверить обновления...", null, async (s, e) => await CheckForUpdatesAsync());
            
            contextMenu.Items.Add("Выход", null, (s, e) => 
            {
                trayIcon.Visible = false;
                Application.Current.Shutdown();
            });
            trayIcon.ContextMenuStrip = contextMenu;
            SyncTrayMenuState();
        }

        private void OpenDiagnostics()
        {
            var diagnosticsWindow = new DiagnosticsWindow(() => musicService?.GetDiagnostics() ?? "Music service is not initialized.");
            diagnosticsWindow.Owner = this;
            diagnosticsWindow.Show();
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var info = await UpdateChecker.CheckAsync();
                if (info.HasUpdate)
                {
                    if (!string.IsNullOrEmpty(info.ExeDownloadUrl))
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"Доступна новая версия: {info.LatestVersion} (текущая: {info.CurrentVersion})\n\nОбновить приложение автоматически прямо сейчас?\n\n(Нажмите 'Да' для моментального авто-обновления, или 'Нет', чтобы открыть релиз на GitHub)",
                            "Dynamic Island PC - Обновление",
                            System.Windows.MessageBoxButton.YesNoCancel,
                            System.Windows.MessageBoxImage.Information);

                        if (result == System.Windows.MessageBoxResult.Yes)
                        {
                            try
                            {
                                await UpdateChecker.DownloadAndInstallAsync(info);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error("Auto-update failed", ex);
                                System.Windows.MessageBox.Show(
                                    $"Ошибка при автоматическом обновлении: {ex.Message}\nОткрываем страницу на GitHub...",
                                    "Dynamic Island PC",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Warning);
                                UpdateChecker.OpenRelease(info);
                            }
                        }
                        else if (result == System.Windows.MessageBoxResult.No)
                        {
                            UpdateChecker.OpenRelease(info);
                        }
                    }
                    else
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"Доступна новая версия: {info.LatestVersion}\nТекущая версия: {info.CurrentVersion}\n\nОткрыть страницу релиза на GitHub?",
                            "Dynamic Island PC",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Information);

                        if (result == System.Windows.MessageBoxResult.Yes)
                            UpdateChecker.OpenRelease(info);
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"У вас установлена последняя версия: {info.CurrentVersion}",
                        "Dynamic Island PC",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to check for updates", ex);
                System.Windows.MessageBox.Show(
                    "Не удалось проверить обновления. Подробности записаны в лог.",
                    "Dynamic Island PC",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
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
            storyboard.Completed += (s, e) => { Opacity = 1; };
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
                e.Handled = true;
            }
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (lockModeEnabled)
                return;

            if (e.OriginalSource is DependencyObject dep && IsInteractiveControl(dep))
                return;

            _dragStartPoint = e.GetPosition(this);
            _isDraggingReady = true;
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingReady && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(this);
                Vector diff = currentPoint - _dragStartPoint;

                if (Math.Abs(diff.X) > 6 || Math.Abs(diff.Y) > 6)
                {
                    _isDraggingReady = false;
                    try
                    {
                        this.Cursor = System.Windows.Input.Cursors.SizeAll;
                        DragMove();

                        var centerPoint = new System.Drawing.Point((int)Math.Round(this.Left + this.Width / 2.0), (int)Math.Round(this.Top + this.Height / 2.0));
                        var dropScreen = System.Windows.Forms.Screen.FromPoint(centerPoint) ?? System.Windows.Forms.Screen.PrimaryScreen;
                        if (dropScreen != null)
                        {
                            targetMonitorDeviceName = dropScreen.DeviceName;
                            var wa = dropScreen.WorkingArea;
                            var maxLeft = Math.Max(wa.Left, wa.Right - this.Width);
                            var maxTop = Math.Max(wa.Top, wa.Bottom - this.Height);
                            this.Left = Math.Clamp(this.Left, wa.Left, maxLeft);
                            this.Top = Math.Clamp(this.Top, wa.Top, maxTop);
                        }

                        customX = this.Left + this.Width / 2.0;
                        customY = this.Top;
                        hasCustomPosition = true;

                        SaveSettings();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("DragMove failed", ex);
                    }
                    finally
                    {
                        this.Cursor = System.Windows.Input.Cursors.Arrow;
                    }
                }
            }
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Arrow;
            if (_isDraggingReady)
            {
                _isDraggingReady = false;
                CycleDisplayMode();
                e.Handled = true;
            }
        }

        private static bool IsInteractiveControl(DependencyObject dep)
        {
            while (dep != null && dep is not Window)
            {
                if (dep is Button || dep is System.Windows.Controls.Primitives.ButtonBase || dep is TextBox)
                    return true;
                if (dep is FrameworkElement fe && (fe.Name == "ProgressContainerCompact" || fe.Name == "ProgressContainerExpanded"))
                    return true;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        private void ProgressContainer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid grid && lastMusicInfo != null && lastMusicInfo.Duration > TimeSpan.Zero)
            {
                _isScrubbing = true;
                _activeScrubberContainer = grid;
                grid.CaptureMouse();
                e.Handled = true;
                HandleScrub(grid, e.GetPosition(grid).X, commit: false);
            }
        }

        private void ProgressContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isScrubbing && _activeScrubberContainer != null && e.LeftButton == MouseButtonState.Pressed)
            {
                HandleScrub(_activeScrubberContainer, e.GetPosition(_activeScrubberContainer).X, commit: false);
                e.Handled = true;
            }
        }

        private void ProgressContainer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isScrubbing && _activeScrubberContainer != null)
            {
                _activeScrubberContainer.ReleaseMouseCapture();
                HandleScrub(_activeScrubberContainer, e.GetPosition(_activeScrubberContainer).X, commit: true);
                _isScrubbing = false;
                _activeScrubberContainer = null;
                e.Handled = true;
            }
        }

        private void HandleScrub(Grid container, double mouseX, bool commit = false)
        {
            if (lastMusicInfo == null || lastMusicInfo.Duration <= TimeSpan.Zero) return;
            double width = container.ActualWidth;
            if (width <= 0) return;

            double ratio = Math.Clamp(mouseX / width, 0.0, 1.0);
            SetProgressRatio(ratio);

            var newPos = TimeSpan.FromSeconds(ratio * lastMusicInfo.Duration.TotalSeconds);
            lastMusicInfo.Position = newPos;
            CurrentTimeText.Text = FormatTime(newPos);
            var remaining = lastMusicInfo.Duration - newPos;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            TotalTimeText.Text = "-" + FormatTime(remaining);

            if (commit)
            {
                _ = musicService.SeekToRatioAsync(ratio);
            }
        }

        private void UpdateVisualizerBars(float[] bands)
        {
            if (bands == null || bands.Length < 4) return;
            UpdateIndicatorBars(PlaybackIndicatorMinimal, bands, 4, 18);
            UpdateIndicatorBars(PlaybackIndicatorCompact, bands, 4, 18);
            UpdateIndicatorBars(PlaybackIndicatorExpanded, bands, 4, 20);
        }

        private void UpdateIndicatorBars(StackPanel indicator, float[] bands, double minH, double maxH)
        {
            if (indicator == null || indicator.Visibility != Visibility.Visible) return;
            for (int i = 0; i < 4 && i < indicator.Children.Count; i++)
            {
                if (indicator.Children[i] is System.Windows.Shapes.Rectangle rect)
                {
                    double val = bands[i];
                    rect.Height = minH + val * (maxH - minH);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
                source?.RemoveHook(HwndHook);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
            }
            _audioVisualizer?.Dispose();
            _progressTimer?.Stop();
            _pauseDebounceTimer?.Stop();
            _topmostWatchdogTimer?.Stop();
            StopTitleMarquee();
            SaveSettings();
            trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}




