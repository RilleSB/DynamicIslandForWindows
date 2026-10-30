using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
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
        private IslandModeController _islandModeController;
        private PlaybackUiController _playbackUiController;
        private MusicInfoService musicService;
        private MusicInfo lastMusicInfo = null;
        private bool isPaused = false;
        private AlbumArtPresenter _albumArtPresenter;
        private bool _isPeekingDrop => _islandModeController?.IsPeekingDrop == true;
        private DispatcherTimer _pauseDebounceTimer;
        private DispatcherTimer _startupTrimTimer;
        private TrayMenuController _trayMenuController;
        private TrackTextPresenter _trackTextPresenter;
        private bool isTopPosition = true;
        private double customX = -1;
        private double customY = -1;
        private bool hasCustomPosition = false;
        private bool isDarkTheme = true;
        private double scale = 1.0;
        private bool decorationEnabled = true;
        private string decorationMediaPath = "";
        private DecorationController _decorationController;
        private bool gamingModeEnabled = false;
        private bool lockModeEnabled
        {
            get => _islandModeController?.LockModeEnabled == true;
            set { if (_islandModeController != null) _islandModeController.LockModeEnabled = value; }
        }
        private bool startWithWindowsEnabled = false;
        private bool browserSourceEnabled = true;
        private AppSettings _settings;
        private WindowChromeController _windowChromeController;
        private SmartVisibilityController _smartVisibilityController;
        private AudioVisualizerService _audioVisualizer;
        private int _visualizerUpdatePending;
        private float _pendingBand0;
        private float _pendingBand1;
        private float _pendingBand2;
        private float _pendingBand3;
        private bool _isClosing;
        private readonly Action _renderVisualizerAction;
        private VisualizerRenderer _visualizerRenderer;
        private SystemVolumeService _systemVolumeService;
        private bool _updatingSystemVolumeSlider;
        private int _musicInfoUpdatePending;
        private Point _dragStartPoint;
        private bool _isDraggingReady = false;

        private int displayMode
        {
            get => _islandModeController?.DisplayMode ?? 0;
            set { if (_islandModeController != null) _islandModeController.DisplayMode = Math.Clamp(value, 0, 2); }
        }
        
        public MainWindow()
        {
            _renderVisualizerAction = RenderVisualizer;
            try
            {
                Logger.Log("Application starting...");
                InitializeComponent();
                _trackTextPresenter = new TrackTextPresenter(
                    CompactTitle, CompactArtist, TrackTitle, ArtistName,
                    CompactSourceBadge, CompactSourceDot, CompactSourceText,
                    ExpandedSourceBadge, ExpandedSourceDot, ExpandedSourceText);
                _albumArtPresenter = new AlbumArtPresenter(
                    AlbumArtMinimal, AlbumArt, AlbumArtExpanded, AlbumArtPaused,
                    AlbumArtOld, AlbumArtExpandedOld, RevealAlbumArt,
                    CompactContentOld, CompactContentNew, ExpandedContentOld, ExpandedContentNew,
                    CompactOldTranslate, CompactNewTranslate, ExpandedOldTranslate, ExpandedNewTranslate,
                    CompactTitleOld, CompactArtistOld, CompactSourceTextOld,
                    TrackTitleOld, ArtistNameOld, ExpandedSourceTextOld,
                    CompactTitle, CompactArtist, CompactSourceText,
                    TrackTitle, ArtistName, ExpandedSourceText,
                    RevealTitleText, RevealSubtitleText, RevealSourceText, TrackRevealOverlay,
                    AlbumArtMinimalScale, AlbumArtScale, AlbumArtExpandedScale,
                    AlbumArtMinimalRotation, AlbumArtRotation, AlbumArtExpandedRotation,
                    AlbumArtOldRotation, AlbumArtExpandedOldRotation,
                    () => fluidPhysicsEnabled,
                    PulseIslandDropletOnTrackChange);
                _playbackUiController = new PlaybackUiController(
                    ProgressTrackCompact, ProgressBarCompact, ProgressDotCompact,
                    ProgressTrackExpanded, ProgressBarExpanded, ProgressDotExpanded,
                    CurrentTimeText, TotalTimeText,
                    _albumArtPresenter,
                    () => musicService?.TogglePlayPauseAsync() ?? Task.CompletedTask,
                    () => musicService?.NextTrackAsync() ?? Task.CompletedTask,
                    () => musicService?.PreviousTrackAsync() ?? Task.CompletedTask,
                    position => musicService?.SeekToPositionAsync(position) ?? Task.CompletedTask,
                    OnPlaybackPositionChanged,
                    () => vinylScratchEnabled,
                    () => vinylRotationEnabled,
                    () => _isClosing);
                _islandModeController = new IslandModeController(
                    this,
                    MinimalMode, CompactMode, ExpandedMode, PausedMode,
                    IslandBorder,
                    () => scale,
                    () => fluidPhysicsEnabled,
                    () => GetTargetWorkingArea(customX, customY),
                    () => (isTopPosition, hasCustomPosition, customX, customY),
                    OnIslandModeTransitionCompleted,
                    EnsureTopmost);
                _decorationController = new DecorationController(
                    GifMinimalContainer, GifCompactContainer, GifExpandedContainer,
                    GifMinimal, GifCompact, GifExpanded,
                    DecorVideoMinimal, DecorVideoCompact, DecorVideoExpanded);
                _visualizerRenderer = new VisualizerRenderer(
                    MinimalMode, CompactMode, ExpandedMode,
                    PlaybackIndicatorMinimal, PlaybackIndicatorCompact, PlaybackIndicatorExpanded,
                    VinylHaloMinimal, VinylHaloCompact, VinylHaloExpanded,
                    IslandBorderScale,
                    AlbumArtMinimalScale, AlbumArtScale, AlbumArtExpandedScale,
                    GifMinimalScale, GifCompactScale, GifExpandedScale);
                _smartVisibilityController = new SmartVisibilityController(this, EnsureTopmost);
                _windowChromeController = new WindowChromeController(
                    this,
                    OnGlobalHotkey,
                    () => _smartVisibilityController?.IsSmartHidden == true);
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
                _systemVolumeService = new SystemVolumeService(Dispatcher, UpdateSystemVolumeSlider);
                InitializeTrayIcon();
                AnimateStartup();
                _startupTrimTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                _startupTrimTimer.Tick += (s, e) =>
                {
                    _startupTrimTimer.Stop();
                    _startupTrimTimer = null;
                    TrimMemory();
                };
                _startupTrimTimer.Start();
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

        private MusicInfo _pendingMusicInfo = null;

        private void InitializeMusicService()
        {
            musicService = new MusicInfoService();
            musicService.SetBrowserSourceEnabled(browserSourceEnabled);
            musicService.MusicInfoChanged += info =>
            {
                Interlocked.Exchange(ref _pendingMusicInfo, info);
                QueueMusicInfoUpdate();
            };

            _audioVisualizer = new AudioVisualizerService();
            _audioVisualizer.BandsUpdated += OnVisualizerBandsUpdated;

            ApplyMusicInfo(musicService.GetCurrentMusicInfo());
            InitializeProgressTimer();
        }

        private void QueueMusicInfoUpdate()
        {
            if (_isClosing)
            {
                Interlocked.Exchange(ref _pendingMusicInfo, null);
                return;
            }

            if (Interlocked.CompareExchange(ref _musicInfoUpdatePending, 1, 0) != 0)
                return;

            try
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(ProcessPendingMusicInfo));
            }
            catch (InvalidOperationException)
            {
                Interlocked.Exchange(ref _musicInfoUpdatePending, 0);
            }
        }

        private void ProcessPendingMusicInfo()
        {
            if (_isClosing)
            {
                Interlocked.Exchange(ref _pendingMusicInfo, null);
                Interlocked.Exchange(ref _musicInfoUpdatePending, 0);
                return;
            }

            try
            {
                var latest = Interlocked.Exchange(ref _pendingMusicInfo, null);
                if (!_isClosing && latest != null)
                    ApplyMusicInfo(latest);
            }
            finally
            {
                Interlocked.Exchange(ref _musicInfoUpdatePending, 0);
                if (!_isClosing && Volatile.Read(ref _pendingMusicInfo) != null)
                    QueueMusicInfoUpdate();
            }
        }

        private void QueueVisualizerUpdate()
        {
            if (_isClosing || _audioVisualizer?.IsEnabled != true || !IsVisible ||
                lastMusicInfo?.IsPlaying != true)
                return;

            if (Interlocked.CompareExchange(ref _visualizerUpdatePending, 1, 0) != 0)
                return;

            try
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Render, _renderVisualizerAction);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Exchange(ref _visualizerUpdatePending, 0);
            }
        }

        private void OnVisualizerBandsUpdated(float band0, float band1, float band2, float band3)
        {
            Volatile.Write(ref _pendingBand0, band0);
            Volatile.Write(ref _pendingBand1, band1);
            Volatile.Write(ref _pendingBand2, band2);
            Volatile.Write(ref _pendingBand3, band3);
            QueueVisualizerUpdate();
        }

        private void InitializeProgressTimer()
        {
            _playbackUiController.Start();
        }

        private void SetProgressRatio(double ratio)
        {
            _playbackUiController.SetProgressRatio(ratio);
        }

        private PlaybackUiState CreatePlaybackUiState()
        {
            return new PlaybackUiState
            {
                TrackKey = lastMusicInfo == null ? null : $"{lastMusicInfo.Artist}|{lastMusicInfo.Title}",
                Position = lastMusicInfo?.Position ?? TimeSpan.Zero,
                Duration = lastMusicInfo?.Duration ?? TimeSpan.Zero,
                IsPlaying = lastMusicInfo?.IsPlaying == true,
                IsPaused = isPaused
            };
        }

        private void OnPlaybackPositionChanged(TimeSpan position)
        {
            if (!_isClosing && lastMusicInfo != null)
                lastMusicInfo.Position = position;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _windowChromeController?.OnSourceInitialized();
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            WindowBlurHelper.SetAcrylicBlur(helper.Handle, false);
        }

        public void EnsureTopmost() => _windowChromeController?.EnsureTopmost();

        private void OnGlobalHotkey()
        {
            if (_smartVisibilityController?.IsSmartHidden == true || !IsVisible)
                ForceShowIsland();
            else
                CycleDisplayMode(force: true);
        }
        
        private void CycleDisplayMode()
        {
            CycleDisplayMode(force: false);
        }

        private void CycleDisplayMode(bool force)
        {
            bool changed = _islandModeController.CycleDisplayMode(force, isPaused, () =>
            {
                isPaused = false;
                _pauseDebounceTimer?.Stop();
                _pauseDebounceTimer = null;
                AnimateFromPaused();
                _playbackUiController.PlayPauseButtonClick();
            });
            if (changed)
                SaveSettings();
        }

        private void ApplyInitialDisplayMode()
        {
            _islandModeController.ApplyInitialDisplayMode();
        }

        private void EnsureActiveDisplayModeVisible()
        {
            _islandModeController.EnsureActiveDisplayModeVisible();
        }

        private void AnimateToMode()
        {
            _islandModeController.AnimateToMode();
        }

        private void AnimateToPaused()
        {
            CancelDropPeek();
            StopRotation();
            ResetBreathing();
            _ = _audioVisualizer?.StopAsync();
            _islandModeController.AnimateToPaused();
        }

        private void AnimateFromPaused()
        {
            if (lastMusicInfo?.IsPlaying == true)
                _ = _audioVisualizer?.StartAsync();
            _islandModeController.AnimateFromPaused();
        }

        private void TriggerDropPeekToMedium(MusicInfo musicInfo = null)
        {
            _islandModeController.TriggerDropPeekToMedium(null);
        }

        private void CollapseDropPeekToMinimal()
        {
            _islandModeController.CollapseDropPeekToMinimal();
        }

        private void CancelDropPeek()
        {
            _islandModeController.CancelDropPeek();
        }

        private Grid GetDisplayModeGrid() => _islandModeController.ActiveMode;

        private void OnIslandModeTransitionCompleted()
        {
            UpdateDecorationVisibility(lastMusicInfo?.IsPlaying == true);
            if (lastMusicInfo?.Duration > TimeSpan.Zero)
                SetProgressRatio(lastMusicInfo.Position.TotalSeconds / lastMusicInfo.Duration.TotalSeconds);
        }

        private void ShowDisplayModeWithoutAnimation()
        {
            EnsureActiveDisplayModeVisible();
        }

        private void ApplyMusicInfo(MusicInfo musicInfo)
        {
            if (musicInfo == null) return;

            if (musicService?.DisableAlbumArtForDiagnostics == true)
                musicInfo.AlbumArt = MusicInfoService.DefaultAlbumArt;
            
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
                _albumArtPresenter.PresentTrack(new AlbumArtTrack
                {
                    Title = musicInfo.Title,
                    Artist = musicInfo.Artist,
                    Source = musicInfo.SourceApp,
                    AlbumArt = musicInfo.AlbumArt
                }, !audioReactiveBreathingEnabled);
                if (displayMode == 0)
                {
                    TriggerDropPeekToMedium(musicInfo);
                }
            }
            else
            {
                _albumArtPresenter.UpdateCurrentArtwork(musicInfo.AlbumArt);
            }

            _trackTextPresenter.SetTrackText(musicInfo.Title, musicInfo.Artist);
            _trackTextPresenter.ApplySourceVisuals(musicInfo.SourceApp);
            bool artUpdated = lastMusicInfo != null && 
                              lastMusicInfo.AlbumArt == MusicInfoService.DefaultAlbumArt && 
                              musicInfo.AlbumArt != null && 
                              musicInfo.AlbumArt != MusicInfoService.DefaultAlbumArt;

            if (trackChanged || lastMusicInfo == null || artUpdated)
            {
                UpdateAlbumPalette(MusicVisualHelper.GetAlbumPaletteCached(musicInfo.AlbumArt, $"{musicInfo.Title}|{musicInfo.Artist}|{musicInfo.SourceApp}"));
            }
            UpdateTrayController(musicInfo);

            if (trackChanged || lastMusicInfo == null)
            {
                lastMusicInfo = musicInfo;
            }
            else
            {
                lastMusicInfo.IsPlaying = musicInfo.IsPlaying;
                if (musicInfo.Duration > TimeSpan.Zero)
                    lastMusicInfo.Duration = musicInfo.Duration;
                lastMusicInfo.AlbumArt = musicInfo.AlbumArt;
                lastMusicInfo.SourceApp = musicInfo.SourceApp;

            }

            _playbackUiController.UpdateState(CreatePlaybackUiState());

            _smartVisibilityController.UpdateState(
                hasDisplayableMusic,
                musicInfo.IsPlaying,
                trackChanged);
            
            UpdateDecorationVisibility(musicInfo.IsPlaying);
            UpdateIndicatorsVisibility(musicInfo.IsPlaying);

            if (musicInfo.IsPlaying)
            {
                StartRotation();
                if (!audioReactiveBreathingEnabled && trackChanged)
                {
                    _albumArtPresenter.PulseAlbumArt();
                }
                SetProgressGlowStrength(0.9);
                _ = _audioVisualizer?.StartAsync();
            }
            else
            {
                StopRotation();
                ResetBreathing();
                SetProgressGlowStrength(0.45);
                _ = _audioVisualizer?.StopAsync();
                SetHaloOpacity(0);
            }

            UpdatePlayPauseIcon(musicInfo.IsPlaying);
            this.Title = $"Dynamic Island PC - {musicInfo.Title} - {musicInfo.Artist}";

            if (!hasDisplayableMusic)
            {
                _pauseDebounceTimer?.Stop();
                _pauseDebounceTimer = null;
                if (isPaused)
                {
                    isPaused = false;
                    AnimateFromPaused();
                }
                else if (!_isPeekingDrop)
                {
                    EnsureActiveDisplayModeVisible();
                }
                _playbackUiController.UpdateState(CreatePlaybackUiState());
                return;
            }

            if (musicInfo.IsPlaying)
            {
                _pauseDebounceTimer?.Stop();
                _pauseDebounceTimer = null;

                if (isPaused)
                {
                    isPaused = false;
                    if (!_isPeekingDrop)
                    {
                        AnimateFromPaused();
                    }
                }
                else if (!_isPeekingDrop)
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
            _playbackUiController.UpdateState(CreatePlaybackUiState());
        }
        

        private static bool HasDisplayableMusic(MusicInfo musicInfo)
        {
            return musicInfo?.HasMedia == true && !string.IsNullOrWhiteSpace(musicInfo.Title);
        }

        private void ApplyDecorationMedia()
        {
            _decorationController.ApplySettings(decorationEnabled, decorationMediaPath, displayMode, _isPeekingDrop, lastMusicInfo?.IsPlaying == true);
        }

        private void UpdateDecorationVisibility(bool isPlaying)
        {
            _decorationController.UpdateVisibility(isPlaying, displayMode, _isPeekingDrop);
        }

        private void DecorationVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            _decorationController.OnVideoMediaEnded(sender);
        }

        private void ForceShowIsland()
        {
            _smartVisibilityController?.ForceShow();
            CycleDisplayMode(force: true);
        }



        private void ShowTrackReveal(MusicInfo info)
        {
            _albumArtPresenter.ShowTrackReveal(new AlbumArtTrack
            {
                Title = info?.Title,
                Artist = info?.Artist,
                Source = info?.SourceApp,
                AlbumArt = info?.AlbumArt
            });
        }

        private void PulseAlbumArt()
        {
            _albumArtPresenter.PulseAlbumArt();
        }

        private void SetProgressGlowStrength(double opacity)
        {
            if (ProgressBarCompact.Effect is System.Windows.Media.Effects.DropShadowEffect compactGlow)
                compactGlow.Opacity = opacity * 0.65;
            if (ProgressBarExpanded.Effect is System.Windows.Media.Effects.DropShadowEffect expandedGlow)
                expandedGlow.Opacity = opacity * 0.55;
        }

        private void StartRotation(double fromAngle = double.NaN)
        {
            _albumArtPresenter.StartRotation(fromAngle);
        }

        private void StopRotation()
        {
            _albumArtPresenter.StopRotation();
        }

        private void ResetVinylRotation()
        {
            _albumArtPresenter.ResetVinylRotation();
        }

        private void ClearAlbumArtLayersForDiagnostics()
        {
            _albumArtPresenter.ClearForDiagnostics(MusicInfoService.DefaultAlbumArt);
            if (lastMusicInfo != null)
                lastMusicInfo.AlbumArt = MusicInfoService.DefaultAlbumArt;
        }

        private void DetachVinylClocksForScratch(double currentAngle)
        {
            _albumArtPresenter.DetachVinylClocksForScratch(currentAngle);
        }

        private void SetVinylManualAngle(double angle)
        {
            _albumArtPresenter.SetVinylManualAngle(angle);
        }

        private void PulseIslandDropletOnTrackChange()
        {
            if (IslandBorderScale == null) return;
            IslandBorderScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            IslandBorderScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            var targetScaleX = fluidPhysicsEnabled ? 0.965 : 0.980;
            var targetScaleY = fluidPhysicsEnabled ? 0.975 : 0.985;
            var dipTime = TimeSpan.FromMilliseconds(fluidPhysicsEnabled ? 130 : 120);
            var totalTime = TimeSpan.FromMilliseconds(fluidPhysicsEnabled ? 450 : 380);
            var returnEase = new CubicEase { EasingMode = EasingMode.EaseOut };

            var pulseX = new DoubleAnimationUsingKeyFrames();
            pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(targetScaleX, KeyTime.FromTimeSpan(dipTime))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            pulseX.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(totalTime))
            {
                EasingFunction = returnEase
            });

            var pulseY = new DoubleAnimationUsingKeyFrames();
            pulseY.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            pulseY.KeyFrames.Add(new EasingDoubleKeyFrame(targetScaleY, KeyTime.FromTimeSpan(dipTime))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            pulseY.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(totalTime))
            {
                EasingFunction = returnEase
            });

            pulseX.Completed += (s, e) =>
            {
                IslandBorderScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                IslandBorderScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                IslandBorderScale.ScaleX = 1.0;
                IslandBorderScale.ScaleY = 1.0;
            };

            IslandBorderScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseX);
            IslandBorderScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseY);
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
            UpdateHaloColors(eqColor);
        }

        private void UpdateHaloColors(Color color)
        {
            var strokeColor = Color.FromArgb(215, color.R, color.G, color.B);
            var brush = new SolidColorBrush(strokeColor);
            void Apply(System.Windows.Shapes.Ellipse halo)
            {
                if (halo == null) return;
                halo.Stroke = brush;
                if (halo.Effect is System.Windows.Media.Effects.DropShadowEffect dse)
                {
                    dse.Color = color;
                }
            }
            Apply(VinylHaloMinimal);
            Apply(VinylHaloCompact);
            Apply(VinylHaloExpanded);
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
            _playbackUiController.PlayPauseButtonClick();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            _playbackUiController.NextButtonClick();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            _playbackUiController.PreviousButtonClick();
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
            _trayMenuController = new TrayMenuController(new TrayMenuCallbacks
            {
                OpenSettings = OpenSettings,
                OpenDiagnostics = OpenDiagnostics,
                CheckForUpdatesAsync = CheckForUpdatesAsync,
                ExitApplication = () => Application.Current.Shutdown(),
                SetPosition = SetPosition,
                SetScale = SetScale,
                SetTheme = dark => SetTheme(dark),
                SetGamingMode = SetGamingMode,
                SetLockMode = SetLockMode,
                SetStartup = SetStartWithWindows,
                SetBrowserSource = SetBrowserSourceEnabled,
                SetDisableAlbumArt = SetDisableAlbumArtForDiagnostics,
                PreviousTrackAsync = () => musicService?.PreviousTrackAsync() ?? Task.CompletedTask,
                PlayPauseAsync = () => musicService?.TogglePlayPauseAsync() ?? Task.CompletedTask,
                NextTrackAsync = () => musicService?.NextTrackAsync() ?? Task.CompletedTask
            });
            _trayMenuController.Initialize(CreateTrayMenuState());
        }

        private TrayMenuState CreateTrayMenuState(MusicInfo musicInfo = null)
        {
            var current = musicInfo ?? lastMusicInfo;
            var title = string.IsNullOrWhiteSpace(current?.Title) ? "Нет медиа" : current.Title;
            var artist = string.IsNullOrWhiteSpace(current?.Artist) ? current?.SourceApp : current.Artist;
            return new TrayMenuState
            {
                TrackText = string.IsNullOrWhiteSpace(artist) ? title : $"{title} - {artist}",
                IsPlaying = current?.IsPlaying == true,
                IsTopPosition = isTopPosition,
                HasCustomPosition = hasCustomPosition,
                IsDarkTheme = isDarkTheme,
                Scale = scale,
                GamingModeEnabled = gamingModeEnabled,
                LockModeEnabled = lockModeEnabled,
                StartWithWindowsEnabled = startWithWindowsEnabled,
                BrowserSourceEnabled = browserSourceEnabled,
                DisableAlbumArtForDiagnostics = DisableAlbumArtForDiagnostics
            };
        }

        private void OpenDiagnostics()
        {
            var diagnosticsWindow = new DiagnosticsWindow(GetApplicationDiagnostics);
            diagnosticsWindow.Owner = this;
            diagnosticsWindow.Show();
        }

        private string GetApplicationDiagnostics()
        {
            using var process = Process.GetCurrentProcess();
            var gcInfo = GC.GetGCMemoryInfo();
            var bytesPerMegabyte = 1024d * 1024d;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Process memory");
            sb.AppendLine($"Working Set: {process.WorkingSet64 / bytesPerMegabyte:F1} MB");
            sb.AppendLine($"Private Bytes: {process.PrivateMemorySize64 / bytesPerMegabyte:F1} MB");
            sb.AppendLine($"Managed Heap: {GC.GetTotalMemory(false) / bytesPerMegabyte:F1} MB");
            sb.AppendLine($"GC heap size: {gcInfo.HeapSizeBytes / bytesPerMegabyte:F1} MB");
            sb.AppendLine($"GC fragmented: {gcInfo.FragmentedBytes / bytesPerMegabyte:F1} MB");
            sb.AppendLine($"Gen 0 / 1 / 2 collections: {GC.CollectionCount(0)} / {GC.CollectionCount(1)} / {GC.CollectionCount(2)}");
            sb.AppendLine($"Album art diagnostics disabled: {DisableAlbumArtForDiagnostics}");
            sb.AppendLine();
            sb.Append(musicService?.GetDiagnostics() ?? "Music service is not initialized.");
            return sb.ToString();
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
            _playbackUiController.HandleWheel(e.Delta);
        }
        
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _playbackUiController.HandleMiddleClick();
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
                if (dep is Button || dep is System.Windows.Controls.Primitives.ButtonBase || dep is TextBox || dep is Slider)
                    return true;
                if (dep is FrameworkElement fe && (fe.Name.StartsWith("AlbumArt") || fe.Name.StartsWith("Vinyl") || fe.Name == "ProgressContainerCompact" || fe.Name == "ProgressContainerExpanded"))
                    return true;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        private void ProgressContainer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid grid)
            {
                _playbackUiController.ProgressMouseDown(grid, e.GetPosition(grid).X);
                if (_playbackUiController.IsScrubbing) e.Handled = true;
            }
        }

        private void ProgressContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Grid grid)
            {
                _playbackUiController.ProgressMouseMove(e.GetPosition(grid).X, e.LeftButton == MouseButtonState.Pressed);
                if (_playbackUiController.IsScrubbing) e.Handled = true;
            }
        }

        private void ProgressContainer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid grid)
            {
                _playbackUiController.ProgressMouseUp(e.GetPosition(grid).X);
                e.Handled = true;
            }
        }

        private void ProgressContainer_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _playbackUiController.ProgressLostMouseCapture();
        }

        private void SystemVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_updatingSystemVolumeSlider && _systemVolumeService != null)
                _systemVolumeService.SetVolume(e.NewValue);
        }

        private void UpdateSystemVolumeSlider(double percent)
        {
            if (_isClosing) return;
            _updatingSystemVolumeSlider = true;
            try { SystemVolumeSlider.Value = Math.Clamp(percent, 0.0, 100.0); }
            finally { _updatingSystemVolumeSlider = false; }
        }

        public static void TrimMemory()
        {
            Task.Run(() =>
            {
                try
                {
                    GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
                }
                catch { }
            });
        }

        private void RenderVisualizer()
        {
            Interlocked.Exchange(ref _visualizerUpdatePending, 0);

            if (_isClosing || _audioVisualizer?.IsEnabled != true || !IsVisible ||
                lastMusicInfo?.IsPlaying != true)
                return;

            float band0 = Volatile.Read(ref _pendingBand0);
            float band1 = Volatile.Read(ref _pendingBand1);
            float band2 = Volatile.Read(ref _pendingBand2);
            float band3 = Volatile.Read(ref _pendingBand3);
            _visualizerRenderer.Render(visualizerSkin, audioReactiveBreathingEnabled, _isPeekingDrop,
                GetDisplayModeGrid(), band0, band1, band2, band3);
        }

        private void ApplyVisualizerSkin(int skin)
        {
            visualizerSkin = Math.Clamp(skin, 0, 3);
            _visualizerRenderer.ApplySkin(visualizerSkin, lastMusicInfo?.IsPlaying == true);
            if (lastMusicInfo?.IsPlaying == true)
            {
                Interlocked.Exchange(ref _visualizerUpdatePending, 0);
                RenderVisualizer();
            }
        }

        private void UpdateIndicatorsVisibility(bool isPlaying)
        {
            _visualizerRenderer.UpdateIndicatorsVisibility(isPlaying, visualizerSkin);
        }

        private void ResetBreathing()
        {
            _visualizerRenderer.Reset();
        }

        private void SetHaloOpacity(double opacity)
        {
            _visualizerRenderer.SetHaloOpacity(opacity);
        }


        private void OnVinylMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                _playbackUiController.VinylMouseDown(element, e.GetPosition(element));
                e.Handled = true;
            }
        }

        private void OnVinylMouseMove(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element)
                _playbackUiController.VinylMouseMove(e.GetPosition(element), e.LeftButton == MouseButtonState.Pressed);
        }

        private void OnVinylMouseUp(object sender, MouseButtonEventArgs e)
        {
            _playbackUiController.VinylMouseUp(commit: true);
            e.Handled = true;
        }

        private void OnVinylLostMouseCapture(object sender, MouseEventArgs e)
        {
            _playbackUiController.VinylMouseUp(commit: false);
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;
            _startupTrimTimer?.Stop();
            _startupTrimTimer = null;
            _saveSettingsDebounceTimer?.Stop();
            _pauseDebounceTimer?.Stop();
            _pauseDebounceTimer = null;
            if (_audioVisualizer != null)
                _audioVisualizer.BandsUpdated -= OnVisualizerBandsUpdated;
            Interlocked.Exchange(ref _visualizerUpdatePending, 0);
            _audioVisualizer?.Dispose();
            _systemVolumeService?.Dispose();
            _islandModeController?.Dispose();
            _playbackUiController?.Dispose();
            _decorationController?.Dispose();
            _albumArtPresenter?.Dispose();
            _smartVisibilityController?.Dispose();
            _windowChromeController?.Dispose();
            _trackTextPresenter?.Dispose();
            musicService?.Dispose();
            SaveSettings();
            _trayMenuController?.Dispose();
            base.OnClosed(e);
        }
    }
}




