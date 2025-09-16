using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MIDI
{
    public class UpdateChecker
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/routersys/YMM4-MIDI/releases/latest";

        public static async Task<(string CurrentVersion, string? LatestVersion, bool IsNewerAvailable)> CheckForUpdatesAsync()
        {
            var currentVersion = GetCurrentVersion();
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return (currentVersion, null, false);
            }

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "YMM4-MIDI-Update-Checker");

                var latestRelease = await client.GetFromJsonAsync<GitHubRelease>(GitHubApiUrl);
                if (latestRelease == null || string.IsNullOrEmpty(latestRelease.TagName))
                {
                    return (currentVersion, null, false);
                }

                var latestVersion = latestRelease.TagName.TrimStart('v');
                var isNewerAvailable = CompareVersions(currentVersion, latestVersion) < 0;

                return (currentVersion, latestVersion, isNewerAvailable);
            }
            catch (Exception)
            {
                return (currentVersion, null, false);
            }
        }

        private static string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{version?.Major ?? 2}.{version?.Minor ?? 3}.{version?.Build ?? 0}";
        }

        private static int CompareVersions(string current, string latest)
        {
            var currentVersion = new Version(current);
            var latestVersion = new Version(latest);
            return currentVersion.CompareTo(latestVersion);
        }
    }

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
    }
}