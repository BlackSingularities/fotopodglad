using System.IO;
using System.Text.Json;

namespace Fotopodglad.Configuration;

public sealed class AppSettings
{
    public string? WatchedFolderPath { get; set; }

    /// <summary>Puste/null = losowe SSID generowane przy każdym starcie hotspotu.</summary>
    public string? WifiSsid { get; set; }

    /// <summary>Puste/null = losowe hasło generowane przy każdym starcie hotspotu.</summary>
    public string? WifiPassphrase { get; set; }

    public int GridColumnCount { get; set; } = 6;

    /// <summary>Rozmiar boku każdego kodu QR w pikselach interfejsu (DIP).</summary>
    public int QrCodeSize { get; set; } = 160;

    /// <summary>Ile sekund minimum trzymać ręcznie wybrane zdjęcie (Okno B), zanim wróci "zawsze najnowsze".</summary>
    public double ManualHoldSeconds { get; set; } = 10;

    /// <summary>Indeks ekranu (z IScreenService.GetScreens()) dla Okna A — podgląd najnowszego zdjęcia.</summary>
    public int MainViewScreenIndex { get; set; } = 0;

    /// <summary>Indeks ekranu dla Okna B — siatka zdjęć.</summary>
    public int GridScreenIndex { get; set; } = 1;

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
