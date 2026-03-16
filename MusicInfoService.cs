using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace DynamicIslandPC
{
    public class MusicInfo
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public BitmapImage AlbumArt { get; set; }
        public bool IsPlaying { get; set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class MusicInfoService
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private SMTCService smtcService;

        public event Action<MusicInfo> MusicInfoChanged;
        
        public MusicInfoService()
        {
            smtcService = new SMTCService();
            smtcService.MusicInfoChanged += info =>
            {
                if (info.AlbumArt == null)
                    info.AlbumArt = CreateDefaultAlbumArt();
                MusicInfoChanged?.Invoke(info);
            };
            smtcService.Initialize();
        }

        public MusicInfo GetCurrentMusicInfo()
        {
            var info = smtcService.GetLastKnownInfo() ?? new MusicInfo();
            
            if (string.IsNullOrEmpty(info.Title))
            {
                info.Title = "Нет воспроизведения";
                info.Artist = "Запустите музыкальный плеер";
                info.IsPlaying = false;
            }
            
            if (info.AlbumArt == null)
                info.AlbumArt = CreateDefaultAlbumArt();
            
            return info;
        }



        private BitmapImage CreateDefaultAlbumArt()
        {
            try
            {
                // Создаем простую дефолтную обложку
                var bitmap = new Bitmap(100, 100);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.FillRectangle(Brushes.DarkGray, 0, 0, 100, 100);
                    g.FillEllipse(Brushes.Gray, 25, 25, 50, 50);
                    g.FillEllipse(Brushes.DarkGray, 40, 40, 20, 20);
                }
                
                var bitmapImage = new BitmapImage();
                using (var memory = new MemoryStream())
                {
                    bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                    memory.Position = 0;
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                }
                
                return bitmapImage;
            }
            catch
            {
                return null;
            }
        }

        public void TogglePlayPause()
        {
            SendMediaKey(VK_MEDIA_PLAY_PAUSE);
        }

        public void NextTrack()
        {
            SendMediaKey(VK_MEDIA_NEXT_TRACK);
        }

        public void PreviousTrack()
        {
            SendMediaKey(VK_MEDIA_PREV_TRACK);
        }

        private void SendMediaKey(byte key)
        {
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}