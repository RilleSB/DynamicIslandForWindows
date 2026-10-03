using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DynamicIslandPC
{
    public sealed class UpdateInfo
    {
        public bool HasUpdate { get; init; }
        public string CurrentVersion { get; init; }
        public string LatestVersion { get; init; }
        public string ReleaseUrl { get; init; }
        public string ExeDownloadUrl { get; init; }
    }

    public static class UpdateChecker
    {
        public static string CurrentVersion => GetCurrentVersion();
        private const string LatestReleaseUrl = "https://api.github.com/repos/RilleSB/DynamicIslandForWindows/releases/latest";

        private static string GetCurrentVersion()
        {
            var ver = typeof(UpdateChecker).Assembly.GetName().Version;
            if (ver != null && ver.Major > 0)
            {
                return ver.Build > 0 ? $"V{ver.Major}.{ver.Minor}.{ver.Build}" : $"V{ver.Major}.{ver.Minor}";
            }
            return "V3.0";
        }

        public static async Task<UpdateInfo> CheckAsync()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIslandPC");

            using var response = await client.GetAsync(LatestReleaseUrl);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var root = document.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? CurrentVersion;
            var url = root.GetProperty("html_url").GetString() ?? "https://github.com/RilleSB/DynamicIslandForWindows/releases";

            string exeUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var nameProp) &&
                        string.Equals(nameProp.GetString(), "DynamicIslandPC.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        if (asset.TryGetProperty("browser_download_url", out var dlProp))
                        {
                            exeUrl = dlProp.GetString();
                            break;
                        }
                    }
                }
            }

            return new UpdateInfo
            {
                CurrentVersion = CurrentVersion,
                LatestVersion = tag,
                ReleaseUrl = url,
                ExeDownloadUrl = exeUrl,
                HasUpdate = CompareVersions(tag, CurrentVersion) > 0
            };
        }

        public static void OpenRelease(UpdateInfo info)
        {
            if (string.IsNullOrWhiteSpace(info?.ReleaseUrl))
                return;

            Process.Start(new ProcessStartInfo(info.ReleaseUrl) { UseShellExecute = true });
        }

        public static async Task DownloadAndInstallAsync(UpdateInfo info, IProgress<int> progress = null)
        {
            if (string.IsNullOrWhiteSpace(info.ExeDownloadUrl))
                throw new InvalidOperationException("Direct executable download URL not found in release assets.");

            var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
                throw new FileNotFoundException("Cannot determine current application path.");

            var tempExePath = Path.Combine(Path.GetTempPath(), $"DynamicIslandPC_Update_{Guid.NewGuid():N}.exe");

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIslandPC");
                using var response = await client.GetAsync(info.ExeDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var remoteStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempExePath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await remoteStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    if (totalBytes > 0 && progress != null)
                    {
                        progress.Report((int)((totalRead * 100) / totalBytes));
                    }
                }
            }

            var pid = Process.GetCurrentProcess().Id;
            var scriptPath = Path.Combine(Path.GetTempPath(), $"update_island_{Guid.NewGuid():N}.bat");

            var scriptContent = $@"@echo off
:wait
timeout /t 1 /nobreak >nul
tasklist /fi ""PID eq {pid}"" | find ""{pid}"" >nul
if %ERRORLEVEL% equ 0 goto wait

copy /y ""{tempExePath}"" ""{currentExe}"" >nul
del ""{tempExePath}"" >nul
start """" ""{currentExe}""
del ""%~f0""
";
            File.WriteAllText(scriptPath, scriptContent, System.Text.Encoding.Default);

            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(startInfo);
            System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
        }

        private static int CompareVersions(string left, string right)
        {
            var leftVersion = NormalizeVersion(left);
            var rightVersion = NormalizeVersion(right);
            return leftVersion.CompareTo(rightVersion);
        }

        private static Version NormalizeVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new Version(0, 0);

            var normalized = value.Trim().TrimStart('v', 'V');
            var match = System.Text.RegularExpressions.Regex.Match(normalized, @"^\d+(\.\d+)+");
            if (match.Success && Version.TryParse(match.Value, out var parsed))
            {
                return parsed;
            }

            return Version.TryParse(normalized, out var version) ? version : new Version(0, 0);
        }
    }
}
