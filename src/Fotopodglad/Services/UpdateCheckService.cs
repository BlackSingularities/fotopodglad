using System.Net.Http;
using System.Text.Json;
using Fotopodglad.Helpers;
using Fotopodglad.Models;

namespace Fotopodglad.Services;

public sealed class UpdateCheckService
{
    private static readonly Uri LatestReleaseApi =
        new("https://api.github.com/repos/BlackSingularities/fotopodglad/releases/latest");

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public UpdateCheckService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Fotopodglad-UpdateChecker/2.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(LatestReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? "0.0.0";
        var releaseUrl = root.GetProperty("html_url").GetString() ?? string.Empty;
        var downloadUrl = releaseUrl;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (string.Equals(asset.GetProperty("name").GetString(), "Fotopodglad.exe", StringComparison.OrdinalIgnoreCase))
            {
                downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? releaseUrl;
                break;
            }
        }

        var current = ParseVersion(ApplicationVersion.DisplayVersion);
        var latest = ParseVersion(tag);
        return new UpdateCheckResult(latest > current, tag, releaseUrl, downloadUrl);
    }

    private static Version ParseVersion(string value)
    {
        var stable = value.Split('-', '+')[0];
        return Version.TryParse(stable, out var version) ? version : new Version(0, 0, 0);
    }
}
