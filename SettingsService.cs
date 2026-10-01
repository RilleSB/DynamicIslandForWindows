using System;
using System.IO;
using System.Text.Json;

namespace DynamicIslandPC
{
    public class AppSettings
    {
        public double CustomX { get; set; } = -1;
        public double CustomY { get; set; } = -1;
        public bool IsTopPosition { get; set; } = true;
        public int DisplayMode { get; set; } = 0;
        public bool IsDarkTheme { get; set; } = true;
        public double Scale { get; set; } = 1.0;
        public string BackgroundColor { get; set; } = "#FF000000";
        public double BackgroundOpacity { get; set; } = 0.7;
        public bool AdaptiveAlbumThemeEnabled { get; set; } = true;
        public bool DecorationEnabled { get; set; } = true;
        public string DecorationMediaPath { get; set; } = "";
        public bool GamingModeEnabled { get; set; } = false;
        public bool LockModeEnabled { get; set; } = false;
        public bool StartWithWindowsEnabled { get; set; } = false;
        public bool BrowserSourceEnabled { get; set; } = true;
        public bool LiquidGlassEnabled { get; set; } = false;
        public string TargetMonitorDeviceName { get; set; } = "";
        public bool ExcludeFromCapture { get; set; } = false;
    }

    public static class SettingsService
    {
        private static readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIslandPC", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(_path))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load settings", ex);
                var backup = _path + ".bak";
                try
                {
                    if (File.Exists(backup))
                    {
                        Logger.Log("Restoring settings from backup");
                        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(backup)) ?? new AppSettings();
                    }
                }
                catch (Exception backupEx)
                {
                    Logger.Error("Failed to load settings backup", backupEx);
                }
            }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                var tempPath = _path + ".tmp";
                var backupPath = _path + ".bak";

                File.WriteAllText(tempPath, json);
                if (File.Exists(_path))
                    File.Copy(_path, backupPath, overwrite: true);
                File.Copy(tempPath, _path, overwrite: true);
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to save settings", ex);
            }
        }
    }
}
