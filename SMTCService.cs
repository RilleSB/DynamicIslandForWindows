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
        private DateTime lastSuccessTime = DateTime.MinValue;
        
        public string DebugInfo { get; private set; } = "";

        public MusicInfo GetMediaFromSMTC()
        {
            var musicInfo = new MusicInfo();

            try
            {
                var sessionManager = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync().GetAwaiter().GetResult();
                
                if (sessionManager == null)
                    return musicInfo;

                var sessions = sessionManager.GetSessions();
                
                foreach (var session in sessions)
                {
                    if (session == null)
                        continue;

                    try
                    {
                        var props = session.TryGetMediaPropertiesAsync().GetAwaiter().GetResult();
                        var timeline = session.GetTimelineProperties();

                        if (props != null && !string.IsNullOrEmpty(props.Title))
                        {
                            musicInfo.Title = props.Title;
                            musicInfo.Artist = props.Artist;
                            musicInfo.IsPlaying = true;
                            
                            // Получаем время только если оно валидное
                            if (timeline != null && timeline.EndTime.TotalSeconds > 0)
                            {
                                musicInfo.Position = timeline.Position;
                                musicInfo.Duration = timeline.EndTime;
                            }
                            
                            // Загружаем обложку
                            if (props.Thumbnail != null)
                            {
                                try
                                {
                                    var stream = props.Thumbnail.OpenReadAsync().GetAwaiter().GetResult();
                                    var image = new BitmapImage();
                                    image.BeginInit();
                                    image.CacheOption = BitmapCacheOption.OnLoad;
                                    image.StreamSource = stream.AsStreamForRead();
                                    image.EndInit();
                                    image.Freeze();
                                    musicInfo.AlbumArt = image;
                                }
                                catch { }
                            }
                            
                            lastKnownInfo = musicInfo;
                            lastSuccessTime = DateTime.Now;
                            return musicInfo;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            if (lastKnownInfo != null && (DateTime.Now - lastSuccessTime).TotalSeconds < 10)
            {
                return lastKnownInfo;
            }

            return musicInfo;
        }
    }
}
