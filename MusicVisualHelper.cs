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
