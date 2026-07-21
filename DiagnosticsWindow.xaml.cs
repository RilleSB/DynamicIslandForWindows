using System;
using System.Windows;

namespace DynamicIslandPC
{
    public partial class DiagnosticsWindow : Window
    {
        private readonly Func<string> diagnosticsProvider;

        public DiagnosticsWindow(Func<string> diagnosticsProvider)
        {
            InitializeComponent();
            this.diagnosticsProvider = diagnosticsProvider;
            RefreshDiagnostics();
        }

        private void RefreshDiagnostics()
        {
            DiagnosticsTextBox.Text = diagnosticsProvider?.Invoke() ?? "Diagnostics are not available.";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshDiagnostics();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
