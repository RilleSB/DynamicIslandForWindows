using System;
using System.IO;

namespace DynamicIslandPC
{
    public class AIMPService
    {
        private string nowPlayingPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AIMP",
            "nowplaying.txt"
        );
        private DateTime lastFileCheck = DateTime.MinValue;
        private MusicInfo lastInfo = null;

        public MusicInfo GetMediaFromAIMP()
        {
            var musicInfo = new MusicInfo();

            try
            {
                if (!File.Exists(nowPlayingPath))
                    return musicInfo;

                var lastWrite = File.GetLastWriteTime(nowPlayingPath);
                
                // Если файл не обновлялся больше 5 секунд - трек не играет
                if ((DateTime.Now - lastWrite).TotalSeconds > 5)
                    return musicInfo;

                // Читаем файл
                var content = File.ReadAllText(nowPlayingPath);
                if (string.IsNullOrWhiteSpace(content))
                    return musicInfo;

                // Парсим формат: "Исполнитель - Название" или построчно
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length >= 2)
                {
                    // Формат: строка 1 = исполнитель, строка 2 = название
                    musicInfo.Artist = lines[0].Trim();
                    musicInfo.Title = lines[1].Trim();
                    musicInfo.IsPlaying = true;
                }
                else if (lines.Length == 1 && lines[0].Contains(" - "))
                {
                    // Формат: "Исполнитель - Название"
                    var parts = lines[0].Split(new[] { " - " }, 2, StringSplitOptions.None);
                    musicInfo.Artist = parts[0].Trim();
                    musicInfo.Title = parts[1].Trim();
                    musicInfo.IsPlaying = true;
                }
            }
            catch { }

            return musicInfo;
        }
    }
}
