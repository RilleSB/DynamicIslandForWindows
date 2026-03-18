using System.Threading;
using System.Windows;

namespace DynamicIslandPC
{
    public partial class App : Application
    {
        private Mutex _singleInstanceMutex;
        private bool _ownsMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstanceMutex = new Mutex(true, "DynamicIslandPC.SingleInstance", out bool createdNew);
            _ownsMutex = createdNew;
            if (!createdNew)
            {
                MessageBox.Show("Dynamic Island PC уже запущен!", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_ownsMutex)
                _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
