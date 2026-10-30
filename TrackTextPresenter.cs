using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DynamicIslandPC
{
    internal sealed class TrackTextPresenter : IDisposable
    {
        private TextBlock _compactTitle;
        private TextBlock _compactArtist;
        private TextBlock _expandedTitle;
        private TextBlock _expandedArtist;
        private Border _compactSourceBadge;
        private Border _expandedSourceBadge;
        private Ellipse _compactSourceDot;
        private Ellipse _expandedSourceDot;
        private TextBlock _compactSourceText;
        private TextBlock _expandedSourceText;
        private bool _isDarkTheme = true;
        private bool _disposed;

        public TrackTextPresenter(
            TextBlock compactTitle, TextBlock compactArtist,
            TextBlock expandedTitle, TextBlock expandedArtist,
            Border compactSourceBadge, Ellipse compactSourceDot, TextBlock compactSourceText,
            Border expandedSourceBadge, Ellipse expandedSourceDot, TextBlock expandedSourceText)
        {
            _compactTitle = compactTitle;
            _compactArtist = compactArtist;
            _expandedTitle = expandedTitle;
            _expandedArtist = expandedArtist;
            _compactSourceBadge = compactSourceBadge;
            _compactSourceDot = compactSourceDot;
            _compactSourceText = compactSourceText;
            _expandedSourceBadge = expandedSourceBadge;
            _expandedSourceDot = expandedSourceDot;
            _expandedSourceText = expandedSourceText;

        }

        public void SetTrackText(string title, string artist)
        {
            if (_disposed) return;

            var safeTitle = title ?? "Неизвестный трек";
            var safeArtist = artist ?? "Неизвестный исполнитель";
            if (_compactTitle.Text != safeTitle) _compactTitle.Text = safeTitle;
            if (_compactArtist.Text != safeArtist) _compactArtist.Text = safeArtist;
            if (_expandedTitle.Text != safeTitle) _expandedTitle.Text = safeTitle;
            if (_expandedArtist.Text != safeArtist) _expandedArtist.Text = safeArtist;
        }

        public void ApplySourceVisuals(string sourceApp)
        {
            if (_disposed) return;

            var sourceName = MusicVisualHelper.NormalizeSource(sourceApp);
            var badgeLabel = MusicVisualHelper.GetSourceBadgeLabel(sourceName);
            var sourceColor = MusicVisualHelper.GetSourceColor(sourceName);
            SetSourceBadge(_compactSourceBadge, _compactSourceDot, _compactSourceText, badgeLabel, sourceColor);
            SetSourceBadge(_expandedSourceBadge, _expandedSourceDot, _expandedSourceText, sourceName, sourceColor);
        }

        public void SetTheme(bool isDarkTheme)
        {
            if (_disposed) return;
            _isDarkTheme = isDarkTheme;
            var textColor = isDarkTheme ? Brushes.White : Brushes.Black;
            var subTextColor = isDarkTheme
                ? new SolidColorBrush(Color.FromRgb(196, 196, 204))
                : new SolidColorBrush(Color.FromRgb(90, 90, 98));
            _compactTitle.Foreground = textColor;
            _expandedTitle.Foreground = textColor;
            _compactArtist.Foreground = subTextColor;
            _expandedArtist.Foreground = subTextColor;
            _compactSourceText.Foreground = subTextColor;
            _expandedSourceText.Foreground = subTextColor;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _compactTitle = null;
            _compactArtist = null;
            _expandedTitle = null;
            _expandedArtist = null;
            _compactSourceBadge = null;
            _expandedSourceBadge = null;
            _compactSourceDot = null;
            _expandedSourceDot = null;
            _compactSourceText = null;
            _expandedSourceText = null;
        }

        private void SetSourceBadge(Border badge, Ellipse dot, TextBlock textBlock, string label, Color sourceColor)
        {
            var backgroundAlpha = _isDarkTheme ? (byte)36 : (byte)28;
            var borderAlpha = _isDarkTheme ? (byte)64 : (byte)58;
            badge.Background = new SolidColorBrush(Color.FromArgb(backgroundAlpha, sourceColor.R, sourceColor.G, sourceColor.B));
            badge.BorderBrush = new SolidColorBrush(Color.FromArgb(borderAlpha, sourceColor.R, sourceColor.G, sourceColor.B));
            dot.Fill = new SolidColorBrush(sourceColor);
            textBlock.Text = string.IsNullOrWhiteSpace(label) ? "Media" : label;
            textBlock.Foreground = _isDarkTheme
                ? new SolidColorBrush(Color.FromRgb(230, 230, 238))
                : new SolidColorBrush(Color.FromRgb(28, 28, 34));
        }

    }
}
