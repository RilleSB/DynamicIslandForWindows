using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DynamicIslandPC
{
    internal sealed class TrayMenuState
    {
        public string TrackText { get; init; }
        public bool IsPlaying { get; init; }
        public bool IsTopPosition { get; init; }
        public bool HasCustomPosition { get; init; }
        public bool IsDarkTheme { get; init; }
        public double Scale { get; init; }
        public bool GamingModeEnabled { get; init; }
        public bool LockModeEnabled { get; init; }
        public bool StartWithWindowsEnabled { get; init; }
        public bool BrowserSourceEnabled { get; init; }
        public bool DisableAlbumArtForDiagnostics { get; init; }
    }

    internal sealed class TrayMenuCallbacks
    {
        public Action OpenSettings { get; init; }
        public Action OpenDiagnostics { get; init; }
        public Func<Task> CheckForUpdatesAsync { get; init; }
        public Action ExitApplication { get; init; }
        public Action<bool> SetPosition { get; init; }
        public Action<double> SetScale { get; init; }
        public Action<bool> SetTheme { get; init; }
        public Action<bool> SetGamingMode { get; init; }
        public Action<bool> SetLockMode { get; init; }
        public Action<bool> SetStartup { get; init; }
        public Action<bool> SetBrowserSource { get; init; }
        public Action<bool> SetDisableAlbumArt { get; init; }
        public Func<Task> PreviousTrackAsync { get; init; }
        public Func<Task> PlayPauseAsync { get; init; }
        public Func<Task> NextTrackAsync { get; init; }
    }

    internal sealed class TrayMenuController : IDisposable
    {
        private TrayMenuCallbacks _callbacks;
        private readonly List<(ToolStripItem item, EventHandler handler)> _bindings = new();
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _contextMenu;
        private ToolStripMenuItem _trackItem;
        private ToolStripMenuItem _playPauseItem;
        private ToolStripMenuItem _positionTopItem;
        private ToolStripMenuItem _positionBottomItem;
        private ToolStripMenuItem _positionCustomItem;
        private ToolStripMenuItem _themeDarkItem;
        private ToolStripMenuItem _themeLightItem;
        private ToolStripMenuItem _scale100Item;
        private ToolStripMenuItem _scale125Item;
        private ToolStripMenuItem _scale150Item;
        private ToolStripMenuItem _gamingItem;
        private ToolStripMenuItem _lockItem;
        private ToolStripMenuItem _startupItem;
        private ToolStripMenuItem _browserItem;
        private ToolStripMenuItem _disableAlbumArtItem;
        private Icon _ownedIcon;
        private TrayMenuState _state;
        private bool _disposed;

        public TrayMenuController(TrayMenuCallbacks callbacks)
        {
            _callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
        }

        public void Initialize(TrayMenuState initialState)
        {
            if (_disposed || _notifyIcon != null) return;
            _state = initialState ?? new TrayMenuState();
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateApplicationIcon(),
                Text = "Dynamic Island PC",
                Visible = true
            };
            _contextMenu = new ContextMenuStrip();
            BuildMenu();
            _notifyIcon.ContextMenuStrip = _contextMenu;
            UpdateState(_state);
        }

        public void UpdateState(TrayMenuState state)
        {
            if (_disposed || state == null) return;
            _state = state;
            if (_contextMenu == null) return;

            _trackItem.Text = string.IsNullOrWhiteSpace(state.TrackText) ? "Нет медиа" : Truncate(state.TrackText, 60);
            _playPauseItem.Text = state.IsPlaying ? "Пауза" : "Воспроизведение";
            _positionTopItem.Checked = state.IsTopPosition && !state.HasCustomPosition;
            _positionBottomItem.Checked = !state.IsTopPosition && !state.HasCustomPosition;
            _positionCustomItem.Checked = state.HasCustomPosition;
            _themeDarkItem.Checked = state.IsDarkTheme;
            _themeLightItem.Checked = !state.IsDarkTheme;
            _scale100Item.Checked = Math.Abs(state.Scale - 1.0) < 0.01;
            _scale125Item.Checked = Math.Abs(state.Scale - 1.25) < 0.01;
            _scale150Item.Checked = Math.Abs(state.Scale - 1.5) < 0.01;
            _gamingItem.Checked = state.GamingModeEnabled;
            _lockItem.Checked = state.LockModeEnabled;
            _startupItem.Checked = state.StartWithWindowsEnabled;
            _browserItem.Checked = state.BrowserSourceEnabled;
            _disableAlbumArtItem.Checked = state.DisableAlbumArtForDiagnostics;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.ContextMenuStrip = null;
            }

            foreach (var binding in _bindings)
                binding.item.Click -= binding.handler;
            _bindings.Clear();

            _contextMenu?.Dispose();
            _contextMenu = null;
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            _ownedIcon?.Dispose();
            _ownedIcon = null;
            _trackItem = null;
            _playPauseItem = null;
            _state = null;
            _callbacks = null;
        }

        private void BuildMenu()
        {
            var positionMenu = new ToolStripMenuItem("Позиция");
            _positionTopItem = Bind(new ToolStripMenuItem("Вверху"), () => _callbacks.SetPosition?.Invoke(true));
            _positionBottomItem = Bind(new ToolStripMenuItem("Внизу"), () => _callbacks.SetPosition?.Invoke(false));
            _positionCustomItem = Bind(new ToolStripMenuItem("Настройки позиции..."), () => _callbacks.OpenSettings?.Invoke());
            positionMenu.DropDownItems.Add(_positionTopItem);
            positionMenu.DropDownItems.Add(_positionBottomItem);
            positionMenu.DropDownItems.Add(_positionCustomItem);
            _contextMenu.Items.Add(positionMenu);

            var themeMenu = new ToolStripMenuItem("Тема");
            _themeDarkItem = Bind(new ToolStripMenuItem("Тёмная"), () => _callbacks.SetTheme?.Invoke(true));
            _themeLightItem = Bind(new ToolStripMenuItem("Светлая"), () => _callbacks.SetTheme?.Invoke(false));
            themeMenu.DropDownItems.Add(_themeDarkItem);
            themeMenu.DropDownItems.Add(_themeLightItem);
            _contextMenu.Items.Add(themeMenu);

            var scaleMenu = new ToolStripMenuItem("Масштаб");
            _scale100Item = Bind(new ToolStripMenuItem("100%"), () => _callbacks.SetScale?.Invoke(1.0));
            _scale125Item = Bind(new ToolStripMenuItem("125%"), () => _callbacks.SetScale?.Invoke(1.25));
            _scale150Item = Bind(new ToolStripMenuItem("150%"), () => _callbacks.SetScale?.Invoke(1.5));
            scaleMenu.DropDownItems.Add(_scale100Item);
            scaleMenu.DropDownItems.Add(_scale125Item);
            scaleMenu.DropDownItems.Add(_scale150Item);
            _contextMenu.Items.Add(scaleMenu);

            _gamingItem = Bind(new ToolStripMenuItem("Игровой режим (клики насквозь)"), () => _callbacks.SetGamingMode?.Invoke(!(_state?.GamingModeEnabled ?? false)));
            _lockItem = Bind(new ToolStripMenuItem("Зафиксировать размер (без клика)"), () => _callbacks.SetLockMode?.Invoke(!(_state?.LockModeEnabled ?? false)));
            _startupItem = Bind(new ToolStripMenuItem("Автозапуск с Windows"), () => _callbacks.SetStartup?.Invoke(!(_state?.StartWithWindowsEnabled ?? false)));
            _contextMenu.Items.Add(_gamingItem);
            _contextMenu.Items.Add(_lockItem);
            _contextMenu.Items.Add(_startupItem);

            var sourcesMenu = new ToolStripMenuItem("Источники");
            _browserItem = Bind(new ToolStripMenuItem("Браузер"), () => _callbacks.SetBrowserSource?.Invoke(!(_state?.BrowserSourceEnabled ?? false)));
            sourcesMenu.DropDownItems.Add(_browserItem);
            _contextMenu.Items.Add(sourcesMenu);

            _disableAlbumArtItem = Bind(new ToolStripMenuItem("Диагностика: не загружать обложки"), () => _callbacks.SetDisableAlbumArt?.Invoke(!(_state?.DisableAlbumArtForDiagnostics ?? false)));
            _contextMenu.Items.Add(_disableAlbumArtItem);

            var musicMenu = new ToolStripMenuItem("Управление музыкой");
            _trackItem = new ToolStripMenuItem("Нет медиа") { Enabled = false };
            var previous = Bind(new ToolStripMenuItem("Предыдущий трек"), () => _ = InvokeAsync(_callbacks.PreviousTrackAsync));
            _playPauseItem = Bind(new ToolStripMenuItem("Воспроизведение / Пауза"), () => _ = InvokeAsync(_callbacks.PlayPauseAsync));
            var next = Bind(new ToolStripMenuItem("Следующий трек"), () => _ = InvokeAsync(_callbacks.NextTrackAsync));
            musicMenu.DropDownItems.Add(_trackItem);
            musicMenu.DropDownItems.Add(new ToolStripSeparator());
            musicMenu.DropDownItems.Add(previous);
            musicMenu.DropDownItems.Add(_playPauseItem);
            musicMenu.DropDownItems.Add(next);
            _contextMenu.Items.Add(musicMenu);

            _contextMenu.Items.Add(Bind(new ToolStripMenuItem("Диагностика..."), () => _callbacks.OpenDiagnostics?.Invoke()));
            _contextMenu.Items.Add(Bind(new ToolStripMenuItem("Проверить обновления..."), () => _ = InvokeAsync(_callbacks.CheckForUpdatesAsync)));
            _contextMenu.Items.Add(Bind(new ToolStripMenuItem("Выход"), ExitApplication));
        }

        private ToolStripMenuItem Bind(ToolStripMenuItem item, Action action)
        {
            EventHandler handler = (_, __) => action();
            item.Click += handler;
            _bindings.Add((item, handler));
            return item;
        }

        private async Task InvokeAsync(Func<Task> action)
        {
            if (!_disposed && action != null)
                await action();
        }

        private void ExitApplication()
        {
            if (_disposed) return;
            if (_notifyIcon != null)
                _notifyIcon.Visible = false;
            _callbacks.ExitApplication?.Invoke();
        }

        private Icon CreateApplicationIcon()
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    _ownedIcon = Icon.ExtractAssociatedIcon(exePath);
                    if (_ownedIcon != null) return _ownedIcon;
                }
            }
            catch
            {
                // Fall back to the standard application icon below.
            }
            return SystemIcons.Application;
        }

        private static string Truncate(string text, int maxLength)
        {
            return text.Length > maxLength ? text.Substring(0, maxLength - 3) + "..." : text;
        }
    }
}
