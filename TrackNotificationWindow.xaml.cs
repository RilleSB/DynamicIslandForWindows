using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DynamicIslandPC
{
    public partial class TrackNotificationWindow : Window
    {
        private DispatcherTimer _closeTimer;

        public TrackNotificationWindow(string text, double ownerLeft, double ownerTop, double ownerWidth, double ownerHeight)
        {
            InitializeComponent();
            NotificationText.Text = text;

            // Позиционируем после загрузки чтобы знать ActualWidth
            Loaded += (s, e) =>
            {
                Left = ownerLeft + ownerWidth / 2 - ActualWidth / 2;
                Top = ownerTop + ownerHeight + 8;
                FadeIn();
            };
        }

        private void FadeIn()
        {
            Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
            fadeIn.Completed += (s, e) => ScheduleClose();
            BeginAnimation(OpacityProperty, fadeIn);
        }

        private void ScheduleClose()
        {
            _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _closeTimer.Tick += (s, e) =>
            {
                _closeTimer.Stop();
                FadeOut();
            };
            _closeTimer.Start();
        }

        private void FadeOut()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, e) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
