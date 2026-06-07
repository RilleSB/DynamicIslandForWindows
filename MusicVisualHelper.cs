using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicIslandPC
{
    internal sealed class AlbumColorPalette
    {
        public Color Primary { get; init; }
        public Color Secondary { get; init; }
        public Color Tertiary { get; init; }
    }

    internal static class MusicVisualHelper
    {
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex HexIdRegex = new(@"^[A-F0-9]{8,}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex OpaqueIdRegex = new(@"^[A-Z0-9_-]{8,}$", RegexOptions.Compiled);
        private static readonly Regex TransportNoiseRegex = new(@"\b\d+\s*(kb/s|mb/s|gb/s|fps|hz)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly AlbumColorPalette DefaultPalette = new()
        {
            Primary = Color.FromRgb(88, 88, 96),
            Secondary = Color.FromRgb(58, 58, 66),
            Tertiary = Color.FromRgb(36, 36, 44)
        };

        public static Color GetAccentColor(BitmapSource bitmapSource)
        {
            return GetAlbumPalette(bitmapSource).Primary;
        }

        public static AlbumColorPalette GetAlbumPalette(BitmapSource bitmapSource)
        {
            if (bitmapSource == null)
                return DefaultPalette;

            try
            {
                // Sample a downscaled version to keep palette extraction cheap.
                var scaled = new TransformedBitmap(bitmapSource, new ScaleTransform(
                    Math.Min(1.0, 32.0 / bitmapSource.PixelWidth),
                    Math.Min(1.0, 32.0 / bitmapSource.PixelHeight)));

                int stride = scaled.PixelWidth * 4;
                var pixels = new byte[scaled.PixelHeight * stride];
                scaled.CopyPixels(pixels, stride, 0);

                var buckets = new ColorBucket[16];
                for (int i = 0; i < buckets.Length; i++)
                    buckets[i] = new ColorBucket();

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];

                    double max = Math.Max(r, Math.Max(g, b));
                    double min = Math.Min(r, Math.Min(g, b));
                    double saturation = max == 0 ? 0 : (max - min) / max;
                    double brightness = max / 255.0;

                    if (brightness < 0.08 || brightness > 0.96)
                        continue;

                    var hue = GetHue(r, g, b);
                    var bucketIndex = (int)Math.Clamp(Math.Floor(hue / 360.0 * buckets.Length), 0, buckets.Length - 1);
                    var weight = 0.25 + saturation * 1.45 + brightness * 0.35;
                    buckets[bucketIndex].Add(r, g, b, weight);
                }

                var colors = new Color[3];
                int count = 0;

                foreach (var bucket in buckets.OrderByDescending(b => b.Score))
                {
                    if (bucket.Score <= 0.001)
                        continue;

                    var candidate = bucket.ToColor();
                    if (count > 0 && !IsDistinct(candidate, colors, count))
                        continue;

                    colors[count++] = SoftenForUi(candidate);
                    if (count == colors.Length)
                        break;
                }

                if (count == 0)
                    return DefaultPalette;
                if (count == 1)
                {
                    colors[1] = Mix(colors[0], Color.FromRgb(42, 42, 50), 0.55);
                    colors[2] = Mix(colors[0], Color.FromRgb(20, 20, 26), 0.75);
                }
                else if (count == 2)
                {
                    colors[2] = Mix(colors[0], colors[1], 0.5);
                }

                return new AlbumColorPalette
                {
                    Primary = colors[0],
                    Secondary = colors[1],
                    Tertiary = colors[2]
                };
            }
            catch
            {
                return DefaultPalette;
            }
        }

        public static string NormalizeSource(string sourceAppId)
        {
            if (string.IsNullOrWhiteSpace(sourceAppId))
                return "Media";

            var normalized = NormalizeWhitespace(sourceAppId);
            var source = normalized.ToLowerInvariant();
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
            if (source.Contains("firefox"))
                return "Firefox";
            if (source.Contains("opera"))
                return "Opera";
            if (source.Contains("browser"))
                return "Browser";
            if (LooksLikeOpaqueId(normalized))
                return "Browser";

            return normalized;
        }

        public static string SanitizeTitle(string title)
        {
            var sanitized = NormalizeWhitespace(title);
            if (string.IsNullOrWhiteSpace(sanitized))
                return string.Empty;

            return sanitized;
        }

        public static string SanitizeArtist(string artist)
        {
            var sanitized = NormalizeWhitespace(artist);
            if (string.IsNullOrWhiteSpace(sanitized))
                return string.Empty;
            if (LooksLikeGarbageMetadata(sanitized))
                return string.Empty;

            return sanitized;
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

        private static Color SoftenForUi(Color color)
        {
            return BlendToward(color.R, color.G, color.B, 0.58);
        }

        private static Color Mix(Color first, Color second, double secondAmount)
        {
            return Color.FromRgb(
                (byte)(first.R + (second.R - first.R) * secondAmount),
                (byte)(first.G + (second.G - first.G) * secondAmount),
                (byte)(first.B + (second.B - first.B) * secondAmount));
        }

        private static bool IsDistinct(Color candidate, Color[] selected, int selectedCount)
        {
            for (int i = 0; i < selectedCount; i++)
            {
                var existing = selected[i];
                var distance = Math.Abs(candidate.R - existing.R)
                    + Math.Abs(candidate.G - existing.G)
                    + Math.Abs(candidate.B - existing.B);
                if (distance < 72)
                    return false;
            }

            return true;
        }

        private static double GetHue(byte r, byte g, byte b)
        {
            double red = r / 255.0;
            double green = g / 255.0;
            double blue = b / 255.0;
            double max = Math.Max(red, Math.Max(green, blue));
            double min = Math.Min(red, Math.Min(green, blue));
            double delta = max - min;

            if (delta <= 0.0001)
                return 0;

            double hue;
            if (Math.Abs(max - red) < 0.0001)
                hue = 60 * (((green - blue) / delta) % 6);
            else if (Math.Abs(max - green) < 0.0001)
                hue = 60 * (((blue - red) / delta) + 2);
            else
                hue = 60 * (((red - green) / delta) + 4);

            return hue < 0 ? hue + 360 : hue;
        }

        private static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return WhitespaceRegex.Replace(value.Trim(), " ");
        }

        private static bool LooksLikeGarbageMetadata(string value)
        {
            if (TransportNoiseRegex.IsMatch(value))
                return true;
            if (LooksLikeOpaqueId(value))
                return true;

            return false;
        }

        private static bool LooksLikeOpaqueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var candidate = value.Trim();
            if (candidate.Contains(' '))
                return false;
            if (HexIdRegex.IsMatch(candidate))
                return true;
            if (!OpaqueIdRegex.IsMatch(candidate))
                return false;

            var digitCount = candidate.Count(char.IsDigit);
            var upperCount = candidate.Count(char.IsUpper);
            return digitCount >= 3 && upperCount >= 3;
        }

        private sealed class ColorBucket
        {
            private double red;
            private double green;
            private double blue;
            private double weight;

            public double Score => weight;

            public void Add(byte r, byte g, byte b, double pixelWeight)
            {
                red += r * pixelWeight;
                green += g * pixelWeight;
                blue += b * pixelWeight;
                weight += pixelWeight;
            }

            public Color ToColor()
            {
                if (weight <= 0.001)
                    return Color.FromRgb(88, 88, 96);

                return Color.FromRgb(
                    (byte)Math.Clamp(red / weight, 0, 255),
                    (byte)Math.Clamp(green / weight, 0, 255),
                    (byte)Math.Clamp(blue / weight, 0, 255));
            }
        }
    }
}
