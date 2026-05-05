using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Ramifier.Services;

public class UpdateService
{
    private const string ReleasesUrl = "https://api.github.com/repos/BowedYoutub3r/Ramifier/releases/latest";

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public string? LatestVersionTag { get; private set; }
    public string? DownloadUrl { get; private set; }

    public async Task<bool> CheckForUpdateAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Ramifier");
            http.Timeout = TimeSpan.FromSeconds(15);

            var release = await http.GetFromJsonAsync<GitHubRelease>(ReleasesUrl);
            if (release?.TagName == null) return false;

            var tagVersion = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(tagVersion, out var latest)) return false;

            // Compare major.minor.build only (ignore revision)
            var current = new Version(CurrentVersion.Major, CurrentVersion.Minor, Math.Max(CurrentVersion.Build, 0));
            latest = new Version(latest.Major, latest.Minor, Math.Max(latest.Build, 0));

            if (latest > current)
            {
                LatestVersionTag = release.TagName;
                DownloadUrl = release.HtmlUrl;
                return true;
            }
        }
        catch { }

        return false;
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }
    }
}
