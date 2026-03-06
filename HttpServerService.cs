using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DynamicIslandPC
{
    public class HttpServerService
    {
        private HttpListener listener;
        private Thread listenerThread;
        private MusicInfo currentTrack = new MusicInfo();
        private DateTime lastUpdateTime = DateTime.MinValue;
        private string coverDebugInfo = "";
        
        public string DebugInfo { get; private set; } = "";

        public void Start()
        {
            var debug = new StringBuilder();
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add("http://localhost:9876/");
                listener.Start();

                debug.AppendLine("   HTTP Server: Запущен на :9876");
                DebugInfo = debug.ToString();

                listenerThread = new Thread(Listen);
                listenerThread.IsBackground = true;
                listenerThread.Start();
            }
            catch (Exception ex)
            {
                debug.AppendLine($"   HTTP Server: Ошибка - {ex.Message}");
                DebugInfo = debug.ToString();
            }
        }

        private void Listen()
        {
            while (listener != null && listener.IsListening)
            {
                try
                {
                    var context = listener.GetContext();
                    var request = context.Request;
                    var response = context.Response;

                    if (request.HttpMethod == "POST" && request.Url.AbsolutePath == "/track")
                    {
                        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            var body = reader.ReadToEnd();
                            var parts = body.Split('|');
                            
                            if (parts.Length >= 2)
                            {
                                currentTrack = new MusicInfo
                                {
                                    Artist = parts[0],
                                    Title = parts[1],
                                    IsPlaying = true
                                };
                                
                                // Иконка сайта (загружаем асинхронно)
                                if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
                                {
                                    var siteUrl = parts[2];
                                    ThreadPool.QueueUserWorkItem(_ => {
                                        var icon = GetSiteIcon(siteUrl);
                                        if (icon != null)
                                        {
                                            currentTrack.AlbumArt = icon;
                                        }
                                    });
                                }
                                
                                lastUpdateTime = DateTime.Now;
                            }
                        }

                        response.StatusCode = 200;
                        response.Close();
                    }
                    else
                    {
                        response.StatusCode = 404;
                        response.Close();
                    }
                }
                catch { }
            }
        }

        private System.Collections.Generic.Dictionary<string, BitmapImage> iconCache = new System.Collections.Generic.Dictionary<string, BitmapImage>();
        private const int MAX_CACHE_SIZE = 20;
        
        private BitmapImage GetSiteIcon(string siteUrl)
        {
            if (iconCache.ContainsKey(siteUrl))
            {
                return iconCache[siteUrl];
            }
            
            try
            {
                var faviconUrl = $"{siteUrl}/favicon.ico";
                var request = (HttpWebRequest)WebRequest.Create(faviconUrl);
                request.Timeout = 2000;
                
                using (var response = request.GetResponse())
                using (var stream = response.GetResponseStream())
                {
                    var image = new BitmapImage();
                    var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);
                    memoryStream.Position = 0;
                    
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = memoryStream;
                    image.EndInit();
                    image.Freeze();
                    
                    if (iconCache.Count >= MAX_CACHE_SIZE)
                    {
                        var firstKey = new System.Collections.Generic.List<string>(iconCache.Keys)[0];
                        iconCache.Remove(firstKey);
                    }
                    
                    iconCache[siteUrl] = image;
                    coverDebugInfo = $"   Иконка: {image.PixelWidth}x{image.PixelHeight}";
                    return image;
                }
            }
            catch
            {
                iconCache[siteUrl] = null;
                return null;
            }
        }

        public MusicInfo GetCurrentTrack()
        {
            if (lastUpdateTime == DateTime.MinValue || (DateTime.Now - lastUpdateTime).TotalSeconds > 5)
            {
                return new MusicInfo();
            }
            
            return currentTrack;
        }

        public void Stop()
        {
            try
            {
                listener?.Stop();
                listener = null;
            }
            catch { }
        }
    }
}
