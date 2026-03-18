using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
                ApplyIslandBackground(GetConfiguredBackgroundColor(), backgroundOpacity);
                SetTheme(isDarkTheme, applyBackground: false);
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
            CompactSourceText.Text = musicInfo.SourceApp ?? "Media";
            ExpandedSourceText.Text = musicInfo.SourceApp ?? "Media";
            UpdateAlbumAccent(MusicVisualHelper.GetAccentColor(musicInfo.AlbumArt));
            lastMusicInfo = musicInfo;

            // Первый запуск без музыки — сразу PausedMode
            if (lastMusicInfo == null && !musicInfo.IsPlaying)
            {
                isPaused = true;
                var currentMode = displayMode == 0 ? MinimalMode : (displayMode == 1 ? CompactMode : ExpandedMode);
                currentMode.Visibility = Visibility.Collapsed;
                PausedMode.Visibility = Visibility.Visible;
            }

            if (musicInfo.Duration > TimeSpan.Zero)
                SetProgressRatio(musicInfo.Position.TotalSeconds / musicInfo.Duration.TotalSeconds);
            
            var gifVisible = musicInfo.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
            GifMinimalContainer.Visibility = gifVisible;
            GifCompactContainer.Visibility = gifVisible;
            GifExpandedContainer.Visibility = gifVisible;
            PlaybackIndicatorMinimal.Visibility = gifVisible;
            PlaybackIndicatorCompact.Visibility = gifVisible;
            PlaybackIndicatorExpanded.Visibility = gifVisible;

            if (musicInfo.IsPlaying)
            {
                StartRotation();
                PulseAlbumArt();
                SetProgressGlowStrength(0.9);
            }
            else
            {
                StopRotation();
                SetProgressGlowStrength(0.45);
            }

            PlayPauseButton.Content = musicInfo.IsPlaying ? "⏸" : "▶";
            this.Title = $"Dynamic Island PC - {musicInfo.Title} - {musicInfo.Artist}";
            UpdateTitleMarquee();

            // РџРµСЂРµРєР»СЋС‡Р°РµРј СЂРµР¶РёРј РїР°СѓР·С‹
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
            0 => (138, 60),
            1 => (335, 70),
            _ => (500, 176)
        };

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


        private void ShowTrackReveal(MusicInfo info)
        {
            RevealAlbumArt.Source = info.AlbumArt;
            RevealTitleText.Text = info.Title ?? "Unknown track";
            bool detailedReveal = displayMode == 2;
            RevealSubtitleText.Visibility = detailedReveal ? Visibility.Visible : Visibility.Collapsed;
            RevealSubtitleText.Text = detailedReveal
                ? (string.IsNullOrWhiteSpace(info.SourceApp)
                    ? (info.Artist ?? "Unknown artist")
                    : $"{info.Artist ?? "Unknown artist"} · {info.SourceApp}")
                : (info.Artist ?? info.SourceApp ?? string.Empty);

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
                pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(1.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
                pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));

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

        private void UpdateTitleMarquee()
        {
            UpdateSingleMarquee(CompactTitle, CompactTitleTranslate, ref _compactMarqueeStoryboard, 124);
            UpdateSingleMarquee(TrackTitle, TrackTitleTranslate, ref _expandedMarqueeStoryboard, 246);
            CompactTitleOldTranslate.X = 0;
            TrackTitleOldTranslate.X = 0;
        }

        private void UpdateSingleMarquee(TextBlock textBlock, TranslateTransform translate, ref Storyboard storyboard, double visibleWidth)
        {
            storyboard?.Stop();
            translate.X = 0;

            var estimatedWidth = (textBlock.Text?.Length ?? 0) * textBlock.FontSize * 0.58;
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

        private static void StopSlideAnimations(TranslateTransform oldTransform, TranslateTransform newTransform)
        {
            oldTransform.BeginAnimation(TranslateTransform.XProperty, null);
            newTransform.BeginAnimation(TranslateTransform.XProperty, null);
        }

        private static void ResetSlideLayerState(Grid oldLayer, Grid newLayer, TranslateTransform oldTransform, TranslateTransform newTransform)
        {
            StopSlideAnimations(oldTransform, newTransform);
            oldTransform.X = 0;
            newTransform.X = 0;
            oldLayer.Opacity = 0;
            newLayer.Opacity = 1;
        }
        
        private void SlideContent(MusicInfo info)
        {
            var dur = TimeSpan.FromMilliseconds(350);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var compactOffset = _slideDirection * Math.Max(CompactMode.ActualWidth, 1);
            var expandedOffset = _slideDirection * Math.Max(ExpandedMode.ActualWidth, 1);

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
            CompactTextOldContainer.Opacity = 0;
            ExpandedTextOldContainer.Opacity = 0;

            // Заполняем новый контент
            AlbumArt.Source = info.AlbumArt;
            AlbumArtExpanded.Source = info.AlbumArt;
            AlbumArtMinimal.Source = info.AlbumArt;
            CompactTitle.Text = info.Title ?? "Неизвестный трек";
            CompactArtist.Text = info.Artist ?? "Неизвестный исполнитель";
            TrackTitle.Text = info.Title ?? "Неизвестный трек";
            ArtistName.Text = info.Artist ?? "Неизвестный исполнитель";

            void Slide(Grid oldLayer, Grid newLayer, TranslateTransform oldTransform, TranslateTransform newTransform, double offset)
            {
                if (Math.Abs(offset) < 1)
                {
                    ResetSlideLayerState(oldLayer, newLayer, oldTransform, newTransform);
                    return;
                }

                oldLayer.Opacity = 1;
                newLayer.Opacity = 1;
                oldTransform.X = 0;
                newTransform.X = 0;
                CompactTextOldContainer.Opacity = 0;
                ExpandedTextOldContainer.Opacity = 0;

                var fadeOut = new DoubleAnimation(1, 0, dur) { EasingFunction = ease };
                var fadeIn = new DoubleAnimation(0.55, 1, dur) { EasingFunction = ease };
                fadeIn.Completed += (_, __) => ResetSlideLayerState(oldLayer, newLayer, oldTransform, newTransform);

                oldTransform.BeginAnimation(TranslateTransform.XProperty, null);
                newTransform.BeginAnimation(TranslateTransform.XProperty, null);
                oldLayer.BeginAnimation(OpacityProperty, fadeOut);
                newLayer.BeginAnimation(OpacityProperty, fadeIn);
            }

            Slide(CompactContentOld, CompactContentNew, CompactOldTranslate, CompactNewTranslate, compactOffset);
            Slide(ExpandedContentOld, ExpandedContentNew, ExpandedOldTranslate, ExpandedNewTranslate, expandedOffset);
        }
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            musicService.TogglePlayPause();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            _slideDirection = -1;
            musicService.NextTrack();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            _slideDirection = 1;
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
            SyncTrayMenuState();
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
            trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}




