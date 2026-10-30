using System;
using System.Windows;
using System.Windows.Threading;

namespace DynamicIslandPC
{
    /// <summary>
    /// Owns the window-level smart visibility state. The current application policy
    /// keeps the island visible whenever music state is refreshed; delayed hiding
    /// remains explicit so a future policy cannot create duplicate timers.
    /// </summary>
    public sealed class SmartVisibilityController : IDisposable
    {
        private Window _window;
        private Action _ensureTopmost;
        private DispatcherTimer _hideTimer;
        private bool _isSmartHidden;
        private bool _disposed;

        public SmartVisibilityController(Window window, Action ensureTopmost)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _ensureTopmost = ensureTopmost;
        }

        public bool IsSmartHidden => _isSmartHidden;

        public void UpdateState(bool hasMedia, bool isPlaying, bool trackChanged)
        {
            if (_disposed)
                return;

            CancelScheduledHide();
            ShowIsland();
            _ensureTopmost?.Invoke();
        }

        public void ForceShow()
        {
            if (_disposed)
                return;

            CancelScheduledHide();
            ShowIsland();
            _ensureTopmost?.Invoke();
        }

        public void ScheduleHide(TimeSpan delay)
        {
            if (_disposed || delay < TimeSpan.Zero)
                return;

            if (_hideTimer == null)
            {
                _hideTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = delay
                };
                _hideTimer.Tick += OnHideTimerTick;
            }
            else
            {
                _hideTimer.Interval = delay;
            }

            _hideTimer.Stop();
            _hideTimer.Start();
        }

        public void HideForSmartState()
        {
            if (_disposed || _window == null)
                return;

            CancelScheduledHide();
            _isSmartHidden = true;
            _window.BeginAnimation(UIElement.OpacityProperty, null);
            _window.Opacity = 0;
            _window.Hide();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CancelScheduledHide();
            if (_window != null)
                _window.BeginAnimation(UIElement.OpacityProperty, null);

            _window = null;
            _ensureTopmost = null;
        }

        private void OnHideTimerTick(object sender, EventArgs e)
        {
            CancelScheduledHide();
            HideForSmartState();
        }

        private void ShowIsland()
        {
            if (_window == null)
                return;

            _isSmartHidden = false;
            _window.BeginAnimation(UIElement.OpacityProperty, null);
            _window.Opacity = 1;
            if (!_window.IsVisible)
                _window.Show();
        }

        private void CancelScheduledHide()
        {
            if (_hideTimer == null)
                return;

            _hideTimer.Stop();
            _hideTimer.Tick -= OnHideTimerTick;
            _hideTimer = null;
        }
    }
}
