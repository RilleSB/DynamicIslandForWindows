using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DynamicIslandPC
{
    public partial class App : Application
    {
        private Mutex _singleInstanceMutex;
        private bool _ownsMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            SetupGlobalExceptionHandling();
            RefreshRateHelper.InitializeHighRefreshRate();
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

        private void SetupGlobalExceptionHandling()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                Logger.Error("Unhandled Dispatcher Exception", e.Exception);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    Logger.Error("Unhandled AppDomain Exception", ex);
                }
                else
                {
                    Logger.Error($"Unhandled AppDomain non-exception: {e.ExceptionObject}");
                }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Logger.Error("Unobserved Task Exception", e.Exception);
                e.SetObserved();
            };
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
