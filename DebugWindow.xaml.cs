using System.Windows;

namespace DynamicIslandPC
{
    public partial class DebugWindow : Window
    {
        public DebugWindow()
        {
            InitializeComponent();
        }

        public void AddDebugInfo(string info)
        {
            Dispatcher.Invoke(() =>
            {
                DebugText.Text += info + "\n";
                DebugText.ScrollToEnd();
            });
        }

        public void ClearDebugInfo()
        {
            Dispatcher.Invoke(() =>
            {
                DebugText.Text = "";
            });
        }
    }
}