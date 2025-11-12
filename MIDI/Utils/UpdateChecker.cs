using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MIDI.Utils
{
    public class UpdateChecker
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/routersys/YMM4-MIDI/releases";
        private const string ManjuboxApiUrl = "https://manjubox.net/api/ymm4plugins/github/detail/routersys/YMM4-MIDI";

        public static async Task<List<GitHubRelease>> GetAllReleasesAsync()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return new List<GitHubRelease>();
            }

            List<GitHubRelease>? releases = null;

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "YMMIDI_CHECKER");
                releases = await client.GetFromJsonAsync<List<GitHubRelease>>(GitHubApiUrl);
            }
            catch (Exception)
            {
                releases = null;
            }

            if (releases == null || releases.Count == 0)
            {
                try
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", "YMMIDI_CHECKER");
                    var manjuboxResponse = await client.GetFromJsonAsync<ManjuboxApiResponse>(ManjuboxApiUrl);
                    if (manjuboxResponse?.Releases != null)
                    {
                        releases = manjuboxResponse.Releases
                            .Where(r => !r.Prerelease)
                            .Select(r => new GitHubRelease { TagName = r.TagName, Body = r.Body })
                            .ToList();
                    }
                }
                catch (Exception)
                {
                    releases = null;
                }
            }

            return releases?.Where(r => !r.Prerelease).ToList() ?? new List<GitHubRelease>();
        }

        public static string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{version?.Major ?? 3}.{version?.Minor ?? 0}.{version?.Build ?? 0}";
        }

        public static int CompareVersions(string current, string latest)
        {
            try
            {
                var currentVersion = new Version(current);
                var latestVersion = new Version(latest.TrimStart('v'));
                return currentVersion.CompareTo(latestVersion);
            }
            catch
            {
                return 0;
            }
        }
    }

    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }

    public class ManjuboxApiResponse
    {
        [JsonPropertyName("releases")]
        public List<ManjuboxRelease>? Releases { get; set; }
    }

    public class ManjuboxRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }
}