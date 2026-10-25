using System;
using System.IO;
using System.Text.Json;

namespace DynamicIslandPC
{
    public class AppSettings
    {
        public bool HasCustomPosition { get; set; } = false;
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
        public bool VinylRotationEnabled { get; set; } = true;
        public bool AudioReactiveBreathingEnabled { get; set; } = true;
        public bool FluidPhysicsEnabled { get; set; } = false;
        public bool VinylScratchEnabled { get; set; } = true;
        public int VisualizerSkin { get; set; } = 0;
    }

    public static class SettingsService
    {
        private static readonly string _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIslandPC", "settings.json");

        public static AppSettings Load()
        {
            AppSettings settings = null;
            try
            {
                if (File.Exists(_path))
                    settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path));
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
                        settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(backup));
                    }
                }
                catch (Exception backupEx)
                {
                    Logger.Error("Failed to load settings backup", backupEx);
                }
            }

            settings ??= new AppSettings();
            if (double.IsNaN(settings.Scale) || settings.Scale < 0.5 || settings.Scale > 3.0)
                settings.Scale = 1.0;

            return settings;
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
