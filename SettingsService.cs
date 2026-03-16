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
        public bool IsDarkTheme { get; set; } = true;
        public double Scale { get; set; } = 1.0;
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
            catch { }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}
