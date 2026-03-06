using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicIslandPC
{
    public class WindowsMediaService
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private HttpServerService httpServer;
        private SMTCService smtcService;
        
        public WindowsMediaService(HttpServerService httpServerInstance = null)
        {
            httpServer = httpServerInstance;
            smtcService = new SMTCService();
        }

        public MusicInfo GetCurrentPlayingMusic()
        {
            var musicInfo = new MusicInfo();
            debugInfo = new System.Text.StringBuilder();
            debugInfo.AppendLine($"=== Поиск музыки {DateTime.Now:HH:mm:ss} ===");

            try
            {
                // Пробуем HTTP сервер (расширение)
                if (httpServer != null)
                {
                    var httpInfo = httpServer.GetCurrentTrack();
                    if (!string.IsNullOrEmpty(httpInfo.Title))
                    {
                        debugInfo.AppendLine($"✓ HTTP: {httpInfo.Artist} - {httpInfo.Title}");
                        DebugInfo = debugInfo.ToString();
                        return httpInfo;
                    }
                }
                
                // Пробуем SMTC (Spotify, iTunes, AIMP и др.)
                var smtcInfo = smtcService.GetMediaFromSMTC();
                if (!string.IsNullOrEmpty(smtcInfo.Title))
                {
                    debugInfo.AppendLine($"✓ SMTC: {smtcInfo.Artist} - {smtcInfo.Title}");
                    DebugInfo = debugInfo.ToString();
                    return smtcInfo;
                }
                
                debugInfo.AppendLine("✗ Медиа не найдено");
            }
            catch (Exception ex)
            {
                debugInfo.AppendLine($"✗ Ошибка: {ex.Message}");
            }

            DebugInfo = debugInfo.ToString();
            return musicInfo;
        }

        public string DebugInfo { get; private set; } = "";
        private System.Text.StringBuilder debugInfo;
    }
}
