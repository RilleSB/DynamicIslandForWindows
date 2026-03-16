using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Windows.Media.Imaging;

namespace DynamicIslandPC
{
    public class SMTCService
    {
        private MusicInfo lastKnownInfo = null;
        private GlobalSystemMediaTransportControlsSessionManager sessionManager = null;
        private GlobalSystemMediaTransportControlsSession currentSession = null;
        private bool initialized = false;

        public event Action<MusicInfo> MusicInfoChanged;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            
            Task.Run(async () =>
            {
                try
                {
                    sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                    sessionManager.CurrentSessionChanged += OnSessionChanged;
                    SubscribeToSession(sessionManager.GetCurrentSession());
                    await FetchAndNotify();
                }
                catch { }
            });
        }

        private void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            SubscribeToSession(sender.GetCurrentSession());
            Task.Run(FetchAndNotify);
        }

        private void SubscribeToSession(GlobalSystemMediaTransportControlsSession session)
        {
            if (currentSession != null)
            {
                currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }
            
            currentSession = session;
            
            if (currentSession != null)
            {
                currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
            }
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            Task.Run(FetchAndNotify);
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            Task.Run(FetchAndNotify);
        }

        private async Task FetchAndNotify()
        {
            var info = await FetchCurrentInfo();
            if (info != null)
            {
                lastKnownInfo = info;
                MusicInfoChanged?.Invoke(info);
            }
        }

        private async Task<MusicInfo> FetchCurrentInfo()
        {
            try
            {
                var session = currentSession ?? sessionManager?.GetCurrentSession();
                if (session == null) return lastKnownInfo;

                var props = await session.TryGetMediaPropertiesAsync();
                if (props == null || string.IsNullOrEmpty(props.Title)) return lastKnownInfo;

                var timeline = session.GetTimelineProperties();
                var info = new MusicInfo
                {
                    Title = props.Title,
                    Artist = props.Artist,
                    IsPlaying = session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    Position = timeline?.Position ?? TimeSpan.Zero,
                    Duration = timeline?.EndTime ?? TimeSpan.Zero
                };

                if (props.Thumbnail != null)
                {
                    try
                    {
                        var stream = await props.Thumbnail.OpenReadAsync();
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream.AsStreamForRead();
                        image.EndInit();
                        image.Freeze();
                        info.AlbumArt = image;
                    }
                    catch { }
                }

                return info;
            }
            catch { }
            return lastKnownInfo;
        }

        public MusicInfo GetLastKnownInfo() => lastKnownInfo;
    }
}
