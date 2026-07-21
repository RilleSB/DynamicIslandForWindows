using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
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
        private string lastDiagnostics = "SMTC is not initialized yet.";

        public bool BrowserSourceEnabled { get; set; } = true;

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
                    sessionManager.SessionsChanged += OnSessionsChanged;
                    await FetchAndNotify();
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to initialize SMTC session manager", ex);
                }
            });
        }

        private void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            Task.Run(FetchAndNotify);
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
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
                var session = await SelectBestSessionAsync();
                if (session == null)
                    return new MusicInfo
                    {
                        SourceApp = "Media",
                        HasMedia = false,
                        IsPlaying = false,
                        Position = TimeSpan.Zero,
                        Duration = TimeSpan.Zero
                    };

                SubscribeToSession(session);
                var props = await session.TryGetMediaPropertiesAsync();
                if (props == null || string.IsNullOrWhiteSpace(props.Title))
                    return new MusicInfo
                    {
                        SourceApp = MusicVisualHelper.NormalizeSource(session.SourceAppUserModelId),
                        HasMedia = false,
                        IsPlaying = false,
                        Position = TimeSpan.Zero,
                        Duration = TimeSpan.Zero
                    };

                var timeline = session.GetTimelineProperties();
                var rawTitle = props.Title;
                var rawArtist = props.Artist;
                var rawSource = session.SourceAppUserModelId;

                var normalizedSource = MusicVisualHelper.NormalizeSource(rawSource);
                var normalizedMetadata = MusicVisualHelper.NormalizeBrowserMetadata(rawTitle, rawArtist, normalizedSource);
                var sanitizedTitle = normalizedMetadata.Title;
                var sanitizedArtist = normalizedMetadata.Artist;

                if (!string.Equals(rawArtist, sanitizedArtist, StringComparison.Ordinal) ||
                    !string.Equals(rawSource, normalizedSource, StringComparison.Ordinal) ||
                    !string.Equals(rawTitle, sanitizedTitle, StringComparison.Ordinal))
                {
                    Logger.Log($"Sanitized browser/media metadata: title='{rawTitle}' -> '{sanitizedTitle}', artist='{rawArtist}' -> '{sanitizedArtist}', source='{rawSource}' -> '{normalizedSource}'");
                }

                var info = new MusicInfo
                {
                    Title = sanitizedTitle,
                    Artist = sanitizedArtist,
                    SourceApp = normalizedSource,
                    RawTitle = rawTitle,
                    RawArtist = rawArtist,
                    RawSourceApp = rawSource,
                    SessionScore = ScoreSession(session, sanitizedTitle, sanitizedArtist, props.Thumbnail != null, BrowserSourceEnabled),
                    IsPlaying = session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    HasMedia = true,
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
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to load album art thumbnail", ex);
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to fetch current SMTC info", ex);
            }
            return lastKnownInfo;
        }

        public MusicInfo GetLastKnownInfo() => lastKnownInfo;

        public string GetDiagnostics()
        {
            var info = lastKnownInfo;
            var sb = new StringBuilder();
            sb.AppendLine("Dynamic Island PC diagnostics");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"Has media: {info?.HasMedia}");
            sb.AppendLine($"Playing: {info?.IsPlaying}");
            sb.AppendLine($"Title: {info?.Title}");
            sb.AppendLine($"Artist: {info?.Artist}");
            sb.AppendLine($"Source: {info?.SourceApp}");
            sb.AppendLine($"Raw title: {info?.RawTitle}");
            sb.AppendLine($"Raw artist: {info?.RawArtist}");
            sb.AppendLine($"Raw source: {info?.RawSourceApp}");
            sb.AppendLine($"Session score: {info?.SessionScore}");
            sb.AppendLine($"Position: {info?.Position}");
            sb.AppendLine($"Duration: {info?.Duration}");
            sb.AppendLine();
            sb.AppendLine(lastDiagnostics);
            return sb.ToString();
        }

        private async Task<GlobalSystemMediaTransportControlsSession> SelectBestSessionAsync()
        {
            var sessions = sessionManager?.GetSessions();
            if (sessions == null || sessions.Count == 0)
            {
                lastDiagnostics = "No SMTC sessions available.";
                return null;
            }

            GlobalSystemMediaTransportControlsSession bestSession = null;
            int bestScore = int.MinValue;
            var diagnostics = new StringBuilder();
            diagnostics.AppendLine("Available SMTC sessions:");

            foreach (var session in sessions)
            {
                try
                {
                    var props = await session.TryGetMediaPropertiesAsync();
                    var source = MusicVisualHelper.NormalizeSource(session.SourceAppUserModelId);
                    if (!BrowserSourceEnabled && MusicVisualHelper.IsBrowserSource(source))
                    {
                        diagnostics.AppendLine($"- {source} | skipped by source rule | rawSource='{session.SourceAppUserModelId}'");
                        continue;
                    }

                    var metadata = MusicVisualHelper.NormalizeBrowserMetadata(props?.Title, props?.Artist, source);
                    var title = metadata.Title;
                    var artist = metadata.Artist;
                    var score = ScoreSession(session, title, artist, props?.Thumbnail != null, BrowserSourceEnabled);
                    diagnostics.AppendLine($"- {MusicVisualHelper.NormalizeSource(session.SourceAppUserModelId)} | score={score} | playing={IsPlaying(session)} | title='{title}' | artist='{artist}' | rawSource='{session.SourceAppUserModelId}'");

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSession = session;
                    }
                }
                catch (Exception ex)
                {
                    diagnostics.AppendLine($"- failed to inspect session '{session.SourceAppUserModelId}': {ex.Message}");
                }
            }

            lastDiagnostics = diagnostics.ToString();
            return bestSession;
        }

        private static int ScoreSession(GlobalSystemMediaTransportControlsSession session, string title, string artist, bool hasThumbnail, bool browserSourceEnabled)
        {
            int score = 0;
            var source = MusicVisualHelper.NormalizeSource(session.SourceAppUserModelId);
            var lower = source.ToLowerInvariant();

            if (IsPlaying(session))
                score += 1000;
            if (!string.IsNullOrWhiteSpace(title))
                score += 250;
            if (!string.IsNullOrWhiteSpace(artist))
                score += 120;
            if (hasThumbnail)
                score += 60;
            if (lower.Contains("spotify") || lower.Contains("yandex") || lower.Contains("vk") || lower.Contains("windows") || lower.Contains("foobar") || lower.Contains("musicbee"))
                score += 90;
            if (MusicVisualHelper.IsBrowserSource(source))
                score -= 20;
            if (!browserSourceEnabled && MusicVisualHelper.IsBrowserSource(source))
                score -= 5000;

            return score;
        }

        private static bool IsPlaying(GlobalSystemMediaTransportControlsSession session)
        {
            return session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
    }
}
