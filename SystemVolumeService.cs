using System;
using System.Windows.Threading;
using NAudio.CoreAudioApi;

namespace DynamicIslandPC
{
    internal sealed class SystemVolumeService : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _deviceTimer;
        private MMDeviceEnumerator _enumerator;
        private MMDevice _device;
        private bool _disposed;
        private bool _settingVolume;
        private readonly Action<double> _volumeChanged;

        public SystemVolumeService(Dispatcher dispatcher, Action<double> volumeChanged)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _volumeChanged = volumeChanged ?? throw new ArgumentNullException(nameof(volumeChanged));
            _deviceTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(750)
            };
            _deviceTimer.Tick += OnDeviceTimerTick;
            TryBindDefaultDevice();
            _deviceTimer.Start();
        }

        public void SetVolume(double percent)
        {
            if (_disposed) return;
            try
            {
                if (_device == null)
                    TryBindDefaultDevice();
                if (_device?.AudioEndpointVolume == null) return;

                _settingVolume = true;
                _device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)Math.Clamp(percent / 100.0, 0.0, 1.0);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to set system volume", ex);
            }
            finally
            {
                _settingVolume = false;
            }
        }

        private void TryBindDefaultDevice()
        {
            if (_disposed) return;
            try
            {
                _enumerator ??= new MMDeviceEnumerator();
                var next = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                if (next == null) return;

                if (_device != null && string.Equals(_device.ID, next.ID, StringComparison.Ordinal))
                {
                    next.Dispose();
                    PublishVolume();
                    return;
                }

                UnbindDevice();
                _device = next;
                _device.AudioEndpointVolume.OnVolumeNotification += OnVolumeNotification;
                PublishVolume();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to bind default audio output device", ex);
                UnbindDevice();
            }
        }

        private void OnDeviceTimerTick(object sender, EventArgs e) => TryBindDefaultDevice();

        private void OnVolumeNotification(AudioVolumeNotificationData data)
        {
            if (_disposed || _settingVolume) return;
            PublishVolume(data.MasterVolume * 100.0f);
        }

        private void PublishVolume(double percent = double.NaN)
        {
            if (_disposed || _device?.AudioEndpointVolume == null) return;
            try
            {
                if (double.IsNaN(percent))
                    percent = _device.AudioEndpointVolume.MasterVolumeLevelScalar * 100.0;
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!_disposed) _volumeChanged(Math.Clamp(percent, 0.0, 100.0));
                }));
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to publish system volume", ex);
            }
        }

        private void UnbindDevice()
        {
            if (_device == null) return;
            try { _device.AudioEndpointVolume.OnVolumeNotification -= OnVolumeNotification; }
            catch (Exception ex) { Logger.Error("Failed to unsubscribe from system volume notifications", ex); }
            _device.Dispose();
            _device = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _deviceTimer.Stop();
            _deviceTimer.Tick -= OnDeviceTimerTick;
            UnbindDevice();
            _enumerator?.Dispose();
            _enumerator = null;
        }
    }
}
