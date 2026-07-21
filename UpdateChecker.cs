using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DynamicIslandPC
{
    internal sealed class UpdateInfo
    {
        public bool HasUpdate { get; init; }
        public string CurrentVersion { get; init; }
        public string LatestVersion { get; init; }
        public string ReleaseUrl { get; init; }
    }

    internal static class UpdateChecker
    {
        public const string CurrentVersion = "V2.5";
        private const string LatestReleaseUrl = "https://api.github.com/repos/RilleSB/DynamicIslandForWindows/releases/latest";

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

            return new UpdateInfo
            {
                CurrentVersion = CurrentVersion,
                LatestVersion = tag,
                ReleaseUrl = url,
                HasUpdate = CompareVersions(tag, CurrentVersion) > 0
            };
        }

        public static void OpenRelease(UpdateInfo info)
        {
            if (string.IsNullOrWhiteSpace(info?.ReleaseUrl))
                return;

            Process.Start(new ProcessStartInfo(info.ReleaseUrl) { UseShellExecute = true });
        }

        private static int CompareVersions(string left, string right)
        {
            var leftVersion = NormalizeVersion(left);
            var rightVersion = NormalizeVersion(right);
            return leftVersion.CompareTo(rightVersion);
        }

        private static Version NormalizeVersion(string value)
        {
            var normalized = (value ?? string.Empty).Trim().TrimStart('v', 'V');
            return Version.TryParse(normalized, out var version) ? version : new Version(0, 0);
        }
    }
}
