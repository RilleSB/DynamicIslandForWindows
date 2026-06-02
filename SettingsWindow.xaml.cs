using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace DynamicIslandPC
{
    public partial class SettingsWindow : Window
    {
        public double PositionX { get; private set; }
        public double PositionY { get; private set; }

        private bool isUpdating = false;
        private bool isWindowReady = false;
        private bool isDarkTheme = true;
        private string backgroundColorHex = "#FF000000";
        private double backgroundOpacity = 0.7;
        private bool decorationEnabled = true;
        private string decorationMediaPath = "";

        private readonly AppSettings settings;
        private readonly Action<double, double> onPositionChanged;
        private readonly Action<bool> onThemeChanged;
        private readonly Action<Color, double> onBackgroundChanged;
        private readonly Action<bool, string> onDecorationChanged;
        private readonly Forms.Screen currentScreen;

        public SettingsWindow(
            double currentX,
            double currentY,
            AppSettings currentSettings,
            Action<double, double> positionChangedCallback,
            Action<bool> themeChangedCallback = null,
            Action<Color, double> backgroundChangedCallback = null,
            Action<bool, string> decorationChangedCallback = null)
        {
            InitializeComponent();

            settings = currentSettings ?? new AppSettings();
            onPositionChanged = positionChangedCallback;
            onThemeChanged = themeChangedCallback;
            onBackgroundChanged = backgroundChangedCallback;
            onDecorationChanged = decorationChangedCallback;

            isDarkTheme = settings.IsDarkTheme;
            backgroundColorHex = string.IsNullOrWhiteSpace(settings.BackgroundColor) ? "#FF000000" : settings.BackgroundColor;
            backgroundOpacity = Math.Clamp(settings.BackgroundOpacity, 0.1, 1.0);
            decorationEnabled = settings.DecorationEnabled;
            decorationMediaPath = settings.DecorationMediaPath ?? string.Empty;

            currentScreen = Forms.Screen.FromPoint(new System.Drawing.Point((int)Math.Round(currentX), (int)Math.Round(currentY)));
            var workingArea = currentScreen.WorkingArea;

            isUpdating = true;
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

            BackgroundOpacitySlider.Value = backgroundOpacity * 100.0;
            DecorationEnabledCheckBox.IsChecked = decorationEnabled;
            isUpdating = false;

            UpdateThemeButtons();
            SyncColorControlsFromBackground();
            UpdateDecorationUi();
            isWindowReady = true;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isWindowReady || isUpdating) return;
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
            if (!isWindowReady || isUpdating) return;
            if (int.TryParse(TextX.Text, out int value))
            {
                isUpdating = true;
                SliderX.Value = Math.Max(SliderX.Minimum, Math.Min(value, SliderX.Maximum));
                PositionX = SliderX.Value;
                onPositionChanged?.Invoke(PositionX, PositionY);
                isUpdating = false;
            }
        }

        private void TextY_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!isWindowReady || isUpdating) return;
            if (int.TryParse(TextY.Text, out int value))
            {
                isUpdating = true;
                SliderY.Value = Math.Max(SliderY.Minimum, Math.Min(value, SliderY.Maximum));
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

        private void DarkThemeButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyThemeChoice(true);
        }

        private void LightThemeButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyThemeChoice(false);
        }

        private void ApplyThemeChoice(bool dark)
        {
            if (!isWindowReady || isUpdating) return;

            isDarkTheme = dark;
            settings.IsDarkTheme = dark;
            backgroundColorHex = (dark ? Colors.Black : Colors.White).ToString();
            settings.BackgroundColor = backgroundColorHex;
            UpdateThemeButtons();
            SyncColorControlsFromBackground();
            onThemeChanged?.Invoke(dark);
        }

        private void ColorPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isWindowReady || isUpdating) return;
            if (sender is not System.Windows.Controls.Button button || button.Tag is not string hex)
                return;

            SetBackgroundColorFromString(hex, notify: true);
        }

        private void BackgroundHexText_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!isWindowReady || isUpdating) return;

            var text = BackgroundHexText.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (!text.StartsWith("#", StringComparison.Ordinal))
                text = "#" + text;

            if (text.Length != 7 && text.Length != 9)
                return;

            SetBackgroundColorFromString(text, notify: true);
        }

        private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isWindowReady || isUpdating) return;

            var color = Color.FromRgb(
                (byte)Math.Round(RedSlider.Value),
                (byte)Math.Round(GreenSlider.Value),
                (byte)Math.Round(BlueSlider.Value));
            SetBackgroundColor(color, notify: true);
        }

        private void BackgroundOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isWindowReady || isUpdating) return;

            backgroundOpacity = Math.Clamp(BackgroundOpacitySlider.Value / 100.0, 0.1, 1.0);
            settings.BackgroundOpacity = backgroundOpacity;
            UpdateBackgroundUi();
            NotifyBackgroundChanged();
        }

        private void NotifyBackgroundChanged()
        {
            onBackgroundChanged?.Invoke(GetBackgroundColor(), backgroundOpacity);
        }

        private Color GetBackgroundColor()
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(backgroundColorHex);
            }
            catch
            {
                return Colors.Black;
            }
        }

        private void SetBackgroundColorFromString(string colorText, bool notify)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorText);
                if (color.A == 0)
                    color.A = 255;
                SetBackgroundColor(color, notify);
            }
            catch
            {
                // Keep typing forgiving: invalid partial HEX should not fight the user.
            }
        }

        private void SetBackgroundColor(Color color, bool notify)
        {
            backgroundColorHex = color.ToString();
            settings.BackgroundColor = backgroundColorHex;
            SyncColorControlsFromBackground();
            if (notify)
                NotifyBackgroundChanged();
        }

        private void SyncColorControlsFromBackground()
        {
            var color = GetBackgroundColor();
            isUpdating = true;
            RedSlider.Value = color.R;
            GreenSlider.Value = color.G;
            BlueSlider.Value = color.B;
            BackgroundHexText.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            isUpdating = false;
            UpdateBackgroundUi();
        }

        private void UpdateBackgroundUi()
        {
            var color = GetBackgroundColor();
            BackgroundColorPreview.Background = new SolidColorBrush(color);
            BackgroundColorPreview.Opacity = backgroundOpacity;
            BackgroundOpacityText.Text = $"{(int)Math.Round(backgroundOpacity * 100)}%";
            RedValueText.Text = color.R.ToString();
            GreenValueText.Text = color.G.ToString();
            BlueValueText.Text = color.B.ToString();
        }

        private void UpdateThemeButtons()
        {
            SetThemeButtonState(DarkThemeButton, isDarkTheme);
            SetThemeButtonState(LightThemeButton, !isDarkTheme);
        }

        private static void SetThemeButtonState(System.Windows.Controls.Button button, bool selected)
        {
            button.Background = selected
                ? new SolidColorBrush(Color.FromRgb(155, 89, 182))
                : new SolidColorBrush(Color.FromRgb(58, 58, 64));
            button.BorderBrush = selected
                ? new SolidColorBrush(Color.FromRgb(185, 124, 210))
                : new SolidColorBrush(Color.FromRgb(85, 85, 96));
        }

        private void DecorationEnabledChanged(object sender, RoutedEventArgs e)
        {
            if (!isWindowReady || isUpdating) return;

            decorationEnabled = DecorationEnabledCheckBox.IsChecked == true;
            settings.DecorationEnabled = decorationEnabled;
            NotifyDecorationChanged();
            UpdateDecorationUi();
        }

        private void DecorationChooseButton_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new Forms.OpenFileDialog
            {
                Title = "Выбери декор рядом с island",
                Filter = "Поддерживаемые файлы|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.m4v;*.mov;*.wmv;*.avi;*.webm;*.mkv|Картинки и GIF|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Видео|*.mp4;*.m4v;*.mov;*.wmv;*.avi;*.webm;*.mkv|Все файлы|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != Forms.DialogResult.OK)
                return;

            if (!IsSupportedDecorationPath(dialog.FileName))
            {
                MessageBox.Show("Этот формат пока не поддерживается для декора.", "Dynamic Island PC", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            decorationMediaPath = dialog.FileName;
            decorationEnabled = true;
            settings.DecorationMediaPath = decorationMediaPath;
            settings.DecorationEnabled = true;

            isUpdating = true;
            DecorationEnabledCheckBox.IsChecked = true;
            isUpdating = false;

            NotifyDecorationChanged();
            UpdateDecorationUi();
        }

        private void DecorationResetButton_Click(object sender, RoutedEventArgs e)
        {
            decorationMediaPath = string.Empty;
            decorationEnabled = true;
            settings.DecorationMediaPath = string.Empty;
            settings.DecorationEnabled = true;

            isUpdating = true;
            DecorationEnabledCheckBox.IsChecked = true;
            isUpdating = false;

            NotifyDecorationChanged();
            UpdateDecorationUi();
        }

        private void NotifyDecorationChanged()
        {
            onDecorationChanged?.Invoke(decorationEnabled, decorationMediaPath);
        }

        private void UpdateDecorationUi()
        {
            DecorationPathText.Text = string.IsNullOrWhiteSpace(decorationMediaPath)
                ? "flex.gif"
                : decorationMediaPath;
            DecorationPathText.ToolTip = DecorationPathText.Text;
            DecorationInfoText.Text = GetDecorationInfoText();
        }

        private string GetDecorationInfoText()
        {
            if (!decorationEnabled)
                return "Декор выключен. Файл сохранится, но рядом с island ничего не будет.";

            if (string.IsNullOrWhiteSpace(decorationMediaPath))
                return "Сейчас используется стандартный flex.gif.";

            if (!File.Exists(decorationMediaPath))
                return "Файл не найден, поэтому будет использован стандартный flex.gif.";

            return IsVideoDecorationPath(decorationMediaPath)
                ? "Выбрано видео. Оно будет зацикливаться без звука, пока музыка играет."
                : "Выбрана картинка или GIF. Она будет показываться рядом, пока музыка играет.";
        }

        private static bool IsSupportedDecorationPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif"
                or ".mp4" or ".m4v" or ".mov" or ".wmv" or ".avi" or ".webm" or ".mkv";
        }

        private static bool IsVideoDecorationPath(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".mp4" or ".m4v" or ".mov" or ".wmv" or ".avi" or ".webm" or ".mkv";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
