using System.Windows;

namespace DynamicIslandPC
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Обеспечиваем запуск только одного экземпляра
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
            
            if (processes.Length > 1)
            {
                MessageBox.Show("Dynamic Island PC уже запущен!", "Информация", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
        }
    }
}