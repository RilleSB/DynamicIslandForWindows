using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DynamicIslandPC
{
    internal sealed class VisualizerRenderer
    {
        private readonly Grid _minimalMode;
        private readonly Grid _compactMode;
        private readonly Grid _expandedMode;
        private readonly StackPanel _minimalIndicator;
        private readonly StackPanel _compactIndicator;
        private readonly StackPanel _expandedIndicator;
        private readonly Ellipse _minimalHalo;
        private readonly Ellipse _compactHalo;
        private readonly Ellipse _expandedHalo;
        private readonly ScaleTransform _islandScale;
        private readonly ScaleTransform[] _artScales;
        private readonly ScaleTransform[] _gifScales;
        private double _breathingScale = 1.0;
        private double _artBreathingScale = 1.0;

        public VisualizerRenderer(
            Grid minimalMode, Grid compactMode, Grid expandedMode,
            StackPanel minimalIndicator, StackPanel compactIndicator, StackPanel expandedIndicator,
            Ellipse minimalHalo, Ellipse compactHalo, Ellipse expandedHalo,
            ScaleTransform islandScale,
            ScaleTransform minimalArtScale, ScaleTransform artScale, ScaleTransform expandedArtScale,
            ScaleTransform minimalGifScale, ScaleTransform compactGifScale, ScaleTransform expandedGifScale)
        {
            _minimalMode = minimalMode;
            _compactMode = compactMode;
            _expandedMode = expandedMode;
            _minimalIndicator = minimalIndicator;
            _compactIndicator = compactIndicator;
            _expandedIndicator = expandedIndicator;
            _minimalHalo = minimalHalo;
            _compactHalo = compactHalo;
            _expandedHalo = expandedHalo;
            _islandScale = islandScale;
            _artScales = new[] { minimalArtScale, artScale, expandedArtScale };
            _gifScales = new[] { minimalGifScale, compactGifScale, expandedGifScale };
        }

        public void Render(int skin, bool breathingEnabled, bool isPeekingDrop, Grid requestedMode,
            float band0, float band1, float band2, float band3)
        {
            Grid activeMode = isPeekingDrop ? _compactMode : requestedMode;
            if (activeMode == null || activeMode.Visibility != Visibility.Visible)
                return;

            StackPanel indicator = activeMode == _expandedMode ? _expandedIndicator :
                activeMode == _compactMode ? _compactIndicator : _minimalIndicator;

            if (skin == 0)
                UpdateIndicatorBars(indicator, band0, band1, band2, band3, 4, activeMode == _expandedMode ? 20 : 18);
            else if (skin == 1)
                UpdateHaloRing(activeMode, band0, band1, band2, band3);
            else if (skin == 2)
                UpdateIndicatorDots(indicator, band0, band1, band2, band3, activeMode == _expandedMode ? 13 : 11);
            else
            {
                UpdateIndicatorBars(indicator, band0, band1, band2, band3, 4, activeMode == _expandedMode ? 20 : 18);
                UpdateHaloRing(activeMode, band0, band1, band2, band3);
            }

            if (breathingEnabled && !isPeekingDrop)
                ApplyAudioBreathing(band0, band1, band2, band3);
        }

        public void ApplySkin(int skin, bool isPlaying)
        {
            if (skin == 1)
            {
                SetIndicatorVisibility(_minimalIndicator, Visibility.Collapsed);
                SetIndicatorVisibility(_compactIndicator, Visibility.Collapsed);
                SetIndicatorVisibility(_expandedIndicator, Visibility.Collapsed);
                SetHaloOpacity(isPlaying ? 0.45 : 0);
            }
            else
            {
                if (skin == 2)
                {
                    SetHaloOpacity(0);
                    PrepareDotTransforms(_minimalIndicator);
                    PrepareDotTransforms(_compactIndicator);
                    PrepareDotTransforms(_expandedIndicator);
                }
                else
                    SetHaloOpacity(skin == 3 && isPlaying ? 0.45 : 0);

                ResetBarsGeometry(_minimalIndicator);
                ResetBarsGeometry(_compactIndicator);
                ResetBarsGeometry(_expandedIndicator);
                var visibility = isPlaying ? Visibility.Visible : Visibility.Collapsed;
                SetIndicatorVisibility(_minimalIndicator, visibility);
                SetIndicatorVisibility(_compactIndicator, visibility);
                SetIndicatorVisibility(_expandedIndicator, visibility);
            }
        }

        public void UpdateIndicatorsVisibility(bool isPlaying, int skin)
        {
            var visibility = isPlaying && skin != 1 ? Visibility.Visible : Visibility.Collapsed;
            SetIndicatorVisibility(_minimalIndicator, visibility);
            SetIndicatorVisibility(_compactIndicator, visibility);
            SetIndicatorVisibility(_expandedIndicator, visibility);
            SetHaloOpacity(isPlaying && (skin == 1 || skin == 3) ? 0.45 : 0);
        }

        public void Reset()
        {
            _breathingScale = 1.0;
            _artBreathingScale = 1.0;
            if (_islandScale != null)
            {
                _islandScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                _islandScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                _islandScale.ScaleX = 1.0;
                _islandScale.ScaleY = 1.0;
            }
            foreach (var transform in _artScales) SetScale(transform, 1.0);
            foreach (var transform in _gifScales) SetScale(transform, 1.0);
        }

        private void ApplyAudioBreathing(float b0, float b1, float b2, float b3)
        {
            if (_islandScale == null) return;
            float energy = Math.Clamp(b0 * 0.24f + b1 * 0.18f + b2 * 0.26f + b3 * 0.32f, 0f, 1f);
            double target = 0.970 + Math.Min(0.030, energy * 0.030);
            _breathingScale = target > _breathingScale ? _breathingScale * 0.45 + target * 0.55 : _breathingScale * 0.76 + target * 0.24;
            _breathingScale = Math.Min(1.0, _breathingScale);
            if (Math.Abs(_islandScale.ScaleX - _breathingScale) > 0.0004)
            {
                _islandScale.ScaleX = _breathingScale;
                _islandScale.ScaleY = _breathingScale;
            }
            double artTarget = 1.0 + Math.Min(0.075, energy * 0.075);
            _artBreathingScale = artTarget > _artBreathingScale ? _artBreathingScale * 0.40 + artTarget * 0.60 : _artBreathingScale * 0.74 + artTarget * 0.26;
            foreach (var transform in _artScales) SetScale(transform, _artBreathingScale);
            foreach (var transform in _gifScales) SetScale(transform, _artBreathingScale);
        }

        private static void UpdateIndicatorBars(StackPanel indicator, float b0, float b1, float b2, float b3, double min, double max)
        {
            if (indicator?.Visibility != Visibility.Visible || indicator.Children.Count < 4) return;
            double range = max - min;
            SetBarHeight(indicator.Children[0], min + b0 * range);
            SetBarHeight(indicator.Children[1], min + b1 * range);
            SetBarHeight(indicator.Children[2], min + b2 * range);
            SetBarHeight(indicator.Children[3], min + b3 * range);
        }

        private static void SetBarHeight(UIElement element, double height)
        {
            if (element is not Rectangle rect) return;
            rect.VerticalAlignment = VerticalAlignment.Center;
            rect.Width = 3.0; rect.RadiusX = 1.5; rect.RadiusY = 1.5;
            if (rect.RenderTransform is TranslateTransform translate) translate.Y = 0;
            if (Math.Abs(rect.Height - height) > 0.4) rect.Height = height;
        }

        private void UpdateHaloRing(Grid activeMode, float b0, float b1, float b2, float b3)
        {
            float energy = Math.Clamp(b0 * 0.65f + b1 * 0.40f + b2 * 0.20f, 0f, 1f);
            var halo = activeMode == _expandedMode ? _expandedHalo : activeMode == _compactMode ? _compactHalo : _minimalHalo;
            ApplyHalo(halo, 0.35 + energy * 0.65, 1.3 + energy * 0.9 + (activeMode == _expandedMode ? 0.3 : 0), 14 + energy * 14 + (activeMode == _expandedMode ? 5 : 0));
        }

        private static void ApplyHalo(Ellipse halo, double opacity, double thickness, double blur)
        {
            if (halo == null) return;
            if (Math.Abs(halo.Opacity - opacity) > 0.01) halo.Opacity = opacity;
            if (Math.Abs(halo.StrokeThickness - thickness) > 0.02) halo.StrokeThickness = thickness;
            if (halo.Effect is System.Windows.Media.Effects.DropShadowEffect effect)
            {
                if (Math.Abs(effect.BlurRadius - blur) > 0.2) effect.BlurRadius = blur;
                effect.Opacity = Math.Min(1.0, opacity + 0.15);
            }
        }

        public void SetHaloOpacity(double opacity)
        {
            SetOpacity(_minimalHalo, opacity); SetOpacity(_compactHalo, opacity); SetOpacity(_expandedHalo, opacity);
        }

        private static void SetOpacity(UIElement element, double opacity)
        {
            if (element != null && Math.Abs(element.Opacity - opacity) > 0.01) element.Opacity = opacity;
        }

        private static void UpdateIndicatorDots(StackPanel indicator, float b0, float b1, float b2, float b3, double range)
        {
            if (indicator?.Visibility != Visibility.Visible || indicator.Children.Count < 4) return;
            SetDotBounce(indicator.Children[0], b0, range); SetDotBounce(indicator.Children[1], b1, range);
            SetDotBounce(indicator.Children[2], b2, range); SetDotBounce(indicator.Children[3], b3, range);
        }

        private static void SetDotBounce(UIElement element, float band, double range)
        {
            if (element is not Rectangle rect) return;
            rect.VerticalAlignment = VerticalAlignment.Bottom;
            rect.Width = 4.5; rect.Height = 4.5; rect.RadiusX = 2.25; rect.RadiusY = 2.25;
            if (rect.RenderTransform is TranslateTransform translate)
            {
                double y = -Math.Clamp(band * range, 0, range);
                if (Math.Abs(translate.Y - y) > 0.2) translate.Y = y;
            }
        }

        private static void PrepareDotTransforms(Panel panel)
        {
            if (panel == null) return;
            foreach (var child in panel.Children)
                if (child is Rectangle rect && rect.RenderTransform is not TranslateTransform)
                    rect.RenderTransform = new TranslateTransform();
        }

        private static void ResetBarsGeometry(Panel panel)
        {
            if (panel == null) return;
            foreach (var child in panel.Children)
                if (child is Rectangle rect)
                {
                    rect.VerticalAlignment = VerticalAlignment.Center; rect.Width = 3.0; rect.RadiusX = 1.5; rect.RadiusY = 1.5;
                    if (rect.RenderTransform is TranslateTransform translate) translate.Y = 0;
                }
        }

        private static void SetIndicatorVisibility(UIElement element, Visibility visibility)
        {
            if (element != null && element.Visibility != visibility) element.Visibility = visibility;
        }

        private static void SetScale(ScaleTransform transform, double scale)
        {
            if (transform != null && Math.Abs(transform.ScaleX - scale) > 0.0005) { transform.ScaleX = scale; transform.ScaleY = scale; }
        }
    }
}
