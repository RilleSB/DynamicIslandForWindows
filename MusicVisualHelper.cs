using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIslandPC
{
    internal static class MusicVisualHelper
    {
        public static Color GetAccentColor(BitmapSource bitmapSource)
        {
            if (bitmapSource == null)
                return Color.FromRgb(88, 88, 96);

            try
            {
                // Sample a downscaled version to keep the accent subtle and stable.
                var scaled = new TransformedBitmap(bitmapSource, new ScaleTransform(
                    Math.Min(1.0, 24.0 / bitmapSource.PixelWidth),
                    Math.Min(1.0, 24.0 / bitmapSource.PixelHeight)));

                int stride = scaled.PixelWidth * 4;
                var pixels = new byte[scaled.PixelHeight * stride];
                scaled.CopyPixels(pixels, stride, 0);

                double totalWeight = 0;
                double red = 0;
                double green = 0;
                double blue = 0;

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];

                    double max = Math.Max(r, Math.Max(g, b));
                    double min = Math.Min(r, Math.Min(g, b));
                    double saturation = max == 0 ? 0 : (max - min) / max;
                    double brightness = (r + g + b) / (255.0 * 3.0);
                    double weight = 0.35 + saturation + brightness * 0.35;

                    totalWeight += weight;
                    red += r * weight;
                    green += g * weight;
                    blue += b * weight;
                }

                if (totalWeight <= 0.001)
                    return Color.FromRgb(88, 88, 96);

                byte avgR = (byte)Math.Clamp(red / totalWeight, 0, 255);
                byte avgG = (byte)Math.Clamp(green / totalWeight, 0, 255);
                byte avgB = (byte)Math.Clamp(blue / totalWeight, 0, 255);

                return BlendToward(avgR, avgG, avgB, 0.55);
            }
            catch
            {
                return Color.FromRgb(88, 88, 96);
            }
        }

        public static string NormalizeSource(string sourceAppId)
        {
            if (string.IsNullOrWhiteSpace(sourceAppId))
                return "Media";

            var source = sourceAppId.ToLowerInvariant();
            if (source.Contains("spotify"))
                return "Spotify";
            if (source.Contains("yandexmusic") || source.Contains("yandex"))
                return "Yandex Music";
            if (source.Contains("vkmusic") || source.Contains("vk.music") || source.Contains("vk"))
                return "VK Music";
            if (source.Contains("youtube"))
                return "YouTube";
            if (source.Contains("wmplayer") || source.Contains("media.player"))
                return "Windows Media";
            if (source.Contains("musicbee"))
                return "MusicBee";
            if (source.Contains("foobar"))
                return "foobar2000";
            if (source.Contains("vlc"))
                return "VLC";
            if (source.Contains("chrome"))
                return "Chrome";
            if (source.Contains("msedge"))
                return "Edge";

            return sourceAppId;
        }

        public static string GetSourceBadgeLabel(string sourceApp)
        {
            var source = NormalizeSource(sourceApp);
            var lower = source.ToLowerInvariant();

            if (lower.Contains("spotify"))
                return "Spotify";
            if (lower.Contains("yandex"))
                return "Yandex";
            if (lower.Contains("vk"))
                return "VK";
            if (lower.Contains("youtube"))
                return "YouTube";
            if (lower.Contains("windows"))
                return "WMP";
            if (lower.Contains("foobar"))
                return "foobar";
            if (lower.Contains("musicbee"))
                return "MusicBee";

            return source.Length > 12 ? source.Substring(0, 12).Trim() : source;
        }

        public static Color GetSourceColor(string sourceApp)
        {
            var source = NormalizeSource(sourceApp).ToLowerInvariant();

            if (source.Contains("spotify"))
                return Color.FromRgb(30, 215, 96);
            if (source.Contains("yandex"))
                return Color.FromRgb(255, 204, 0);
            if (source.Contains("vk"))
                return Color.FromRgb(0, 119, 255);
            if (source.Contains("youtube"))
                return Color.FromRgb(255, 59, 48);
            if (source.Contains("windows") || source.Contains("edge"))
                return Color.FromRgb(76, 194, 255);
            if (source.Contains("chrome"))
                return Color.FromRgb(251, 188, 5);
            if (source.Contains("vlc"))
                return Color.FromRgb(255, 140, 26);
            if (source.Contains("musicbee"))
                return Color.FromRgb(253, 187, 45);
            if (source.Contains("foobar"))
                return Color.FromRgb(207, 207, 216);

            return Color.FromRgb(138, 138, 150);
        }

        private static Color BlendToward(byte r, byte g, byte b, double amount)
        {
            byte baseR = 26;
            byte baseG = 26;
            byte baseB = 31;

            return Color.FromRgb(
                (byte)(baseR + (r - baseR) * amount),
                (byte)(baseG + (g - baseG) * amount),
                (byte)(baseB + (b - baseB) * amount));
        }
    }
}
