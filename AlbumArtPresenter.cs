using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DynamicIslandPC
{
    internal sealed class AlbumArtTrack
    {
        public string Title { get; init; }
        public string Artist { get; init; }
        public string Source { get; init; }
        public ImageSource AlbumArt { get; init; }
    }

    internal sealed class AlbumArtPresenter : IDisposable
    {
        private readonly Image _minimalImage;
        private readonly Image _currentImage;
        private readonly Image _expandedImage;
        private readonly Image _pausedImage;
        private readonly Image _oldImage;
        private readonly Image _expandedOldImage;
        private readonly Image _revealImage;
        private readonly Grid _compactOld;
        private readonly Grid _compactNew;
        private readonly Grid _expandedOld;
        private readonly Grid _expandedNew;
        private readonly TranslateTransform _compactOldTranslate;
        private readonly TranslateTransform _compactNewTranslate;
        private readonly TranslateTransform _expandedOldTranslate;
        private readonly TranslateTransform _expandedNewTranslate;
        private readonly TextBlock _compactOldTitle;
        private readonly TextBlock _compactOldArtist;
        private readonly TextBlock _compactOldSource;
        private readonly TextBlock _expandedOldTitle;
        private readonly TextBlock _expandedOldArtist;
        private readonly TextBlock _expandedOldSource;
        private readonly TextBlock _compactTitle;
        private readonly TextBlock _compactArtist;
        private readonly TextBlock _compactSource;
        private readonly TextBlock _expandedTitle;
        private readonly TextBlock _expandedArtist;
        private readonly TextBlock _expandedSource;
        private readonly TextBlock _revealTitle;
        private readonly TextBlock _revealSubtitle;
        private readonly TextBlock _revealSource;
        private readonly Border _revealOverlay;
        private readonly ScaleTransform[] _artScales;
        private readonly RotateTransform[] _rotations;
        private readonly Func<bool> _getFluidPhysics;
        private readonly Action _pulseIslandDroplet;

        private Storyboard _slideStoryboard;
        private EventHandler _slideCompleted;
        private Storyboard _revealStoryboard;
        private EventHandler _revealCompleted;
        private Storyboard _pulseStoryboard;
        private EventHandler _pulseCompleted;
        private AnimationClock _vinylClock;
        private bool _isVinylSpinning;
        private int _slideDirection = -1;
        private bool _disposed;

        public AlbumArtPresenter(
            Image minimalImage, Image currentImage, Image expandedImage, Image pausedImage,
            Image oldImage, Image expandedOldImage, Image revealImage,
            Grid compactOld, Grid compactNew, Grid expandedOld, Grid expandedNew,
            TranslateTransform compactOldTranslate, TranslateTransform compactNewTranslate,
            TranslateTransform expandedOldTranslate, TranslateTransform expandedNewTranslate,
            TextBlock compactOldTitle, TextBlock compactOldArtist, TextBlock compactOldSource,
            TextBlock expandedOldTitle, TextBlock expandedOldArtist, TextBlock expandedOldSource,
            TextBlock compactTitle, TextBlock compactArtist, TextBlock compactSource,
            TextBlock expandedTitle, TextBlock expandedArtist, TextBlock expandedSource,
            TextBlock revealTitle, TextBlock revealSubtitle, TextBlock revealSource, Border revealOverlay,
            ScaleTransform minimalScale, ScaleTransform currentScale, ScaleTransform expandedScale,
            RotateTransform minimalRotation, RotateTransform currentRotation, RotateTransform expandedRotation,
            RotateTransform oldRotation, RotateTransform expandedOldRotation,
            Func<bool> getFluidPhysics, Action pulseIslandDroplet)
        {
            _minimalImage = minimalImage;
            _currentImage = currentImage;
            _expandedImage = expandedImage;
            _pausedImage = pausedImage;
            _oldImage = oldImage;
            _expandedOldImage = expandedOldImage;
            _revealImage = revealImage;
            _compactOld = compactOld;
            _compactNew = compactNew;
            _expandedOld = expandedOld;
            _expandedNew = expandedNew;
            _compactOldTranslate = compactOldTranslate;
            _compactNewTranslate = compactNewTranslate;
            _expandedOldTranslate = expandedOldTranslate;
            _expandedNewTranslate = expandedNewTranslate;
            _compactOldTitle = compactOldTitle;
            _compactOldArtist = compactOldArtist;
            _compactOldSource = compactOldSource;
            _expandedOldTitle = expandedOldTitle;
            _expandedOldArtist = expandedOldArtist;
            _expandedOldSource = expandedOldSource;
            _compactTitle = compactTitle;
            _compactArtist = compactArtist;
            _compactSource = compactSource;
            _expandedTitle = expandedTitle;
            _expandedArtist = expandedArtist;
            _expandedSource = expandedSource;
            _revealTitle = revealTitle;
            _revealSubtitle = revealSubtitle;
            _revealSource = revealSource;
            _revealOverlay = revealOverlay;
            _artScales = new[] { minimalScale, currentScale, expandedScale };
            _rotations = new[] { minimalRotation, currentRotation, expandedRotation, oldRotation, expandedOldRotation };
            _getFluidPhysics = getFluidPhysics;
            _pulseIslandDroplet = pulseIslandDroplet;
            _revealOverlay.RenderTransform = new TranslateTransform();
        }

        public void SetSlideDirection(int direction)
        {
            _slideDirection = direction < 0 ? -1 : 1;
        }

        public void PresentTrack(AlbumArtTrack track, bool pulseAlbumArt)
        {
            if (_disposed || track == null) return;
            CancelTransition();

            ResetSlideLayerState(_compactOld, _compactNew, _compactOldTranslate, _compactNewTranslate);
            ResetSlideLayerState(_expandedOld, _expandedNew, _expandedOldTranslate, _expandedNewTranslate);
            _oldImage.Source = _currentImage.Source;
            _expandedOldImage.Source = _expandedImage.Source;
            _compactOldTitle.Text = _compactTitle.Text;
            _compactOldArtist.Text = _compactArtist.Text;
            _compactOldSource.Text = _compactSource.Text;
            _expandedOldTitle.Text = _expandedTitle.Text;
            _expandedOldArtist.Text = _expandedArtist.Text;
            _expandedOldSource.Text = _expandedSource.Text;

            _currentImage.Source = track.AlbumArt;
            _expandedImage.Source = track.AlbumArt;
            _minimalImage.Source = track.AlbumArt;
            _pausedImage.Source = track.AlbumArt;
            _compactTitle.Text = track.Title ?? "Неизвестный трек";
            _compactArtist.Text = track.Artist ?? "Неизвестный исполнитель";
            _compactSource.Text = track.Source ?? "Media";
            _expandedTitle.Text = track.Title ?? "Неизвестный трек";
            _expandedArtist.Text = track.Artist ?? "Неизвестный исполнитель";
            _expandedSource.Text = track.Source ?? "Media";

            if (_oldRotation != null && _currentRotation != null)
                _oldRotation.Angle = _currentRotation.Angle;
            if (_expandedOldRotation != null && _expandedRotation != null)
                _expandedOldRotation.Angle = _expandedRotation.Angle;

            StartSlide();
            if (pulseAlbumArt) PulseAlbumArt();
            _pulseIslandDroplet?.Invoke();
        }

        public void UpdateCurrentArtwork(ImageSource albumArt)
        {
            if (_disposed) return;
            _minimalImage.Source = albumArt;
            _currentImage.Source = albumArt;
            _expandedImage.Source = albumArt;
            _pausedImage.Source = albumArt;
        }

        public void ShowTrackReveal(AlbumArtTrack track)
        {
            if (_disposed || track == null) return;
            StopRevealStoryboard();
            _revealImage.Source = track.AlbumArt;
            _revealTitle.Text = track.Title ?? "Неизвестный трек";
            _revealSubtitle.Text = track.Artist ?? "Неизвестный исполнитель";
            _revealSource.Text = track.Source ?? string.Empty;
            _revealSource.Visibility = string.IsNullOrWhiteSpace(_revealSource.Text) ? Visibility.Collapsed : Visibility.Visible;
            _revealOverlay.Visibility = Visibility.Visible;
            _revealOverlay.Opacity = 0;
            ((TranslateTransform)_revealOverlay.RenderTransform).X = 0;
            ((TranslateTransform)_revealOverlay.RenderTransform).Y = 6;

            var storyboard = new Storyboard();
            AddAnimation(storyboard, _revealOverlay, UIElement.OpacityProperty, 0, 1, TimeSpan.Zero, TimeSpan.FromMilliseconds(180));
            AddAnimation(storyboard, _revealOverlay, UIElement.OpacityProperty, 1, 1, TimeSpan.FromMilliseconds(180), TimeSpan.FromMilliseconds(900));
            AddAnimation(storyboard, _revealOverlay, UIElement.OpacityProperty, 1, 0, TimeSpan.FromMilliseconds(1080), TimeSpan.FromMilliseconds(260));
            AddAnimation(storyboard, (TranslateTransform)_revealOverlay.RenderTransform, TranslateTransform.YProperty, 6, 0, TimeSpan.Zero, TimeSpan.FromMilliseconds(220), new QuadraticEase { EasingMode = EasingMode.EaseOut });
            AddAnimation(storyboard, (TranslateTransform)_revealOverlay.RenderTransform, TranslateTransform.YProperty, 0, -4, TimeSpan.FromMilliseconds(1080), TimeSpan.FromMilliseconds(260), new QuadraticEase { EasingMode = EasingMode.EaseIn });
            StartRevealStoryboard(storyboard);
        }

        public void CancelTransition()
        {
            StopSlideStoryboard();
            StopRevealStoryboard();
            StopPulseStoryboard();
            ResetSlideLayerState(_compactOld, _compactNew, _compactOldTranslate, _compactNewTranslate);
            ResetSlideLayerState(_expandedOld, _expandedNew, _expandedOldTranslate, _expandedNewTranslate);
            ClearOldSlideAlbumArt();
            _revealOverlay.BeginAnimation(UIElement.OpacityProperty, null);
            _revealOverlay.Visibility = Visibility.Collapsed;
            _revealOverlay.Opacity = 0;
        }

        public void ClearForDiagnostics(ImageSource defaultArt)
        {
            CancelTransition();
            UpdateCurrentArtwork(defaultArt);
            _revealImage.Source = defaultArt;
        }

        public void PulseAlbumArt()
        {
            if (_disposed) return;
            StopPulseStoryboard();
            var storyboard = new Storyboard();
            foreach (var scale in _artScales)
            {
                if (scale == null) continue;
                AddKeyFrameAnimation(storyboard, scale, ScaleTransform.ScaleXProperty);
                AddKeyFrameAnimation(storyboard, scale, ScaleTransform.ScaleYProperty);
            }
            StartPulseStoryboard(storyboard);
        }

        public void StartRotation(double fromAngle = double.NaN)
        {
            if (_disposed) return;
            if (!double.IsNaN(fromAngle))
            {
                double normalized = fromAngle % 360.0;
                if (normalized < 0) normalized += 360.0;
                var animation = new DoubleAnimation(normalized, normalized + 360.0, TimeSpan.FromSeconds(12)) { RepeatBehavior = RepeatBehavior.Forever };
                _vinylClock = animation.CreateClock();
                ApplyRotationClock(_vinylClock);
                _isVinylSpinning = true;
                return;
            }
            if (_vinylClock == null)
            {
                var animation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(12)) { RepeatBehavior = RepeatBehavior.Forever };
                _vinylClock = animation.CreateClock();
                ApplyRotationClock(_vinylClock);
                _isVinylSpinning = true;
            }
            else if (!_isVinylSpinning)
            {
                _vinylClock.Controller.Resume();
                _isVinylSpinning = true;
            }
        }

        public void StopRotation()
        {
            if (_vinylClock != null && _isVinylSpinning)
            {
                _vinylClock.Controller.Pause();
                _isVinylSpinning = false;
            }
        }

        public void ResetVinylRotation()
        {
            try { _vinylClock?.Controller?.Stop(); } catch { }
            _isVinylSpinning = false;
            foreach (var rotation in _rotations)
            {
                if (rotation == null) continue;
                rotation.ApplyAnimationClock(RotateTransform.AngleProperty, null);
                rotation.Angle = 0;
            }
            _vinylClock = null;
        }

        public double CurrentVinylAngle => _currentRotation?.Angle ?? _minimalRotation?.Angle ?? 0;

        public void DetachVinylClocksForScratch(double currentAngle)
        {
            try { _vinylClock?.Controller?.Stop(); } catch { }
            _vinylClock = null;
            _isVinylSpinning = false;
            foreach (var rotation in _rotations)
            {
                if (rotation == null) continue;
                rotation.ApplyAnimationClock(RotateTransform.AngleProperty, null);
                rotation.Angle = currentAngle;
            }
        }

        public void SetVinylManualAngle(double angle)
        {
            foreach (var rotation in _rotations)
                if (rotation != null) rotation.Angle = angle;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CancelTransition();
            ResetVinylRotation();
        }

        private RotateTransform _minimalRotation => _rotations[0];
        private RotateTransform _currentRotation => _rotations[1];
        private RotateTransform _expandedRotation => _rotations[2];
        private RotateTransform _oldRotation => _rotations[3];
        private RotateTransform _expandedOldRotation => _rotations[4];

        private void StartSlide()
        {
            int direction = _slideDirection == 0 ? -1 : _slideDirection;
            double compactExit = direction * 20, compactEnter = -direction * 24;
            double expandedExit = direction * 24, expandedEnter = -direction * 28;
            var exitDuration = TimeSpan.FromMilliseconds(340);
            var enterDuration = _getFluidPhysics() ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromMilliseconds(460);
            var enterEase = _getFluidPhysics()
                ? (IEasingFunction)new BackEase { Amplitude = 0.55, EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseOut };
            var smoothEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            var storyboard = new Storyboard();
            AddSlideAnimations(storyboard, _compactOld, _compactNew, _compactOldTranslate, _compactNewTranslate, compactExit, compactEnter, exitDuration, enterDuration, enterEase, smoothEase);
            AddSlideAnimations(storyboard, _expandedOld, _expandedNew, _expandedOldTranslate, _expandedNewTranslate, expandedExit, expandedEnter, exitDuration, enterDuration, enterEase, smoothEase);
            StartSlideStoryboard(storyboard);
        }

        private void AddSlideAnimations(Storyboard storyboard, Grid oldLayer, Grid newLayer, TranslateTransform oldTransform, TranslateTransform newTransform, double exitX, double enterX, TimeSpan exitDuration, TimeSpan enterDuration, IEasingFunction enterEase, IEasingFunction smoothEase)
        {
            AddAnimation(storyboard, oldTransform, TranslateTransform.XProperty, 0, exitX, TimeSpan.Zero, exitDuration, smoothEase);
            AddAnimation(storyboard, oldLayer, UIElement.OpacityProperty, 1, 0, TimeSpan.Zero, exitDuration, smoothEase);
            AddAnimation(storyboard, newTransform, TranslateTransform.XProperty, enterX, 0, TimeSpan.Zero, enterDuration, enterEase);
            AddAnimation(storyboard, newLayer, UIElement.OpacityProperty, 0, 1, TimeSpan.Zero, TimeSpan.FromMilliseconds(380), smoothEase);
        }

        private void StartSlideStoryboard(Storyboard storyboard)
        {
            StopSlideStoryboard();
            _slideStoryboard = storyboard;
            _slideCompleted = (_, __) =>
            {
                storyboard.Completed -= _slideCompleted;
                _slideStoryboard = null;
                _slideCompleted = null;
                ResetSlideLayerState(_compactOld, _compactNew, _compactOldTranslate, _compactNewTranslate);
                ResetSlideLayerState(_expandedOld, _expandedNew, _expandedOldTranslate, _expandedNewTranslate);
                ClearOldSlideAlbumArt();
                _slideDirection = -1;
            };
            storyboard.Completed += _slideCompleted;
            storyboard.Begin();
        }

        private void StartRevealStoryboard(Storyboard storyboard)
        {
            StopRevealStoryboard();
            _revealStoryboard = storyboard;
            _revealCompleted = (_, __) =>
            {
                storyboard.Completed -= _revealCompleted;
                _revealStoryboard = null;
                _revealCompleted = null;
                _revealOverlay.Visibility = Visibility.Collapsed;
                _revealOverlay.Opacity = 0;
            };
            storyboard.Completed += _revealCompleted;
            storyboard.Begin();
        }

        private void StartPulseStoryboard(Storyboard storyboard)
        {
            _pulseStoryboard = storyboard;
            _pulseCompleted = (_, __) =>
            {
                storyboard.Completed -= _pulseCompleted;
                _pulseStoryboard = null;
                _pulseCompleted = null;
                foreach (var scale in _artScales)
                {
                    if (scale == null) continue;
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    scale.ScaleX = 1;
                    scale.ScaleY = 1;
                }
            };
            storyboard.Completed += _pulseCompleted;
            storyboard.Begin();
        }

        private void StopSlideStoryboard()
        {
            if (_slideStoryboard == null) return;
            if (_slideCompleted != null) _slideStoryboard.Completed -= _slideCompleted;
            _slideStoryboard.Stop();
            _slideStoryboard = null;
            _slideCompleted = null;
        }

        private void StopRevealStoryboard()
        {
            if (_revealStoryboard == null) return;
            if (_revealCompleted != null) _revealStoryboard.Completed -= _revealCompleted;
            _revealStoryboard.Stop();
            _revealStoryboard = null;
            _revealCompleted = null;
        }

        private void StopPulseStoryboard()
        {
            if (_pulseStoryboard == null) return;
            if (_pulseCompleted != null) _pulseStoryboard.Completed -= _pulseCompleted;
            _pulseStoryboard.Stop();
            _pulseStoryboard = null;
            _pulseCompleted = null;
            foreach (var scale in _artScales)
            {
                if (scale == null) continue;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleX = 1;
                scale.ScaleY = 1;
            }
        }

        private void ClearOldSlideAlbumArt()
        {
            _oldImage.Source = null;
            _expandedOldImage.Source = null;
        }

        private static void ResetSlideLayerState(Grid oldLayer, Grid newLayer, TranslateTransform oldTransform, TranslateTransform newTransform)
        {
            oldTransform.BeginAnimation(TranslateTransform.XProperty, null);
            newTransform.BeginAnimation(TranslateTransform.XProperty, null);
            oldLayer.BeginAnimation(UIElement.OpacityProperty, null);
            newLayer.BeginAnimation(UIElement.OpacityProperty, null);
            oldTransform.X = 0;
            newTransform.X = 0;
            oldLayer.Opacity = 0;
            newLayer.Opacity = 1;
        }

        private void ApplyRotationClock(AnimationClock clock)
        {
            foreach (var rotation in _rotations)
                rotation?.ApplyAnimationClock(RotateTransform.AngleProperty, clock);
        }

        private static void AddAnimation(Storyboard storyboard, DependencyObject target, DependencyProperty property, double from, double to, TimeSpan begin, TimeSpan duration, IEasingFunction easing = null)
        {
            var animation = new DoubleAnimation { From = from, To = to, BeginTime = begin, Duration = duration, EasingFunction = easing };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            storyboard.Children.Add(animation);
        }

        private static void AddKeyFrameAnimation(Storyboard storyboard, ScaleTransform target, DependencyProperty property)
        {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.05, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(460))) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            storyboard.Children.Add(animation);
        }
    }
}
