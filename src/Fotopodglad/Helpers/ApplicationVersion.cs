using System.Reflection;

namespace Fotopodglad.Helpers;

internal static class ApplicationVersion
{
    public static string DisplayVersion { get; } = ResolveDisplayVersion(
        typeof(ApplicationVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        typeof(ApplicationVersion).Assembly.GetName().Version);

    public static string ProductName { get; } = $"Fotopodgląd v{DisplayVersion}";

    public static string CreateWindowTitle(string section) => $"{ProductName} — {section}";

    internal static string ResolveDisplayVersion(string? informationalVersion, Version? assemblyVersion)
    {
        var normalizedInformationalVersion = informationalVersion?.Split('+', 2)[0].Trim();
        if (!string.IsNullOrWhiteSpace(normalizedInformationalVersion))
        {
            return normalizedInformationalVersion;
        }

        if (assemblyVersion is null)
        {
            return "1.0.0";
        }

        return assemblyVersion.Build >= 0
            ? assemblyVersion.ToString(3)
            : assemblyVersion.ToString(2);
    }
}
