using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
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
        private string lastThumbnailRetryKey = "";
        private System.Threading.Timer watchdogTimer;

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
                    if (sessionManager != null)
                    {
                        sessionManager.CurrentSessionChanged += OnSessionChanged;
                        sessionManager.SessionsChanged += OnSessionsChanged;
                    }
                    TriggerFetch(0);
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to initialize SMTC session manager", ex);
                }
                finally
                {
                    StartWatchdogTimer();
                }
            });
        }

        private void StartWatchdogTimer()
        {
            if (watchdogTimer != null) return;
            watchdogTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (sessionManager == null)
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                                if (sessionManager != null)
                                {
                                    sessionManager.CurrentSessionChanged += OnSessionChanged;
                                    sessionManager.SessionsChanged += OnSessionsChanged;
                                    TriggerFetch(0);
                                }
                            }
                            catch { }
                        });
                        return;
                    }

                    // Watchdog heartbeat: ensure we never miss dropped WinRT events
                    TriggerFetch(0);
                }
                catch { }
            }, null, 1500, 1500);
        }

        private void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            TriggerFetch(40);
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            TriggerFetch(40);
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            TriggerFetch(30);
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            TriggerFetch(30);
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
            if (session == null)
            {
                UnsubscribeCurrentSession();
                return;
            }

            if (object.ReferenceEquals(currentSession, session))
            {
                return;
            }

            UnsubscribeCurrentSession();

            currentSession = session;
            currentSubscribedSourceApp = session.SourceAppUserModelId;

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

            if (session == null && lastKnownInfo?.HasMedia == true)
            {
                // Brief transition retry loop before dropping to empty state
                for (int i = 0; i < 3; i++)
                {
                    await Task.Delay(100, ct);
                    (session, props) = await SelectBestSessionAsync(ct);
                    if (session != null)
                        break;
                }
            }

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

            try
            {
                var liveProps = await session.TryGetMediaPropertiesAsync();
                if (liveProps != null && (!string.IsNullOrWhiteSpace(liveProps.Title) || liveProps.Thumbnail != null))
                {
                    props = liveProps;
                }
            }
            catch { }

            // If title is temporarily missing during track transition, retry briefly
            if (props == null || string.IsNullOrWhiteSpace(props.Title))
            {
                for (int i = 0; i < 4; i++)
                {
                    await Task.Delay(80, ct);
                    try
                    {
                        var refreshed = await session.TryGetMediaPropertiesAsync();
                        if (refreshed != null && !string.IsNullOrWhiteSpace(refreshed.Title))
                        {
                            props = refreshed;
                            break;
                        }
                    }
                    catch { }
                }
            }

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

            bool isCurrentSystem = false;
            try
            {
                isCurrentSystem = object.ReferenceEquals(session, sessionManager?.GetCurrentSession());
            }
            catch { }

            var info = new MusicInfo
            {
                Title = sanitizedTitle,
                Artist = sanitizedArtist,
                SourceApp = normalizedSource,
                RawTitle = rawTitle,
                RawArtist = rawArtist,
                RawSourceApp = rawSource,
                SessionScore = ScoreSession(session, sanitizedTitle, sanitizedArtist, props.Thumbnail != null, BrowserSourceEnabled, isCurrentSystem),
                IsPlaying = IsPlaying(session),
                HasMedia = true,
                Position = timeline?.Position ?? TimeSpan.Zero,
                Duration = timeline?.EndTime ?? TimeSpan.Zero
            };

            if (props.Thumbnail == null)
            {
                // Quick retry loop if thumbnail wasn't ready yet at the moment of track switch
                for (int i = 0; i < 6; i++)
                {
                    try
                    {
                        await Task.Delay(75, ct);
                        var refreshed = await session.TryGetMediaPropertiesAsync();
                        if (refreshed?.Thumbnail != null)
                        {
                            props = refreshed;
                            break;
                        }
                    }
                    catch { }
                }
            }

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
                    // Attempt to decode thumbnail with retries in case file stream is locked by player
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        try
                        {
                            using var winrtStream = await props.Thumbnail.OpenReadAsync();
                            if (winrtStream != null && winrtStream.Size > 0)
                            {
                                byte[] bytes;
                                using (var netStream = winrtStream.AsStreamForRead())
                                using (var ms = new MemoryStream())
                                {
                                    await netStream.CopyToAsync(ms);
                                    bytes = ms.ToArray();
                                }

                                if (bytes.Length > 0)
                                {
                                    var image = new BitmapImage();
                                    using (var memStream = new MemoryStream(bytes))
                                    {
                                        image.BeginInit();
                                        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                                        image.CacheOption = BitmapCacheOption.OnLoad;
                                        image.StreamSource = memStream;
                                        image.EndInit();
                                    }
                                    image.Freeze();
                                    info.AlbumArt = image;
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (attempt == 2)
                            {
                                Logger.Error($"Failed to load album art thumbnail for '{rawTitle}'", ex);
                            }
                            else
                            {
                                await Task.Delay(100);
                            }
                        }
                    }
                }
            }
            else if (lastKnownInfo?.AlbumArt != null &&
                     string.Equals(lastKnownInfo.RawTitle, rawTitle, StringComparison.Ordinal) &&
                     string.Equals(lastKnownInfo.RawArtist, rawArtist, StringComparison.Ordinal))
            {
                // Preserve existing album art if thumbnail is temporarily null on the same track
                info.AlbumArt = lastKnownInfo.AlbumArt;
            }

            // If thumbnail is still null for an active track, schedule delayed retries
            if (info.AlbumArt == null && !string.IsNullOrWhiteSpace(info.Title) && session != null)
            {
                var trackKey = $"{normalizedSource}|{sanitizedTitle}|{sanitizedArtist}";
                if (!string.Equals(lastThumbnailRetryKey, trackKey, StringComparison.Ordinal))
                {
                    lastThumbnailRetryKey = trackKey;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(400);
                            TriggerFetch(0);
                            await Task.Delay(800);
                            TriggerFetch(0);
                        }
                        catch { }
                    });
                }
            }
            else if (info.AlbumArt != null)
            {
                lastThumbnailRetryKey = "";
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
            if (sessionManager == null)
            {
                lastDiagnostics = "No SMTC session manager available.";
                return (null, null);
            }

            var candidateSessions = new List<GlobalSystemMediaTransportControlsSession>();
            GlobalSystemMediaTransportControlsSession currentSystemSession = null;

            try
            {
                currentSystemSession = sessionManager.GetCurrentSession();
                if (currentSystemSession != null)
                {
                    candidateSessions.Add(currentSystemSession);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting CurrentSession", ex);
            }

            try
            {
                var allSessions = sessionManager.GetSessions();
                if (allSessions != null)
                {
                    foreach (var s in allSessions)
                    {
                        if (s != null && !candidateSessions.Any(existing => object.ReferenceEquals(existing, s)))
                        {
                            candidateSessions.Add(s);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting all sessions", ex);
            }

            if (candidateSessions.Count == 0)
            {
                lastDiagnostics = "No SMTC sessions available.";
                return (null, null);
            }

            GlobalSystemMediaTransportControlsSession bestSession = null;
            GlobalSystemMediaTransportControlsSessionMediaProperties bestProps = null;
            int bestScore = int.MinValue;
            var diagnostics = new StringBuilder();
            diagnostics.AppendLine("Available SMTC sessions:");

            foreach (var session in candidateSessions)
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
                    bool isCur = object.ReferenceEquals(session, currentSystemSession);
                    var score = ScoreSession(session, title, artist, props?.Thumbnail != null, BrowserSourceEnabled, isCur);
                    diagnostics.AppendLine($"- {MusicVisualHelper.NormalizeSource(session.SourceAppUserModelId)} | score={score} | playing={IsPlaying(session)} | isCurrent={isCur} | title='{title}' | artist='{artist}' | rawSource='{session.SourceAppUserModelId}'");

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

        private static int ScoreSession(GlobalSystemMediaTransportControlsSession session, string title, string artist, bool hasThumbnail, bool browserSourceEnabled, bool isCurrentSession)
        {
            int score = 0;
            var source = MusicVisualHelper.NormalizeSource(session.SourceAppUserModelId);
            var lower = source.ToLowerInvariant();

            if (isCurrentSession)
                score += 300;
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

        public async Task<bool> SeekToPositionAsync(TimeSpan position)
        {
            try
            {
                var session = currentSession ?? sessionManager?.GetCurrentSession();
                if (session != null)
                {
                    long ticks = position.Ticks;
                    bool result = await session.TryChangePlaybackPositionAsync(ticks);
                    if (result && lastKnownInfo != null)
                    {
                        lastKnownInfo.Position = position;
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to seek playback position", ex);
            }
            return false;
        }

        public async Task<bool> SeekToRatioAsync(double ratio)
        {
            if (lastKnownInfo != null && lastKnownInfo.Duration > TimeSpan.Zero)
            {
                ratio = Math.Clamp(ratio, 0.0, 1.0);
                var target = TimeSpan.FromTicks((long)(lastKnownInfo.Duration.Ticks * ratio));
                return await SeekToPositionAsync(target);
            }
            return false;
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
