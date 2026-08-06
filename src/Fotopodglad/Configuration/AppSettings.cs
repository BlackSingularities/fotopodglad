using System.IO;
using System.Text.Json;
using Fotopodglad.Models;

namespace Fotopodglad.Configuration;

public sealed class AppSettings
{
    public const double MinManualHoldSeconds = 3;
    public const double MaxManualHoldSeconds = 900;

    public string? WatchedFolderPath { get; set; }

    /// <summary>Domyślnie true: hotspot i kody QR uruchamiają się automatycznie wraz z aplikacją.</summary>
    public bool GuestAccessEnabled { get; set; } = true;

    /// <summary>Czy pod siatką pokazywać krótką instrukcję łączenia i pobierania zdjęć dla gości.</summary>
    public bool ShowGuestInstructions { get; set; } = true;

    /// <summary>Puste/null = losowe SSID generowane przy każdym starcie hotspotu.</summary>
    public string? WifiSsid { get; set; }

    /// <summary>Puste/null = losowe hasło generowane przy każdym starcie hotspotu.</summary>
    public string? WifiPassphrase { get; set; }

    public int GridColumnCount { get; set; } = 6;

    /// <summary>Czy na podglądzie zdjęcia wyświetlać panel z nazwą pliku i parametrami EXIF.</summary>
    public bool ShowPhotoParameters { get; set; } = true;

    /// <summary>Rozmiar boku każdego kodu QR w pikselach interfejsu (DIP).</summary>
    public int QrCodeSize { get; set; } = 160;

    /// <summary>Ile sekund minimum trzymać ręcznie wybrane zdjęcie (Okno B), zanim wróci "zawsze najnowsze".</summary>
    public double ManualHoldSeconds { get; set; } = 10;

    /// <summary>Indeks ekranu (z IScreenService.GetScreens()) dla Okna A — podgląd najnowszego zdjęcia.</summary>
    public int MainViewScreenIndex { get; set; } = 0;

    /// <summary>Indeks ekranu dla Okna B — siatka zdjęć.</summary>
    public int GridScreenIndex { get; set; } = 1;

    public WindowPresentationMode WindowMode { get; set; } = WindowPresentationMode.Fullscreen;
    public SavedWindowPlacement MainWindowPlacement { get; set; } = new();
    public SavedWindowPlacement GridWindowPlacement { get; set; } = new();

    public PhotoFilterMode PhotoFilter { get; set; } = PhotoFilterMode.All;
    public DateTime? FilterDateFrom { get; set; }
    public DateTime? FilterDateTo { get; set; }
    public List<string> FlaggedPhotoPaths { get; set; } = [];

    public HistogramMode Histogram { get; set; } = HistogramMode.Off;
    public bool ShowClippingWarnings { get; set; }
    public int MaxConcurrentDownloads { get; set; } = 3;
    public int ThumbnailCacheMegabytes { get; set; } = 256;

    public double ExifTextSize { get; set; } = 15;
    public double InstructionTextSize { get; set; } = 11;
    public ThemeMode Theme { get; set; } = ThemeMode.Automatic;
    public UiScaleMode UiScale { get; set; } = UiScaleMode.Normal;
    public LanguageMode Language { get; set; } = LanguageMode.Automatic;
    public bool CheckForUpdates { get; set; } = true;
    public DateTime? LastUpdateCheckUtc { get; set; }

    public event EventHandler? Changed;

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public AppSettings Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void ResetToDefaults()
    {
        var defaults = new AppSettings { WatchedFolderPath = WatchedFolderPath };
        CopyFrom(defaults);
    }

    public void CopyFrom(AppSettings source)
    {
        WatchedFolderPath = source.WatchedFolderPath;
        GuestAccessEnabled = source.GuestAccessEnabled;
        ShowGuestInstructions = source.ShowGuestInstructions;
        WifiSsid = source.WifiSsid;
        WifiPassphrase = source.WifiPassphrase;
        GridColumnCount = source.GridColumnCount;
        ShowPhotoParameters = source.ShowPhotoParameters;
        QrCodeSize = source.QrCodeSize;
        ManualHoldSeconds = source.ManualHoldSeconds;
        MainViewScreenIndex = source.MainViewScreenIndex;
        GridScreenIndex = source.GridScreenIndex;
        WindowMode = source.WindowMode;
        MainWindowPlacement = source.MainWindowPlacement;
        GridWindowPlacement = source.GridWindowPlacement;
        PhotoFilter = source.PhotoFilter;
        FilterDateFrom = source.FilterDateFrom;
        FilterDateTo = source.FilterDateTo;
        FlaggedPhotoPaths = [.. source.FlaggedPhotoPaths];
        Histogram = source.Histogram;
        ShowClippingWarnings = source.ShowClippingWarnings;
        MaxConcurrentDownloads = source.MaxConcurrentDownloads;
        ThumbnailCacheMegabytes = source.ThumbnailCacheMegabytes;
        ExifTextSize = source.ExifTextSize;
        InstructionTextSize = source.InstructionTextSize;
        Theme = source.Theme;
        UiScale = source.UiScale;
        Language = source.Language;
        CheckForUpdates = source.CheckForUpdates;
        LastUpdateCheckUtc = source.LastUpdateCheckUtc;
    }

    public static bool RequiresRestart(AppSettings before, AppSettings after) =>
        !string.Equals(before.WatchedFolderPath, after.WatchedFolderPath, StringComparison.OrdinalIgnoreCase) ||
        before.GuestAccessEnabled != after.GuestAccessEnabled ||
        !string.Equals(before.WifiSsid, after.WifiSsid, StringComparison.Ordinal) ||
        !string.Equals(before.WifiPassphrase, after.WifiPassphrase, StringComparison.Ordinal) ||
        before.MainViewScreenIndex != after.MainViewScreenIndex ||
        before.GridScreenIndex != after.GridScreenIndex;

    private static string SettingsFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fotopodglad", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Uszkodzony lub niedostępny plik ustawień — wracamy do stanu domyślnego.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Brak możliwości zapisu ustawień nie powinien blokować działania aplikacji.
        }
    }
}
