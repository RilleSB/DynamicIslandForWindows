using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Forms;

namespace DynamicIslandPC
{
    public partial class SettingsWindow : Window
    {
        public double PositionX { get; private set; }
        public double PositionY { get; private set; }
        private bool isUpdating = false;
        private Action<double, double> onPositionChanged;
        private Action<Color> onColorChanged;
        private Screen currentScreen;

        public SettingsWindow(double currentX, double currentY, Action<double, double> positionChangedCallback, Action<Color> colorChangedCallback = null)
        {
            InitializeComponent();
            onPositionChanged = positionChangedCallback;
            onColorChanged = colorChangedCallback;

            currentScreen = Screen.FromPoint(new System.Drawing.Point((int)Math.Round(currentX), (int)Math.Round(currentY)));
            var workingArea = currentScreen.WorkingArea;
            SliderX.Minimum = workingArea.Left;
            SliderX.Maximum = workingArea.Right;
            SliderY.Minimum = workingArea.Top;
            SliderY.Maximum = workingArea.Bottom;
            
            PositionX = currentX;
            PositionY = currentY;
            
            SliderX.Value = currentX;
            SliderY.Value = currentY;
            TextX.Text = ((int)currentX).ToString();
            TextY.Text = ((int)currentY).ToString();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isUpdating) return;
            isUpdating = true;
            
            if (sender == SliderX)
            {
                TextX.Text = ((int)SliderX.Value).ToString();
                PositionX = SliderX.Value;
            }
            else if (sender == SliderY)
            {
                TextY.Text = ((int)SliderY.Value).ToString();
                PositionY = SliderY.Value;
            }
            
            onPositionChanged?.Invoke(PositionX, PositionY);
            
            isUpdating = false;
        }

        private void TextX_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (isUpdating) return;
            if (int.TryParse(TextX.Text, out int value))
            {
                isUpdating = true;
                SliderX.Value = Math.Max(0, Math.Min(value, SliderX.Maximum));
                PositionX = SliderX.Value;
                onPositionChanged?.Invoke(PositionX, PositionY);
                isUpdating = false;
            }
        }

        private void TextY_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (isUpdating) return;
            if (int.TryParse(TextY.Text, out int value))
            {
                isUpdating = true;
                SliderY.Value = Math.Max(0, Math.Min(value, SliderY.Maximum));
                PositionY = SliderY.Value;
                onPositionChanged?.Invoke(PositionX, PositionY);
                isUpdating = false;
            }
        }

        private void CenterButton_Click(object sender, RoutedEventArgs e)
        {
            var workingArea = currentScreen.WorkingArea;
            SliderX.Value = workingArea.Left + (workingArea.Width / 2.0);
        }

        private void TopButton_Click(object sender, RoutedEventArgs e)
        {
            var workingArea = currentScreen.WorkingArea;
            SliderX.Value = workingArea.Left + (workingArea.Width / 2.0);
            SliderY.Value = workingArea.Top + 20;
        }

        private void BottomButton_Click(object sender, RoutedEventArgs e)
        {
            var workingArea = currentScreen.WorkingArea;
            SliderX.Value = workingArea.Left + (workingArea.Width / 2.0);
            SliderY.Value = workingArea.Bottom - 120;
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.ColorDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var color = Color.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
                onColorChanged?.Invoke(color);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
