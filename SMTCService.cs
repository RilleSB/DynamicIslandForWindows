using System;
using System.IO;
using System.Threading;
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
        private string currentSubscribedSourceApp = null;
        private bool initialized = false;
        private string lastDiagnostics = "SMTC is not initialized yet.";
        private string lastLoggedSanitizedKey = "";

        private readonly object stateLock = new();
        private CancellationTokenSource debounceCts;
        private CancellationTokenSource activeFetchCts;
        private bool isFetching = false;
        private bool needsRefetch = false;

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
                    TriggerFetch(0);
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to initialize SMTC session manager", ex);
                }
            });
        }

        private void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            TriggerFetch(50);
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            TriggerFetch(50);
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            TriggerFetch(40);
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            TriggerFetch(40);
        }

        public void TriggerFetch(int debounceMs = 50)
        {
            lock (stateLock)
            {
                needsRefetch = true;
                debounceCts?.Cancel();
                debounceCts?.Dispose();
                debounceCts = new CancellationTokenSource();
                var token = debounceCts.Token;

                Task.Run(async () =>
                {
                    try
                    {
                        if (debounceMs > 0)
                            await Task.Delay(debounceMs, token);

                        if (token.IsCancellationRequested)
                            return;

                        await ProcessFetchQueueAsync();
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Logger.Error("Error during debounced fetch trigger", ex);
                    }
                }, token);
            }
        }

        private async Task ProcessFetchQueueAsync()
        {
            while (true)
            {
                CancellationToken ct;
                lock (stateLock)
                {
                    if (!needsRefetch)
                        return;

                    if (isFetching)
                        return;

                    isFetching = true;
                    needsRefetch = false;

                    activeFetchCts?.Cancel();
                    activeFetchCts?.Dispose();
                    activeFetchCts = new CancellationTokenSource();
                    ct = activeFetchCts.Token;
                }

                try
                {
                    await FetchAndNotifyAsync(ct);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Logger.Error("Error during SMTC FetchAndNotifyAsync", ex);
                }
                finally
                {
                    lock (stateLock)
                    {
                        isFetching = false;
                    }
                }
            }
        }

        private void SubscribeToSession(GlobalSystemMediaTransportControlsSession session)
        {
            var newSource = session?.SourceAppUserModelId;
            if (string.Equals(currentSubscribedSourceApp, newSource, StringComparison.Ordinal) && currentSession != null)
            {
                return;
            }

            UnsubscribeCurrentSession();

            currentSession = session;
            currentSubscribedSourceApp = newSource;

            if (currentSession != null)
            {
                try
                {
                    currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to subscribe to SMTC session events", ex);
                }
            }
        }

        private void UnsubscribeCurrentSession()
        {
            if (currentSession != null)
            {
                try
                {
                    currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                }
                catch { }
                currentSession = null;
            }
            currentSubscribedSourceApp = null;
        }

        private async Task FetchAndNotifyAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var (session, props) = await SelectBestSessionAsync(ct);
            ct.ThrowIfCancellationRequested();

            if (session == null)
            {
                UnsubscribeCurrentSession();
                var emptyInfo = new MusicInfo
                {
                    SourceApp = "Media",
                    HasMedia = false,
                    IsPlaying = false,
                    Position = TimeSpan.Zero,
                    Duration = TimeSpan.Zero
                };
                lastKnownInfo = emptyInfo;
                MusicInfoChanged?.Invoke(emptyInfo);
                return;
            }

            SubscribeToSession(session);

            if (props == null || string.IsNullOrWhiteSpace(props.Title))
            {
                var noTrackInfo = new MusicInfo
                {
                    SourceApp = MusicVisualHelper.NormalizeSource(session.SourceAppUserModelId),
                    HasMedia = false,
                    IsPlaying = false,
                    Position = TimeSpan.Zero,
                    Duration = TimeSpan.Zero
                };
                lastKnownInfo = noTrackInfo;
                MusicInfoChanged?.Invoke(noTrackInfo);
                return;
            }

            GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = null;
            try { timeline = session.GetTimelineProperties(); } catch { }

            var rawTitle = props.Title;
            var rawArtist = props.Artist;
            var rawSource = session.SourceAppUserModelId;

            var normalizedSource = MusicVisualHelper.NormalizeSource(rawSource);
            var normalizedMetadata = MusicVisualHelper.NormalizeBrowserMetadata(rawTitle, rawArtist, normalizedSource);
            var sanitizedTitle = normalizedMetadata.Title;
            var sanitizedArtist = normalizedMetadata.Artist;

            var sanitizedKey = $"{rawTitle}|{rawArtist}|{rawSource}";
            if (!string.Equals(lastLoggedSanitizedKey, sanitizedKey, StringComparison.Ordinal))
            {
                lastLoggedSanitizedKey = sanitizedKey;
                if (!string.Equals(rawArtist, sanitizedArtist, StringComparison.Ordinal) ||
                    !string.Equals(rawSource, normalizedSource, StringComparison.Ordinal) ||
                    !string.Equals(rawTitle, sanitizedTitle, StringComparison.Ordinal))
                {
                    Logger.Log($"Sanitized browser/media metadata: title='{rawTitle}' -> '{sanitizedTitle}', artist='{rawArtist}' -> '{sanitizedArtist}', source='{rawSource}' -> '{normalizedSource}'");
                }
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
                IsPlaying = IsPlaying(session),
                HasMedia = true,
                Position = timeline?.Position ?? TimeSpan.Zero,
                Duration = timeline?.EndTime ?? TimeSpan.Zero
            };

            if (props.Thumbnail != null)
            {
                if (lastKnownInfo?.AlbumArt != null &&
                    string.Equals(lastKnownInfo.RawTitle, rawTitle, StringComparison.Ordinal) &&
                    string.Equals(lastKnownInfo.RawArtist, rawArtist, StringComparison.Ordinal) &&
                    string.Equals(lastKnownInfo.SourceApp, normalizedSource, StringComparison.Ordinal))
                {
                    info.AlbumArt = lastKnownInfo.AlbumArt;
                }
                else
                {
                    try
                    {
                        using var winrtStream = await props.Thumbnail.OpenReadAsync();
                        ct.ThrowIfCancellationRequested();
                        if (winrtStream != null && winrtStream.Size > 0)
                        {
                            using var netStream = winrtStream.AsStreamForRead();
                            using var memStream = new MemoryStream();
                            await netStream.CopyToAsync(memStream, ct);
                            memStream.Position = 0;

                            var image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.StreamSource = memStream;
                            image.EndInit();
                            image.Freeze();
                            info.AlbumArt = image;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Failed to load album art thumbnail", ex);
                    }
                }
            }

            ct.ThrowIfCancellationRequested();
            lastKnownInfo = info;
            MusicInfoChanged?.Invoke(info);
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

        private async Task<(GlobalSystemMediaTransportControlsSession session, GlobalSystemMediaTransportControlsSessionMediaProperties props)> SelectBestSessionAsync(CancellationToken ct)
        {
            var sessions = sessionManager?.GetSessions();
            if (sessions == null || sessions.Count == 0)
            {
                lastDiagnostics = "No SMTC sessions available.";
                return (null, null);
            }

            GlobalSystemMediaTransportControlsSession bestSession = null;
            GlobalSystemMediaTransportControlsSessionMediaProperties bestProps = null;
            int bestScore = int.MinValue;
            var diagnostics = new StringBuilder();
            diagnostics.AppendLine("Available SMTC sessions:");

            foreach (var session in sessions)
            {
                ct.ThrowIfCancellationRequested();
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
                        bestProps = props;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    diagnostics.AppendLine($"- failed to inspect session '{session.SourceAppUserModelId}': {ex.Message}");
                }
            }

            lastDiagnostics = diagnostics.ToString();
            return (bestSession, bestProps);
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
            try
            {
                return session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            }
            catch
            {
                return false;
            }
        }
    }
}
