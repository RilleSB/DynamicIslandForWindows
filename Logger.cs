using System;
using System.IO;

namespace DynamicIslandPC
{
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIslandPC",
            "app.log"
        );

        static Logger()
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch { }
        }

        public static void Log(string message)
        {
            try
            {
                var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                File.AppendAllText(LogPath, logMessage + Environment.NewLine);
            }
            catch { }
        }

        public static void Error(string message, Exception ex = null)
        {
            var errorMessage = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
            Log($"ERROR: {errorMessage}");
        }

        public static void OpenLogFile()
        {
            try
            {
                if (File.Exists(LogPath))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LogPath) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
