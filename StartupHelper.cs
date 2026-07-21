using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace DynamicIslandPC
{
    internal static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "DynamicIslandPC";

        public static void SetEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null)
                return;

            if (enabled)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(exePath))
                    key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(AppName) != null;
        }
    }
}
