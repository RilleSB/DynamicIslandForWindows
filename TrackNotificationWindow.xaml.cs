using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Forms;

namespace DynamicIslandPC
{
    public partial class TrackNotificationWindow : Window
    {
        private DispatcherTimer _closeTimer;

        public TrackNotificationWindow(string text, double ownerLeft, double ownerTop, double ownerWidth, double ownerHeight)
        {
            InitializeComponent();
            NotificationText.Text = text;

            Loaded += (s, e) =>
            {
                UpdatePosition(ownerLeft, ownerTop, ownerWidth, ownerHeight);
                FadeIn();
            };
        }

        public void UpdateText(string text, double ownerLeft, double ownerTop, double ownerWidth, double ownerHeight)
        {
            NotificationText.Text = text;
            UpdatePosition(ownerLeft, ownerTop, ownerWidth, ownerHeight);
            ResetTimer();
        }

        public void UpdateOwnerBounds(double ownerLeft, double ownerTop, double ownerWidth, double ownerHeight)
        {
            UpdatePosition(ownerLeft, ownerTop, ownerWidth, ownerHeight);
        }

        private void UpdatePosition(double ownerLeft, double ownerTop, double ownerWidth, double ownerHeight)
        {
            var centerX = ownerLeft + ownerWidth / 2;
            var desiredLeft = centerX - ActualWidth / 2;
            var desiredTop = ownerTop + ownerHeight + 14;

            var screen = Screen.FromPoint(new System.Drawing.Point((int)Math.Round(centerX), (int)Math.Round(desiredTop)));
            var workingArea = screen.WorkingArea;

            Left = Math.Clamp(desiredLeft, workingArea.Left + 8, workingArea.Right - ActualWidth - 8);
            Top = Math.Clamp(desiredTop, workingArea.Top + 8, workingArea.Bottom - ActualHeight - 8);
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
            _closeTimer.Tick += (s, e) => { _closeTimer.Stop(); FadeOut(); };
            _closeTimer.Start();
        }

        private void ResetTimer()
        {
            _closeTimer?.Stop();
            BeginAnimation(OpacityProperty, null); // сбрасываем fade-out если шёл
            Opacity = 1;
            ScheduleClose();
        }

        private void FadeOut()
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, e) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
