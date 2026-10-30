using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DynamicIslandPC
{
    internal sealed class PlaybackUiState
    {
        public string TrackKey { get; set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsPaused { get; set; }

        public PlaybackUiState Clone() => new()
        {
            TrackKey = TrackKey,
            Position = Position,
            Duration = Duration,
            IsPlaying = IsPlaying,
            IsPaused = IsPaused
        };
    }

    internal sealed class PlaybackUiController : IDisposable
    {
        private readonly Rectangle _compactTrack;
        private readonly Rectangle _compactBar;
        private readonly Ellipse _compactDot;
        private readonly Rectangle _expandedTrack;
        private readonly Rectangle _expandedBar;
        private readonly Ellipse _expandedDot;
        private readonly TextBlock _currentTime;
        private readonly TextBlock _totalTime;
        private readonly DispatcherTimer _progressTimer;
        private readonly Func<Task> _playPauseAsync;
        private readonly Func<Task> _nextAsync;
        private readonly Func<Task> _previousAsync;
        private readonly Func<TimeSpan, Task> _seekAsync;
        private readonly Action<TimeSpan> _positionChanged;
        private readonly Func<bool> _isVinylScratchEnabled;
        private readonly Func<bool> _isVinylRotationEnabled;
        private readonly Func<bool> _isWindowClosing;
        private readonly AlbumArtPresenter _albumArtPresenter;

        private PlaybackUiState _state;
        private DateTime _lastSeekAt = DateTime.MinValue;
        private DateTime _lastProgressTickAt = DateTime.UtcNow;
        private Grid _activeScrubber;
        private bool _isScrubbing;
        private bool _disposed;
        private bool _started;

        private bool _isScratching;
        private FrameworkElement _scratchContainer;
        private Point _scratchCenter;
        private Vector _lastScratchVector;
        private double _accumulatedAngle;
        private double _scratchTravel;
        private TimeSpan _scratchBasePosition;
        private TimeSpan _scratchCurrentPosition;
        private double _scratchAngle;

        public PlaybackUiController(
            Rectangle compactTrack, Rectangle compactBar, Ellipse compactDot,
            Rectangle expandedTrack, Rectangle expandedBar, Ellipse expandedDot,
            TextBlock currentTime, TextBlock totalTime,
            AlbumArtPresenter albumArtPresenter,
            Func<Task> playPauseAsync, Func<Task> nextAsync, Func<Task> previousAsync,
            Func<TimeSpan, Task> seekAsync, Action<TimeSpan> positionChanged,
            Func<bool> isVinylScratchEnabled, Func<bool> isVinylRotationEnabled,
            Func<bool> isWindowClosing)
        {
            _compactTrack = compactTrack;
            _compactBar = compactBar;
            _compactDot = compactDot;
            _expandedTrack = expandedTrack;
            _expandedBar = expandedBar;
            _expandedDot = expandedDot;
            _currentTime = currentTime;
            _totalTime = totalTime;
            _albumArtPresenter = albumArtPresenter;
            _playPauseAsync = playPauseAsync;
            _nextAsync = nextAsync;
            _previousAsync = previousAsync;
            _seekAsync = seekAsync;
            _positionChanged = positionChanged;
            _isVinylScratchEnabled = isVinylScratchEnabled;
            _isVinylRotationEnabled = isVinylRotationEnabled;
            _isWindowClosing = isWindowClosing;
            _progressTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(250) };
            _progressTimer.Tick += OnProgressTimerTick;
            _expandedTrack.SizeChanged += OnProgressTrackSizeChanged;
        }

        public bool IsScrubbing => _isScrubbing;

        public void Start()
        {
            if (_disposed || _started) return;
            _started = true;
            _lastProgressTickAt = DateTime.UtcNow;
            _progressTimer.Start();
        }

        public void UpdateState(PlaybackUiState incoming)
        {
            if (_disposed || incoming == null) return;

            var next = incoming.Clone();
            bool sameTrack = _state != null && string.Equals(_state.TrackKey, incoming.TrackKey, StringComparison.Ordinal);
            if (sameTrack && _state != null)
            {
                bool inSeekCooldown = DateTime.UtcNow - _lastSeekAt < TimeSpan.FromSeconds(1.5);
                double difference = Math.Abs((incoming.Position - _state.Position).TotalSeconds);
                if (inSeekCooldown || (difference <= 2.0 && _state.Position != TimeSpan.Zero))
                    next.Position = _state.Position;
            }

            _state = next;
            _lastProgressTickAt = DateTime.UtcNow;
            SetProgressRatio(_state.Duration > TimeSpan.Zero
                ? _state.Position.TotalSeconds / _state.Duration.TotalSeconds
                : 0);
        }

        public void SetProgressRatio(double ratio)
        {
            if (_disposed || _state?.IsPaused == true) return;
            ApplyProgressVisuals(Math.Clamp(ratio, 0.0, 1.0), updateTime: !_isScrubbing);
        }

        public void HandleWheel(int delta)
        {
            if (_disposed || delta == 0) return;
            _ = RunCommandAsync(delta > 0 ? _previousAsync : _nextAsync, "mouse wheel playback command");
        }

        public void HandleMiddleClick()
        {
            if (_disposed) return;
            _ = RunCommandAsync(_playPauseAsync, "middle-click playback command");
        }

        public void PlayPauseButtonClick()
        {
            _ = RunCommandAsync(_playPauseAsync, "play/pause command");
        }

        public void NextButtonClick()
        {
            _albumArtPresenter?.SetSlideDirection(-1);
            _ = RunCommandAsync(_nextAsync, "next-track command");
        }

        public void PreviousButtonClick()
        {
            _albumArtPresenter?.SetSlideDirection(1);
            _ = RunCommandAsync(_previousAsync, "previous-track command");
        }

        public void ProgressMouseDown(Grid container, double mouseX)
        {
            if (_disposed || _state == null || _state.Duration <= TimeSpan.Zero) return;
            _isScrubbing = true;
            _activeScrubber = container;
            _activeScrubber?.CaptureMouse();
            ApplyScrub(mouseX, commit: false);
        }

        public void ProgressMouseMove(double mouseX, bool leftButtonPressed)
        {
            if (_isScrubbing && _activeScrubber != null && leftButtonPressed)
                ApplyScrub(mouseX, commit: false);
        }

        public void ProgressMouseUp(double mouseX)
        {
            if (!_isScrubbing || _activeScrubber == null) return;
            var container = _activeScrubber;
            ApplyScrub(mouseX, commit: true);
            container.ReleaseMouseCapture();
            _isScrubbing = false;
            _activeScrubber = null;
        }

        public void ProgressLostMouseCapture()
        {
            if (!_isScrubbing) return;
            _isScrubbing = false;
            _activeScrubber = null;
        }

        public void VinylMouseDown(FrameworkElement container, Point position)
        {
            if (_disposed || container == null) return;
            if (!_isVinylScratchEnabled())
            {
                _ = RunCommandAsync(_playPauseAsync, "vinyl click playback command");
                return;
            }

            _isScratching = true;
            _scratchContainer = container;
            _scratchContainer.CaptureMouse();
            _scratchCenter = new Point(container.ActualWidth / 2.0, container.ActualHeight / 2.0);
            _lastScratchVector = position - _scratchCenter;
            _accumulatedAngle = 0;
            _scratchTravel = 0;
            _scratchBasePosition = _state?.Position ?? TimeSpan.Zero;
            _scratchCurrentPosition = _scratchBasePosition;
            _scratchAngle = _albumArtPresenter?.CurrentVinylAngle ?? 0;
            _albumArtPresenter?.DetachVinylClocksForScratch(_scratchAngle);
        }

        public void VinylMouseMove(Point position, bool leftButtonPressed)
        {
            if (!_isScratching || _scratchContainer == null) return;
            if (!leftButtonPressed)
            {
                EndVinylScratch(commit: true);
                return;
            }

            Vector currentVector = position - _scratchCenter;
            if (currentVector.Length < 3.0) return;
            double previousAngle = Math.Atan2(_lastScratchVector.Y, _lastScratchVector.X);
            double currentAngle = Math.Atan2(currentVector.Y, currentVector.X);
            double delta = currentAngle - previousAngle;
            while (delta > Math.PI) delta -= 2 * Math.PI;
            while (delta < -Math.PI) delta += 2 * Math.PI;

            _accumulatedAngle += delta;
            _scratchTravel += Math.Abs(delta);
            _lastScratchVector = currentVector;
            _scratchAngle += delta * (180.0 / Math.PI);
            _albumArtPresenter?.SetVinylManualAngle(_scratchAngle);

            if (_state != null && _state.Duration > TimeSpan.Zero)
            {
                double targetSeconds = Math.Clamp(
                    _scratchBasePosition.TotalSeconds + (_accumulatedAngle / (2 * Math.PI)) * 15.0,
                    0.0, _state.Duration.TotalSeconds);
                _scratchCurrentPosition = TimeSpan.FromSeconds(targetSeconds);
                SetPosition(_scratchCurrentPosition);
                ApplyProgressVisuals(targetSeconds / _state.Duration.TotalSeconds, updateTime: true);
            }
        }

        public void VinylMouseUp(bool commit)
        {
            if (_isScratching)
                EndVinylScratch(commit);
        }

        public void EndVinylScratch(bool commit)
        {
            if (!_isScratching) return;
            _isScratching = false;
            try { _scratchContainer?.ReleaseMouseCapture(); } catch { }
            _scratchContainer = null;

            if (_scratchTravel < 0.10)
            {
                _ = RunCommandAsync(_playPauseAsync, "vinyl tap playback command");
            }
            else if (commit)
            {
                _lastSeekAt = DateTime.UtcNow;
                SetPosition(_scratchCurrentPosition);
                _ = RunCommandAsync(() => _seekAsync?.Invoke(_scratchCurrentPosition), "vinyl seek command");
            }

            if (_isVinylRotationEnabled() && _state?.IsPlaying == true)
                _albumArtPresenter?.StartRotation(_scratchAngle);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _progressTimer.Stop();
            _progressTimer.Tick -= OnProgressTimerTick;
            _expandedTrack.SizeChanged -= OnProgressTrackSizeChanged;
            try { _activeScrubber?.ReleaseMouseCapture(); } catch { }
            try { _scratchContainer?.ReleaseMouseCapture(); } catch { }
            _activeScrubber = null;
            _scratchContainer = null;
            _isScrubbing = false;
            _isScratching = false;
        }

        private void OnProgressTimerTick(object sender, EventArgs e)
        {
            if (_disposed || _isWindowClosing() || _isScrubbing || _state == null || _state.Duration <= TimeSpan.Zero)
                return;

            var now = DateTime.UtcNow;
            var delta = now - _lastProgressTickAt;
            _lastProgressTickAt = now;
            if (_state.IsPlaying && !_state.IsPaused)
            {
                var nextPosition = _state.Position + delta;
                if (nextPosition > _state.Duration) nextPosition = _state.Duration;
                SetPosition(nextPosition);
            }
            SetProgressRatio(_state.Position.TotalSeconds / _state.Duration.TotalSeconds);
        }

        private void OnProgressTrackSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isScrubbing && _state?.Duration > TimeSpan.Zero)
                SetProgressRatio(_state.Position.TotalSeconds / _state.Duration.TotalSeconds);
        }

        private void ApplyScrub(double mouseX, bool commit)
        {
            if (_state == null || _state.Duration <= TimeSpan.Zero || _activeScrubber == null) return;
            double width = _activeScrubber.ActualWidth;
            if (width <= 0) return;
            double ratio = Math.Clamp(mouseX / width, 0.0, 1.0);
            var position = TimeSpan.FromSeconds(ratio * _state.Duration.TotalSeconds);
            SetPosition(position);
            ApplyProgressVisuals(ratio, updateTime: true);
            if (commit)
            {
                _lastSeekAt = DateTime.UtcNow;
                _ = RunCommandAsync(() => _seekAsync?.Invoke(position), "scrub seek command");
            }
        }

        private void SetPosition(TimeSpan position)
        {
            if (_state == null) return;
            var next = _state.Clone();
            next = new PlaybackUiState
            {
                TrackKey = next.TrackKey,
                Position = position,
                Duration = next.Duration,
                IsPlaying = next.IsPlaying,
                IsPaused = next.IsPaused
            };
            _state = next;
            _positionChanged?.Invoke(position);
        }

        private void ApplyProgressVisuals(double ratio, bool updateTime)
        {
            ratio = Math.Clamp(ratio, 0.0, 1.0);
            ApplyProgress(_compactTrack, _compactBar, _compactDot, ratio);
            ApplyProgress(_expandedTrack, _expandedBar, _expandedDot, ratio);
            if (updateTime && _state != null)
            {
                _currentTime.Text = FormatTime(_state.Position);
                var remaining = _state.Duration > TimeSpan.Zero ? _state.Duration - _state.Position : TimeSpan.Zero;
                if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                _totalTime.Text = "-" + FormatTime(remaining);
            }
        }

        private static void ApplyProgress(Rectangle track, Rectangle bar, Ellipse dot, double ratio)
        {
            if (track == null || bar == null || dot == null || track.ActualWidth <= 0) return;
            double filled = track.ActualWidth * ratio;
            bar.Width = filled;
            dot.Margin = new Thickness(Math.Max(0, filled - dot.Width / 2), 0, 0, 0);
        }

        private static string FormatTime(TimeSpan time)
        {
            return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
        }

        private async Task RunCommandAsync(Func<Task> command, string operation)
        {
            if (_disposed || command == null) return;
            try { await command(); }
            catch (Exception ex) { Logger.Error($"Failed to execute {operation}", ex); }
        }
    }
}
