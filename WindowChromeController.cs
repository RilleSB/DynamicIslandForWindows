using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DynamicIslandPC
{
    /// <summary>
    /// Owns the native window integration that is independent from Island mode layout.
    /// </summary>
    public sealed class WindowChromeController : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_SPACE = 0x20;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const uint GW_HWNDPREV = 3;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private const uint WDA_NONE = 0x00000000;
        private const uint WDA_MONITOR = 0x00000001;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

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

        private Window _window;
        private Action _hotkeyAction;
        private Func<bool> _isSmartHidden;
        private HwndSource _source;
        private DispatcherTimer _topmostWatchdogTimer;
        private IntPtr _windowHandle;
        private bool _hotKeyRegistered;
        private bool _hookAttached;
        private bool _sourceInitialized;
        private bool _gamingModeEnabled;
        private bool _excludeFromCapture;
        private bool _disposed;

        public WindowChromeController(Window window, Action hotkeyAction, Func<bool> isSmartHidden)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _hotkeyAction = hotkeyAction;
            _isSmartHidden = isSmartHidden;

            _window.Deactivated += OnWindowDeactivated;
            _window.IsVisibleChanged += OnWindowIsVisibleChanged;
            _window.StateChanged += OnWindowStateChanged;
        }

        public void ApplySettings(bool gamingModeEnabled, bool excludeFromCapture)
        {
            if (_disposed)
                return;

            _gamingModeEnabled = gamingModeEnabled;
            _excludeFromCapture = excludeFromCapture;
            ApplyClickThroughMode();
            ApplyDisplayAffinity();
        }

        public void OnSourceInitialized()
        {
            if (_disposed || _sourceInitialized || _window == null)
                return;

            var helper = new WindowInteropHelper(_window);
            _windowHandle = helper.Handle;
            if (_windowHandle == IntPtr.Zero)
                return;

            _source = HwndSource.FromHwnd(_windowHandle);
            if (_source != null)
            {
                _source.AddHook(HwndHook);
                _hookAttached = true;
            }

            _hotKeyRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID, MOD_CONTROL, VK_SPACE);
            if (!_hotKeyRegistered)
                Logger.Error("Failed to register global hotkey Ctrl+Space");

            _sourceInitialized = true;
            ApplyDisplayAffinity();
            ApplyClickThroughMode();
            StartTopmostWatchdog();
            EnsureTopmost();
        }

        public void EnsureTopmost()
        {
            if (_disposed || _window == null || !_window.IsVisible || (_isSmartHidden?.Invoke() ?? false))
                return;

            try
            {
                var hwnd = _windowHandle;
                if (hwnd == IntPtr.Zero)
                    hwnd = new WindowInteropHelper(_window).Handle;
                if (hwnd == IntPtr.Zero)
                    return;

                var previousWindow = GetWindow(hwnd, GW_HWNDPREV);
                if (previousWindow != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }

                if (!_window.Topmost)
                    _window.Topmost = true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to ensure window topmost", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopTopmostWatchdog();

            if (_source != null && _hookAttached)
            {
                _source.RemoveHook(HwndHook);
                _hookAttached = false;
            }

            if (_hotKeyRegistered && _windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
                _hotKeyRegistered = false;
            }

            if (_window != null)
            {
                _window.Deactivated -= OnWindowDeactivated;
                _window.IsVisibleChanged -= OnWindowIsVisibleChanged;
                _window.StateChanged -= OnWindowStateChanged;
            }

            _source = null;
            _window = null;
            _hotkeyAction = null;
            _isSmartHidden = null;
        }

        private void StartTopmostWatchdog()
        {
            if (_topmostWatchdogTimer != null)
                return;

            _topmostWatchdogTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(3.0)
            };
            _topmostWatchdogTimer.Tick += OnTopmostWatchdogTick;
            _topmostWatchdogTimer.Start();
        }

        private void StopTopmostWatchdog()
        {
            if (_topmostWatchdogTimer == null)
                return;

            _topmostWatchdogTimer.Tick -= OnTopmostWatchdogTick;
            _topmostWatchdogTimer.Stop();
            _topmostWatchdogTimer = null;
        }

        private void OnTopmostWatchdogTick(object sender, EventArgs e)
        {
            EnsureTopmost();
        }

        private void OnWindowDeactivated(object sender, EventArgs e)
        {
            EnsureTopmost();
        }

        private void OnWindowIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_window?.IsVisible == true)
                EnsureTopmost();
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            if (_window == null)
                return;

            if (_window.WindowState == WindowState.Minimized)
                _window.WindowState = WindowState.Normal;
            EnsureTopmost();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                _hotkeyAction?.Invoke();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void ApplyClickThroughMode()
        {
            if (_disposed || _windowHandle == IntPtr.Zero)
                return;

            try
            {
                var style = GetWindowLong(_windowHandle, GWL_EXSTYLE);
                var newStyle = _gamingModeEnabled
                    ? style | WS_EX_TRANSPARENT
                    : style & ~WS_EX_TRANSPARENT;

                if (newStyle != style)
                    SetWindowLong(_windowHandle, GWL_EXSTYLE, newStyle);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply click-through gaming mode", ex);
            }
        }

        private void ApplyDisplayAffinity()
        {
            if (_disposed || _windowHandle == IntPtr.Zero)
                return;

            try
            {
                var affinity = _excludeFromCapture ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE;
                var ok = SetWindowDisplayAffinity(_windowHandle, affinity);
                var error = Marshal.GetLastWin32Error();

                if (!ok && _excludeFromCapture)
                {
                    Logger.Error($"SetWindowDisplayAffinity(0x{affinity:X}) failed (err={error}), attempting fallback to WDA_MONITOR");
                    var fallbackOk = SetWindowDisplayAffinity(_windowHandle, WDA_MONITOR);
                    var fallbackError = Marshal.GetLastWin32Error();
                    Logger.Log($"Fallback SetWindowDisplayAffinity(WDA_MONITOR) result: {fallbackOk}, err={fallbackError}");
                }

                GetWindowDisplayAffinity(_windowHandle, out var currentAffinity);
                Logger.Log($"ApplyDisplayAffinity finished for hWnd=0x{_windowHandle:X}: requested=0x{affinity:X}, ok={ok}, err={error}, actualAffinity=0x{currentAffinity:X}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply window display affinity", ex);
            }
        }
    }
}
