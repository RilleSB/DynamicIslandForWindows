using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DynamicIslandPC
{
    internal sealed class IslandModeController : IDisposable
    {
        private readonly Window _window;
        private readonly Grid _minimalMode;
        private readonly Grid _compactMode;
        private readonly Grid _expandedMode;
        private readonly Grid _pausedMode;
        private readonly Border _islandBorder;
        private readonly Func<double> _getScale;
        private readonly Func<bool> _getFluidPhysics;
        private readonly Func<Rect> _getWorkingArea;
        private readonly Func<(bool isTop, bool hasCustom, double customX, double customY)> _getPositionSettings;
        private readonly Action _transitionCompleted;
        private readonly Action _ensureTopmost;
        private readonly DispatcherTimer _peekTimer;

        private Storyboard _activeStoryboard;
        private EventHandler _activeStoryboardCompleted;
        private DateTime _lastModeSwitchAt = DateTime.MinValue;
        private bool _disposed;

        public IslandModeController(
            Window window,
            Grid minimalMode, Grid compactMode, Grid expandedMode, Grid pausedMode,
            Border islandBorder,
            Func<double> getScale,
            Func<bool> getFluidPhysics,
            Func<Rect> getWorkingArea,
            Func<(bool isTop, bool hasCustom, double customX, double customY)> getPositionSettings,
            Action transitionCompleted,
            Action ensureTopmost)
        {
            _window = window;
            _minimalMode = minimalMode;
            _compactMode = compactMode;
            _expandedMode = expandedMode;
            _pausedMode = pausedMode;
            _islandBorder = islandBorder;
            _getScale = getScale;
            _getFluidPhysics = getFluidPhysics;
            _getWorkingArea = getWorkingArea;
            _getPositionSettings = getPositionSettings;
            _transitionCompleted = transitionCompleted;
            _ensureTopmost = ensureTopmost;
            _peekTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2800) };
            _peekTimer.Tick += OnPeekTimerTick;
        }

        public int DisplayMode { get; set; }

        public bool IsPeekingDrop { get; private set; }

        public bool LockModeEnabled { get; set; }

        public Grid ActiveMode => IsPeekingDrop ? _compactMode : GetModeGrid(DisplayMode);

        public void ApplyInitialDisplayMode()
        {
            DisplayMode = Math.Clamp(DisplayMode, 0, 2);
            SetWindowToMode(DisplayMode);
            EnsureActiveDisplayModeVisible();
        }

        public void SetScaleAndPosition()
        {
            SetWindowToMode(IsPeekingDrop ? 1 : DisplayMode);
            UpdateWindowPosition();
        }

        public void UpdateWindowPosition()
        {
            var position = CalculateWindowPosition(_window.Width, _window.Height);
            _window.Left = position.left;
            _window.Top = position.top;
        }

        public (double left, double top) CalculateWindowPosition(double targetWidth, double targetHeight)
        {
            var area = _getWorkingArea();
            var settings = _getPositionSettings();
            if (settings.hasCustom)
            {
                double maxLeft = Math.Max(area.Left, area.Right - targetWidth);
                double maxTop = Math.Max(area.Top, area.Bottom - targetHeight);
                return (
                    Math.Clamp(settings.customX - targetWidth / 2, area.Left, maxLeft),
                    Math.Clamp(settings.customY, area.Top, maxTop));
            }

            double centeredLeft = area.Left + (area.Width - targetWidth) / 2;
            double top = settings.isTop ? area.Top + 20 : area.Bottom - targetHeight - 60;
            return (centeredLeft, top);
        }

        public bool CycleDisplayMode(bool force, bool isPaused, Action resumeFromPaused)
        {
            if (_disposed || (LockModeEnabled && !force))
                return false;

            var now = DateTime.UtcNow;
            if ((now - _lastModeSwitchAt).TotalMilliseconds < 250)
                return false;
            _lastModeSwitchAt = now;

            if (IsPeekingDrop)
            {
                CancelDropPeek();
                DisplayMode = 1;
                AnimateToMode();
                return true;
            }

            if (isPaused)
            {
                resumeFromPaused?.Invoke();
                return true;
            }

            DisplayMode = (DisplayMode + 1) % 3;
            AnimateToMode();
            return true;
        }

        public void EnsureActiveDisplayModeVisible()
        {
            StopActiveStoryboard();
            _pausedMode.BeginAnimation(UIElement.OpacityProperty, null);
            _pausedMode.Opacity = 0;
            _pausedMode.Visibility = Visibility.Collapsed;

            Grid active = ActiveMode;
            SetModeVisibility(active);
        }

        public void AnimateToMode()
        {
            if (_disposed) return;
            CancelDropPeek();
            _pausedMode.BeginAnimation(UIElement.OpacityProperty, null);
            _pausedMode.Opacity = 0;
            _pausedMode.Visibility = Visibility.Collapsed;

            Grid current = GetVisibleMode();
            Grid target = GetModeGrid(DisplayMode);
            var targetSize = GetScaledModeSize(DisplayMode);
            var targetPosition = CalculateWindowPosition(targetSize.width, targetSize.height);

            if (current == null)
            {
                SetWindowToMode(DisplayMode);
                EnsureActiveDisplayModeVisible();
                CompleteTransition();
                return;
            }

            if (current == target &&
                Math.Abs(_window.Left - targetPosition.left) < 1.0 &&
                Math.Abs(_window.Top - targetPosition.top) < 1.0 &&
                Math.Abs(_window.Width - targetSize.width) < 1.0 &&
                Math.Abs(_window.Height - targetSize.height) < 1.0)
            {
                EnsureActiveDisplayModeVisible();
                CompleteTransition();
                return;
            }

            double currentLeft = _window.Left;
            double currentTop = _window.Top;
            double currentWidth = _window.Width;
            double currentHeight = _window.Height;
            bool expanding = targetSize.width > currentWidth || targetSize.height > currentHeight;
            var (hEase, vEase, duration) = GetWindowTransitionEasing(450, expanding);

            StopActiveStoryboard();
            ResetAnimationState(current);
            _islandBorder.CornerRadius = new CornerRadius(GetModeCornerRadius(DisplayMode) * _getScale());

            var storyboard = new Storyboard();
            if (current != target)
            {
                target.Visibility = Visibility.Visible;
                target.Opacity = 0;
                AddOpacityAnimation(storyboard, current, 1, 0, TimeSpan.Zero, TimeSpan.FromMilliseconds(160), new QuadraticEase { EasingMode = EasingMode.EaseOut });
                AddOpacityAnimation(storyboard, target, 0, 1, TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(220), new QuadraticEase { EasingMode = EasingMode.EaseOut });
            }

            AddWindowAnimation(storyboard, Window.WidthProperty, currentWidth, targetSize.width, duration, hEase);
            AddWindowAnimation(storyboard, Window.HeightProperty, currentHeight, targetSize.height, duration, vEase);
            AddWindowAnimation(storyboard, Window.LeftProperty, currentLeft, targetPosition.left, duration, hEase);
            AddWindowAnimation(storyboard, Window.TopProperty, currentTop, targetPosition.top, duration, vEase);

            StartStoryboard(storyboard, (_, __) =>
            {
                SetWindowValues(targetSize.width, targetSize.height, targetPosition.left, targetPosition.top);
                EnsureActiveDisplayModeVisible();
                CompleteTransition();
            });
        }

        public void AnimateToPaused()
        {
            if (_disposed) return;
            CancelDropPeek();
            Grid current = GetVisibleMode();
            var pausedSize = 60 * _getScale();
            var targetPosition = CalculateWindowPosition(pausedSize, pausedSize);

            StopActiveStoryboard();
            if (current == null)
            {
                HideAllModes();
                _pausedMode.Visibility = Visibility.Visible;
                _pausedMode.Opacity = 1;
                SetWindowValues(pausedSize, pausedSize, targetPosition.left, targetPosition.top);
                _islandBorder.CornerRadius = new CornerRadius(30 * _getScale());
                CompleteTransition();
                return;
            }

            foreach (var grid in new[] { _minimalMode, _compactMode, _expandedMode })
            {
                if (grid != current)
                {
                    grid.BeginAnimation(UIElement.OpacityProperty, null);
                    grid.Visibility = Visibility.Collapsed;
                    grid.Opacity = 0;
                }
            }
            current.Visibility = Visibility.Visible;
            _pausedMode.Visibility = Visibility.Visible;
            _pausedMode.Opacity = 0;
            _islandBorder.CornerRadius = new CornerRadius(30 * _getScale());

            var (hEase, vEase, duration) = GetWindowTransitionEasing(380, false);
            var storyboard = new Storyboard();
            AddOpacityAnimation(storyboard, current, current.Opacity, 0, TimeSpan.Zero, TimeSpan.FromMilliseconds(200), null);
            AddOpacityAnimation(storyboard, _pausedMode, 0, 1, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200), null);
            AddWindowAnimation(storyboard, Window.WidthProperty, _window.Width, pausedSize, TimeSpan.FromMilliseconds(200) + duration, hEase, TimeSpan.FromMilliseconds(200));
            AddWindowAnimation(storyboard, Window.HeightProperty, _window.Height, pausedSize, TimeSpan.FromMilliseconds(200) + duration, vEase, TimeSpan.FromMilliseconds(200));
            AddWindowAnimation(storyboard, Window.LeftProperty, _window.Left, targetPosition.left, TimeSpan.FromMilliseconds(200) + duration, hEase, TimeSpan.FromMilliseconds(200));
            AddWindowAnimation(storyboard, Window.TopProperty, _window.Top, targetPosition.top, TimeSpan.FromMilliseconds(200) + duration, vEase, TimeSpan.FromMilliseconds(200));

            StartStoryboard(storyboard, (_, __) =>
            {
                HideAllModes();
                _pausedMode.Visibility = Visibility.Visible;
                _pausedMode.Opacity = 1;
                SetWindowValues(pausedSize, pausedSize, targetPosition.left, targetPosition.top);
                CompleteTransition();
            });
        }

        public void AnimateFromPaused()
        {
            if (_disposed) return;
            var target = GetModeGrid(DisplayMode);
            var targetSize = GetScaledModeSize(DisplayMode);
            var targetPosition = CalculateWindowPosition(targetSize.width, targetSize.height);

            StopActiveStoryboard();
            target.Visibility = Visibility.Visible;
            target.Opacity = 0;
            _islandBorder.CornerRadius = new CornerRadius(GetModeCornerRadius(DisplayMode) * _getScale());

            var (hEase, vEase, duration) = GetWindowTransitionEasing(380, true);
            var fadeDelay = TimeSpan.FromMilliseconds(180);
            var storyboard = new Storyboard();
            AddOpacityAnimation(storyboard, _pausedMode, _pausedMode.Opacity, 0, TimeSpan.Zero, fadeDelay, null);
            AddOpacityAnimation(storyboard, target, 0, 1, fadeDelay, TimeSpan.FromMilliseconds(250), null);
            AddWindowAnimation(storyboard, Window.WidthProperty, _window.Width, targetSize.width, duration, hEase, fadeDelay);
            AddWindowAnimation(storyboard, Window.HeightProperty, _window.Height, targetSize.height, duration, vEase, fadeDelay);
            AddWindowAnimation(storyboard, Window.LeftProperty, _window.Left, targetPosition.left, duration, hEase, fadeDelay);
            AddWindowAnimation(storyboard, Window.TopProperty, _window.Top, targetPosition.top, duration, vEase, fadeDelay);

            StartStoryboard(storyboard, (_, __) =>
            {
                _pausedMode.Visibility = Visibility.Collapsed;
                _pausedMode.Opacity = 0;
                SetWindowValues(targetSize.width, targetSize.height, targetPosition.left, targetPosition.top);
                EnsureActiveDisplayModeVisible();
                CompleteTransition();
            });
        }

        public void TriggerDropPeekToMedium(Action prepareCompactContent)
        {
            if (_disposed) return;
            CancelActivePeekAndTimer();
            StopActiveStoryboard();
            IsPeekingDrop = true;
            prepareCompactContent?.Invoke();

            var targetSize = GetScaledModeSize(1);
            var targetPosition = CalculateWindowPosition(targetSize.width, targetSize.height);
            double currentWidth = _window.Width;
            double currentHeight = _window.Height;
            double currentLeft = _window.Left;
            double currentTop = _window.Top;

            _minimalMode.BeginAnimation(UIElement.OpacityProperty, null);
            _minimalMode.Visibility = Visibility.Collapsed;
            _minimalMode.Opacity = 0;
            _compactMode.BeginAnimation(UIElement.OpacityProperty, null);
            _compactMode.Visibility = Visibility.Visible;
            _compactMode.Opacity = 1;
            _islandBorder.CornerRadius = new CornerRadius(GetModeCornerRadius(1) * _getScale());

            bool expanding = targetSize.width > currentWidth || targetSize.height > currentHeight;
            var (hEase, vEase, _) = GetWindowTransitionEasing(450, expanding);
            TimeSpan widthDuration = _getFluidPhysics() ? TimeSpan.FromMilliseconds(520) : TimeSpan.FromMilliseconds(460);
            TimeSpan heightDuration = _getFluidPhysics() ? TimeSpan.FromMilliseconds(460) : TimeSpan.FromMilliseconds(420);
            var storyboard = new Storyboard();
            AddWindowAnimation(storyboard, Window.WidthProperty, currentWidth, targetSize.width, widthDuration, new BackEase { Amplitude = _getFluidPhysics() ? 0.75 : 0.28, EasingMode = EasingMode.EaseOut });
            AddWindowAnimation(storyboard, Window.HeightProperty, currentHeight, targetSize.height, heightDuration, new BackEase { Amplitude = _getFluidPhysics() ? 0.45 : 0.18, EasingMode = EasingMode.EaseOut });
            AddWindowAnimation(storyboard, Window.LeftProperty, currentLeft, targetPosition.left, widthDuration, hEase);
            AddWindowAnimation(storyboard, Window.TopProperty, currentTop, targetPosition.top, heightDuration, vEase);

            StartStoryboard(storyboard, (_, __) =>
            {
                SetWindowValues(targetSize.width, targetSize.height, targetPosition.left, targetPosition.top);
                _compactMode.Visibility = Visibility.Visible;
                _compactMode.Opacity = 1;
                _minimalMode.Visibility = Visibility.Collapsed;
                _minimalMode.Opacity = 0;
                CompleteTransition();
                _peekTimer.Start();
            });
        }

        public void CollapseDropPeekToMinimal()
        {
            if (!IsPeekingDrop || DisplayMode != 0)
            {
                IsPeekingDrop = false;
                EnsureActiveDisplayModeVisible();
                CompleteTransition();
                return;
            }

            CancelActivePeekAndTimer();
            StopActiveStoryboard();
            var targetSize = GetScaledModeSize(0);
            var targetPosition = CalculateWindowPosition(targetSize.width, targetSize.height);
            _minimalMode.Visibility = Visibility.Visible;
            _minimalMode.Opacity = 0;
            _islandBorder.CornerRadius = new CornerRadius(GetModeCornerRadius(0) * _getScale());

            TimeSpan widthDuration = _getFluidPhysics() ? TimeSpan.FromMilliseconds(440) : TimeSpan.FromMilliseconds(380);
            TimeSpan heightDuration = _getFluidPhysics() ? TimeSpan.FromMilliseconds(400) : TimeSpan.FromMilliseconds(350);
            var storyboard = new Storyboard();
            AddWindowAnimation(storyboard, Window.WidthProperty, _window.Width, targetSize.width, widthDuration, _getFluidPhysics() ? new BackEase { Amplitude = 0.55, EasingMode = EasingMode.EaseOut } : new QuarticEase { EasingMode = EasingMode.EaseOut });
            AddWindowAnimation(storyboard, Window.HeightProperty, _window.Height, targetSize.height, heightDuration, _getFluidPhysics() ? new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut } : new CubicEase { EasingMode = EasingMode.EaseOut });
            AddWindowAnimation(storyboard, Window.LeftProperty, _window.Left, targetPosition.left, widthDuration, _getFluidPhysics() ? new BackEase { Amplitude = 0.55, EasingMode = EasingMode.EaseOut } : new QuarticEase { EasingMode = EasingMode.EaseOut });
            AddWindowAnimation(storyboard, Window.TopProperty, _window.Top, targetPosition.top, heightDuration, _getFluidPhysics() ? new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut } : new CubicEase { EasingMode = EasingMode.EaseOut });
            AddOpacityAnimation(storyboard, _compactMode, _compactMode.Opacity, 0, TimeSpan.Zero, TimeSpan.FromMilliseconds(180), new QuadraticEase { EasingMode = EasingMode.EaseOut });
            AddOpacityAnimation(storyboard, _minimalMode, 0, 1, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(220), new QuadraticEase { EasingMode = EasingMode.EaseOut });

            StartStoryboard(storyboard, (_, __) =>
            {
                SetWindowValues(targetSize.width, targetSize.height, targetPosition.left, targetPosition.top);
                _compactMode.Visibility = Visibility.Collapsed;
                _compactMode.Opacity = 0;
                IsPeekingDrop = false;
                EnsureActiveDisplayModeVisible();
                CompleteTransition();
            });
        }

        public void CancelDropPeek()
        {
            CancelActivePeekAndTimer();
            IsPeekingDrop = false;
            StopActiveStoryboard();
            EnsureActiveDisplayModeVisible();
        }

        public (double width, double height) GetModeSize(int mode) => mode switch
        {
            0 => (138, 60),
            1 => (335, 70),
            _ => (500, 176)
        };

        public double GetModeCornerRadius(int mode) => mode switch
        {
            0 => 30,
            1 => 35,
            _ => 35
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _peekTimer.Stop();
            _peekTimer.Tick -= OnPeekTimerTick;
            StopActiveStoryboard();
        }

        private void OnPeekTimerTick(object sender, EventArgs e)
        {
            _peekTimer.Stop();
            CollapseDropPeekToMinimal();
        }

        private void CompleteTransition()
        {
            _ensureTopmost?.Invoke();
            _transitionCompleted?.Invoke();
        }

        private void StartStoryboard(Storyboard storyboard, EventHandler completed)
        {
            StopActiveStoryboard();
            _activeStoryboard = storyboard;
            EventHandler trackedCompleted = null;
            trackedCompleted = (sender, args) =>
            {
                storyboard.Completed -= trackedCompleted;
                if (ReferenceEquals(_activeStoryboard, storyboard))
                {
                    _activeStoryboard = null;
                    _activeStoryboardCompleted = null;
                }
                completed(sender, args);
            };
            _activeStoryboardCompleted = trackedCompleted;
            storyboard.Completed += trackedCompleted;
            storyboard.Begin();
        }

        private void StopActiveStoryboard()
        {
            if (_activeStoryboard == null) return;
            if (_activeStoryboardCompleted != null)
                _activeStoryboard.Completed -= _activeStoryboardCompleted;
            _activeStoryboard.Stop();
            _activeStoryboard = null;
            _activeStoryboardCompleted = null;
            _window.BeginAnimation(Window.WidthProperty, null);
            _window.BeginAnimation(Window.HeightProperty, null);
            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);
        }

        private void CancelActivePeekAndTimer()
        {
            _peekTimer.Stop();
            StopActiveStoryboard();
        }

        private void ResetAnimationState(Grid currentMode)
        {
            _minimalMode.BeginAnimation(UIElement.OpacityProperty, null);
            _compactMode.BeginAnimation(UIElement.OpacityProperty, null);
            _expandedMode.BeginAnimation(UIElement.OpacityProperty, null);
            _pausedMode.BeginAnimation(UIElement.OpacityProperty, null);
            _pausedMode.Opacity = 0;
            _pausedMode.Visibility = Visibility.Collapsed;
            _minimalMode.Opacity = currentMode == _minimalMode ? 1 : 0;
            _compactMode.Opacity = currentMode == _compactMode ? 1 : 0;
            _expandedMode.Opacity = currentMode == _expandedMode ? 1 : 0;
            _minimalMode.Visibility = currentMode == _minimalMode ? Visibility.Visible : Visibility.Collapsed;
            _compactMode.Visibility = currentMode == _compactMode ? Visibility.Visible : Visibility.Collapsed;
            _expandedMode.Visibility = currentMode == _expandedMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetModeVisibility(Grid active)
        {
            _minimalMode.Visibility = active == _minimalMode ? Visibility.Visible : Visibility.Collapsed;
            _compactMode.Visibility = active == _compactMode ? Visibility.Visible : Visibility.Collapsed;
            _expandedMode.Visibility = active == _expandedMode ? Visibility.Visible : Visibility.Collapsed;
            _minimalMode.Opacity = active == _minimalMode ? 1 : 0;
            _compactMode.Opacity = active == _compactMode ? 1 : 0;
            _expandedMode.Opacity = active == _expandedMode ? 1 : 0;
            active.Opacity = 1;
        }

        private void HideAllModes()
        {
            HideAllModesExcept(null);
        }

        private void HideAllModesExcept(Grid except)
        {
            foreach (var grid in new[] { _minimalMode, _compactMode, _expandedMode })
            {
                if (grid != except)
                {
                    grid.BeginAnimation(UIElement.OpacityProperty, null);
                    grid.Visibility = Visibility.Collapsed;
                    grid.Opacity = 0;
                }
            }
        }

        private Grid GetVisibleMode()
        {
            if (_minimalMode.Visibility == Visibility.Visible) return _minimalMode;
            if (_compactMode.Visibility == Visibility.Visible) return _compactMode;
            if (_expandedMode.Visibility == Visibility.Visible) return _expandedMode;
            return null;
        }

        private Grid GetModeGrid(int mode) => mode switch
        {
            0 => _minimalMode,
            1 => _compactMode,
            _ => _expandedMode
        };

        private (double width, double height) GetScaledModeSize(int mode)
        {
            var size = GetModeSize(mode);
            return (size.width * _getScale(), size.height * _getScale());
        }

        private void SetWindowToMode(int mode)
        {
            var size = GetScaledModeSize(mode);
            var position = CalculateWindowPosition(size.width, size.height);
            SetWindowValues(size.width, size.height, position.left, position.top);
            _islandBorder.CornerRadius = new CornerRadius(GetModeCornerRadius(mode) * _getScale());
        }

        private void SetWindowValues(double width, double height, double left, double top)
        {
            _window.BeginAnimation(Window.WidthProperty, null);
            _window.BeginAnimation(Window.HeightProperty, null);
            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);
            _window.Width = width;
            _window.Height = height;
            _window.Left = left;
            _window.Top = top;
        }

        private void AddWindowAnimation(Storyboard storyboard, DependencyProperty property, double from, double to, TimeSpan duration, IEasingFunction easing, TimeSpan? begin = null)
        {
            var animation = new DoubleAnimation { From = from, To = to, Duration = duration, BeginTime = begin ?? TimeSpan.Zero, EasingFunction = easing };
            Storyboard.SetTarget(animation, _window);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            storyboard.Children.Add(animation);
        }

        private static void AddOpacityAnimation(Storyboard storyboard, UIElement target, double from, double to, TimeSpan begin, TimeSpan duration, IEasingFunction easing)
        {
            var animation = new DoubleAnimation { From = from, To = to, BeginTime = begin, Duration = duration, EasingFunction = easing };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(animation);
        }

        private (IEasingFunction horizontalEase, IEasingFunction verticalEase, TimeSpan duration) GetWindowTransitionEasing(int standardDurationMs, bool isExpanding)
        {
            if (_getFluidPhysics())
            {
                if (isExpanding)
                    return (new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }, new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut }, TimeSpan.FromMilliseconds(standardDurationMs + 40));
                return (new QuarticEase { EasingMode = EasingMode.EaseOut }, new CubicEase { EasingMode = EasingMode.EaseOut }, TimeSpan.FromMilliseconds(standardDurationMs));
            }
            return (new QuarticEase { EasingMode = EasingMode.EaseInOut }, new CubicEase { EasingMode = EasingMode.EaseInOut }, TimeSpan.FromMilliseconds(standardDurationMs));
        }
    }
}
