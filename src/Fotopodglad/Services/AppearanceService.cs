using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Fotopodglad.Configuration;
using Fotopodglad.Models;
using Microsoft.Win32;

namespace Fotopodglad.Services;

public static class AppearanceService
{
    private const string ThemeMarker = "Fotopodglad.ActiveTheme";

    public static void Apply(AppSettings settings)
    {
        if (Application.Current is null) return;
        var useLight = settings.Theme == ThemeMode.Light ||
            settings.Theme == ThemeMode.Automatic && IsWindowsLightTheme();
        var old = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(dictionary => dictionary.Contains(ThemeMarker));
        if (old is not null) Application.Current.Resources.MergedDictionaries.Remove(old);
        var theme = new ResourceDictionary
        {
            Source = new Uri(useLight ? "Resources/Colors.Light.xaml" : "Resources/Colors.xaml", UriKind.Relative)
        };
        theme[ThemeMarker] = true;
        Application.Current.Resources.MergedDictionaries.Add(theme);
        ApplyScale(settings);
    }

    public static void ApplyScale(AppSettings settings)
    {
        var scale = ScaleFactor(settings);
        foreach (Window window in Application.Current.Windows)
        {
            window.FontSize = 12 * scale;
            window.Resources["UiScaleFactor"] = scale;
        }
    }

    public static double ScaleFactor(AppSettings settings) =>
        settings.UiScale switch { UiScaleMode.Small => 0.9, UiScaleMode.Large => 1.25, _ => 1.0 };

    private static bool IsWindowsLightTheme()
    {
        try
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 0) is int value && value != 0;
        }
        catch { return false; }
    }

    public static bool UseEnglish(AppSettings settings) => settings.Language == LanguageMode.English ||
        settings.Language == LanguageMode.Automatic && !CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("pl", StringComparison.OrdinalIgnoreCase);
}
