using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace DynamicIslandPC
{
    internal sealed class DecorationController : IDisposable
    {
        private readonly Grid[] _containers;
        private readonly Image[] _images;
        private readonly MediaElement[] _videos;
        private string _mediaPath = string.Empty;
        private bool _enabled;
        private bool _isVideo;
        private bool _isPlaying;
        private int _displayMode;
        private bool _isPeekingDrop;
        private Uri _videoUri;
        private BitmapImage _imageSource;
        private bool _imageIsAnimated;
        private Image _loadedImage;
        private MediaElement _loadedVideo;
        private bool _disposed;

        public DecorationController(
            Grid minimalContainer, Grid compactContainer, Grid expandedContainer,
            Image minimalImage, Image compactImage, Image expandedImage,
            MediaElement minimalVideo, MediaElement compactVideo, MediaElement expandedVideo)
        {
            _containers = new[] { minimalContainer, compactContainer, expandedContainer };
            _images = new[] { minimalImage, compactImage, expandedImage };
            _videos = new[] { minimalVideo, compactVideo, expandedVideo };
        }

        public void ApplySettings(bool enabled, string mediaPath, int displayMode, bool isPeekingDrop, bool isPlaying)
        {
            if (_disposed) return;

            mediaPath ??= string.Empty;
            bool mediaChanged = _enabled != enabled || !string.Equals(_mediaPath, mediaPath, StringComparison.OrdinalIgnoreCase);
            _enabled = enabled;
            _mediaPath = mediaPath;
            _displayMode = Math.Clamp(displayMode, 0, 2);
            _isPeekingDrop = isPeekingDrop;
            _isPlaying = isPlaying;

            if (mediaChanged)
                LoadMedia();

            UpdateVisibility(isPlaying, _displayMode, _isPeekingDrop);
        }

        public void UpdateVisibility(bool isPlaying, int displayMode, bool isPeekingDrop)
        {
            if (_disposed) return;
            _isPlaying = isPlaying;
            _displayMode = Math.Clamp(displayMode, 0, 2);
            _isPeekingDrop = isPeekingDrop;

            bool visible = _enabled && _isPlaying;
            SetContainersVisibility(visible);

            if (_isVideo)
            {
                ClearImages();
                UpdateVideoVisibility(visible);
            }
            else
            {
                ClearVideos();
                UpdateImageVisibility(visible);
            }
        }

        public void OnVideoMediaEnded(object sender)
        {
            if (sender is not MediaElement video || !_isVideo || !_isPlaying ||
                video != _loadedVideo || video != GetActiveVideo())
                return;

            try
            {
                video.Position = TimeSpan.Zero;
                video.Play();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to restart decoration video", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearVideos();
            ClearImages();
            _imageSource = null;
            _videoUri = null;
        }

        private void LoadMedia()
        {
            ClearVideos();
            ClearImages();
            _imageSource = null;
            _videoUri = null;
            _isVideo = false;

            bool customAvailable = !string.IsNullOrWhiteSpace(_mediaPath) &&
                File.Exists(_mediaPath) && IsSupportedPath(_mediaPath);

            if (customAvailable && IsVideoPath(_mediaPath))
            {
                _isVideo = true;
                if (_enabled)
                    _videoUri = new Uri(_mediaPath, UriKind.Absolute);
                return;
            }

            if (!_enabled) return;

            Uri imageUri = customAvailable
                ? new Uri(_mediaPath, UriKind.Absolute)
                : new Uri("pack://application:,,,/flex.gif", UriKind.Absolute);
            bool animated = !customAvailable || Path.GetExtension(_mediaPath).Equals(".gif", StringComparison.OrdinalIgnoreCase);

            try
            {
                _imageIsAnimated = animated;
                _imageSource = LoadBitmap(imageUri);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load custom decoration image, falling back to flex.gif", ex);
                _imageIsAnimated = true;
                _imageSource = LoadBitmap(new Uri("pack://application:,,,/flex.gif", UriKind.Absolute));
            }
        }

        private void UpdateImageVisibility(bool visible)
        {
            if (visible)
                LoadImageForActiveMode();
            else
                ClearImages();

            for (int i = 0; i < _images.Length; i++)
                _images[i].Visibility = visible && _loadedImage == _images[i]
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadImageForActiveMode()
        {
            if (!_enabled || _isVideo || _imageSource == null)
            {
                ClearImages();
                return;
            }

            Image activeImage = GetActiveImage();
            if (_loadedImage == activeImage)
            {
                ResumeAnimation(activeImage);
                return;
            }

            PauseAnimation(_loadedImage);
            if (WpfAnimatedGif.ImageBehavior.GetAnimatedSource(activeImage) == null && activeImage.Source == null)
                SetImage(activeImage, _imageSource, _imageIsAnimated);

            ResumeAnimation(activeImage);
            _loadedImage = activeImage;
        }

        private void UpdateVideoVisibility(bool visible)
        {
            if (!visible || !_enabled || !_isVideo || _videoUri == null)
            {
                ClearVideos();
                return;
            }

            MediaElement activeVideo = GetActiveVideo();
            if (_loadedVideo != activeVideo)
            {
                ClearVideos();
                activeVideo.Source = _videoUri;
                activeVideo.Position = TimeSpan.Zero;
                _loadedVideo = activeVideo;
            }

            activeVideo.Visibility = Visibility.Visible;
            try { activeVideo.Play(); }
            catch (Exception ex) { Logger.Error("Failed to update decoration video playback", ex); }
        }

        private Image GetActiveImage() => _images[GetActiveModeIndex()];

        private MediaElement GetActiveVideo() => _videos[GetActiveModeIndex()];

        private int GetActiveModeIndex() => _isPeekingDrop ? 1 : _displayMode;

        private void SetContainersVisibility(bool visible)
        {
            var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            foreach (var container in _containers)
                container.Visibility = visibility;
        }

        private void ClearImages()
        {
            foreach (var image in _images)
            {
                PauseAnimation(image);
                WpfAnimatedGif.ImageBehavior.SetAnimatedSource(image, null);
                image.Source = null;
                image.Visibility = Visibility.Collapsed;
            }
            _loadedImage = null;
        }

        private void ClearVideos()
        {
            foreach (var video in _videos)
            {
                try
                {
                    video.Stop();
                    video.Close();
                    video.Source = null;
                    video.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to release decoration video", ex);
                }
            }
            _loadedVideo = null;
        }

        private static void PauseAnimation(Image image)
        {
            if (image != null)
                WpfAnimatedGif.ImageBehavior.GetAnimationController(image)?.Pause();
        }

        private static void ResumeAnimation(Image image)
        {
            if (image != null)
                WpfAnimatedGif.ImageBehavior.GetAnimationController(image)?.Play();
        }

        private static BitmapImage LoadBitmap(Uri uri)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = uri;
            bitmap.EndInit();
            return bitmap;
        }

        private static void SetImage(Image image, BitmapImage bitmap, bool animated)
        {
            if (animated)
                WpfAnimatedGif.ImageBehavior.SetAnimatedSource(image, bitmap);
            else
            {
                WpfAnimatedGif.ImageBehavior.SetAnimatedSource(image, null);
                image.Source = bitmap;
            }
        }

        private static bool IsVideoPath(string path) => Path.GetExtension(path).ToLowerInvariant() is
            ".mp4" or ".m4v" or ".mov" or ".wmv" or ".avi" or ".webm" or ".mkv";

        private static bool IsSupportedPath(string path) => Path.GetExtension(path).ToLowerInvariant() is
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or
            ".mp4" or ".m4v" or ".mov" or ".wmv" or ".avi" or ".webm" or ".mkv";
    }
}
