namespace Fotopodglad.Models;

public sealed record UpdateCheckResult(bool IsUpdateAvailable, string LatestVersion, string ReleaseUrl, string DownloadUrl);
