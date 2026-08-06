using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Fotopodglad.Configuration;

namespace Fotopodglad.Services;

public static class LocalizationService
{
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["Ustawienia"] = "Settings", ["Diagnostyka"] = "Diagnostics", ["O aplikacji"] = "About",
        ["Ekrany i okna"] = "Displays and windows", ["Tryb widoków"] = "View mode", ["Układ monitorów"] = "Display layout",
        ["Okno A — podgląd"] = "Window A — preview", ["Okno B — siatka"] = "Window B — gallery",
        ["Folder zdjęć"] = "Photo folder", ["Wybierz…"] = "Browse…", ["Galeria i podgląd"] = "Gallery and preview",
        ["Pokazuj nazwę i parametry EXIF"] = "Show filename and EXIF details",
        ["Oznaczaj przepalenia na czerwono i niedoświetlenia na niebiesko"] = "Mark clipped highlights red and shadows blue",
        ["Kolumny siatki"] = "Grid columns", ["Czas podglądu (s)"] = "Preview hold time (s)", ["Histogram"] = "Histogram",
        ["Filtr zdjęć"] = "Photo filter", ["Data od"] = "From", ["Data do"] = "To",
        ["Limit cache miniaturek (MB)"] = "Thumbnail cache limit (MB)", ["Rozmiar tekstu EXIF"] = "EXIF text size",
        ["Hotspot i pobieranie"] = "Hotspot and downloads", ["Automatycznie uruchamiaj hotspot i QR"] = "Start hotspot and QR automatically",
        ["Pokazuj instrukcję pod siatką"] = "Show instructions below the gallery", ["Nazwa Wi‑Fi (puste = losowa)"] = "Wi‑Fi name (blank = random)",
        ["Hasło (puste = losowe)"] = "Password (blank = random)", ["Wielkość QR"] = "QR size",
        ["Jednoczesne pobrania (1–20)"] = "Concurrent downloads (1–20)", ["Rozmiar instrukcji"] = "Instruction text size",
        ["Automatycznie sprawdzaj aktualizacje GitHub"] = "Automatically check GitHub for updates",
        ["Wygląd i język"] = "Appearance and language", ["Motyw"] = "Theme", ["Skala interfejsu"] = "UI scale", ["Język"] = "Language",
        ["Hotspot"] = "Hotspot", ["Stan"] = "Status", ["Adres IP"] = "IP address", ["Próba naprawy"] = "Repair attempt",
        ["Aktywne pobrania"] = "Active downloads", ["Ostatnia zmiana"] = "Last change", ["Folder"] = "Folder",
        ["Aktualizacje"] = "Updates", ["Sprawdź teraz"] = "Check now", ["Przywróć domyślne"] = "Restore defaults",
        ["Anuluj"] = "Cancel", ["Zapisz"] = "Save", ["Skróty klawiszowe"] = "Keyboard shortcuts",
        ["Jak działa udostępnianie"] = "How sharing works", ["Oczekiwanie na zdjęcia…"] = "Waiting for photos…",
        ["Dołącz do sieci"] = "Join the network", ["Pobierz wyświetlane zdjęcie"] = "Download displayed photo",
        ["Konfiguracja prezentacji, galerii i udostępniania"] = "Presentation, gallery and sharing configuration",
        ["F11 przełącza tryb pełnoekranowy i okienkowy. Pozycje okien są zapamiętywane."] = "F11 switches between fullscreen and windowed mode. Window positions are remembered.",
        ["Po utracie dysku aplikacja pokaże ostrzeżenie i automatycznie wznowi obserwowanie po jego powrocie."] = "If the drive disappears, the app shows a warning and resumes watching automatically when it returns.",
        ["Prawy przycisk myszy na miniaturze oznacza lub odznacza zdjęcie."] = "Right-click a thumbnail to flag or unflag it.",
        ["Oryginalny plik jest wysyłany bez rekompresji i utraty jakości."] = "The original file is sent without recompression or quality loss.",
        ["Podgląd, selekcja i bezstratne udostępnianie zdjęć podczas sesji."] = "Preview, select and share original photos during a session.",
        ["Obsługiwane są JPEG, PNG, TIFF, HEIC/HEIF oraz popularne formaty RAW: ARW, NEF/NRW, CR2/CR3, RAF, DNG, RW2, ORF i PEF. Dostępność konkretnego kodeka zależy od pliku i systemu."] = "Supported formats include JPEG, PNG, TIFF, HEIC/HEIF and popular RAW files: ARW, NEF/NRW, CR2/CR3, RAF, DNG, RW2, ORF and PEF. Decoding depends on the specific file and codec.",
        ["Pierwszy QR łączy telefon z hotspotem, drugi pobiera dokładnie zdjęcie widoczne w podglądzie. Po pięciu minutach bez pobierania aplikacja zmienia hasło sesji. Liczba równoległych pobrań jest ograniczona, a pliki wysyłane są w oryginalnej jakości."] = "The first QR joins the phone to the hotspot; the second downloads the photo currently shown in the preview. After five minutes without a download the session password is rotated. Concurrent downloads are limited and files retain their original quality.",
        ["1. Zeskanuj „Dołącz do sieci”"] = "1. Scan “Join the network”",
        ["  •  2. Kliknij zdjęcie, które chcesz pobrać"] = "  •  2. Select the photo you want to download",
        ["  •  3. Zeskanuj „Pobierz wyświetlane zdjęcie” — możesz zapisać je na telefonie"] = "  •  3. Scan “Download displayed photo” and save it to your phone"
    };
    private static readonly IReadOnlyDictionary<string, string> Polish =
        English.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    public static void Apply(DependencyObject root, AppSettings settings)
    {
        Walk(root, AppearanceService.UseEnglish(settings));
    }

    private static void Walk(DependencyObject element, bool useEnglish)
    {
        var translations = useEnglish ? English : Polish;
        switch (element)
        {
            case TextBlock text when translations.TryGetValue(text.Text, out var translated): text.Text = translated; break;
            case ContentControl content when content.Content is string value && translations.TryGetValue(value, out var translated): content.Content = translated; break;
            case HeaderedContentControl header when header.Header is string value && translations.TryGetValue(value, out var translated): header.Header = translated; break;
            case Run run when translations.TryGetValue(run.Text, out var translated): run.Text = translated; break;
        }
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>()) Walk(child, useEnglish);
    }
}
