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
        ["Okno A — podgląd"] = "Window A — preview", ["Okno B — siatka"] = "Window B — grid",
        ["Folder zdjęć"] = "Photo folder", ["Wybierz…"] = "Browse…", ["Galeria i podgląd"] = "Gallery and preview",
        ["Automatycznie pokazuj najnowsze zdjęcie"] = "Automatically show the latest photo",
        ["Po wybraniu zdjęcia z galerii automatyczny podgląd wznowi się po ustawionym czasie. Gdy opcja jest wyłączona, wybrane zdjęcie pozostaje na ekranie."] = "After selecting a gallery photo, automatic preview resumes after the configured time. When this option is off, the selected photo remains on screen.",
        ["Pokazuj nazwę i parametry EXIF"] = "Show filename and EXIF details",
        ["Oznaczaj przepalenia na czerwono i niedoświetlenia na niebiesko"] = "Mark clipped highlights red and shadows blue",
        ["Kolumny siatki"] = "Grid columns", ["Czas wybranego zdjęcia (s)"] = "Selected photo time (s)", ["Histogram"] = "Histogram",
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
        ["Fotograficzne centrum podglądu, selekcji i lokalnego udostępniania zdjęć podczas sesji, wydarzeń i pracy zespołowej."] = "A photography hub for previewing, selecting and locally sharing photos during sessions, events and team workflows.",
        ["Działa lokalnie"] = "Runs locally", ["1 lub wiele ekranów"] = "One or multiple displays", ["Zdjęcia bez rekompresji"] = "No photo recompression",
        ["Co robi Fotopodgląd"] = "What Fotopodgląd does", ["Okno B — galeria"] = "Window B — gallery",
        ["Pokazuje wybrane zdjęcie w dużym formacie. Opcjonalnie może automatycznie śledzić najnowsze pliki, zachowując ustawiony czas na obejrzenie ręcznie wybranego zdjęcia. Wyświetla też EXIF, histogram i ostrzeżenia ekspozycji."] = "Shows the selected photo at a large size. It can optionally follow the latest files automatically while preserving the configured viewing time for a manually selected photo. It also displays EXIF data, a histogram and exposure warnings.",
        ["Pokazuje przewijaną siatkę miniaturek, instrukcję dla gości i kody QR. Kliknięte zdjęcie otwiera się w oknie podglądu, a prawy przycisk oznacza je do filtrowania."] = "Shows a scrollable thumbnail grid, guest instructions and QR codes. A selected photo opens in the preview window and right-clicking flags it for filtering.",
        ["Na jednym ekranie podgląd zajmuje górną część, a galeria dolne 40% wysokości. Po odłączeniu dodatkowego monitora aplikacja automatycznie przechodzi do układu jednoekranowego."] = "On one display, the preview uses the upper area and the gallery the lower 40%. If an additional display is disconnected, the app automatically switches to the single-display layout.",
        ["Jak rozpocząć sesję"] = "How to start a session",
        ["1. Wybierz folder, do którego aparat zapisuje zdjęcia.\n2. Przypisz podgląd i galerię do odpowiednich ekranów.\n3. Włącz hotspot, jeżeli goście mają pobierać zdjęcia przez QR.\n4. Fotografuj — nowe pliki pojawią się automatycznie, bez ręcznego odświeżania."] = "1. Select the folder where the camera saves photos.\n2. Assign the preview and gallery to the appropriate displays.\n3. Enable the hotspot if guests should download photos through QR codes.\n4. Start shooting — new files appear automatically without manual refresh.",
        ["Hotspot i udostępnianie przez QR"] = "Hotspot and QR sharing",
        ["Pierwszy kod QR łączy telefon z siecią Wi‑Fi utworzoną przez komputer. Drugi prowadzi zawsze do zdjęcia widocznego aktualnie w podglądzie — nie po prostu do najnowszego pliku."] = "The first QR code connects a phone to the Wi‑Fi network created by the computer. The second always points to the photo currently shown in the preview — not simply the newest file.",
        ["Zdjęcia są udostępniane bezpośrednio z wybranego folderu w oryginalnej jakości, bez rekompresji. Limit równoległych pobrań chroni komputer i sieć przed przeciążeniem. Po pięciu minutach bez pobierania hasło sesji jest zmieniane, a kody QR aktualizują się automatycznie."] = "Photos are shared directly from the selected folder in their original quality, without recompression. A concurrent-download limit protects the computer and network from overload. After five minutes without a download, the session password rotates and the QR codes update automatically.",
        ["Hotspot wymaga obsługi mobilnego punktu dostępu przez kartę i sterownik Wi‑Fi. Zakładka Diagnostyka pokazuje stan uruchamiania, adres IP, próby automatycznej naprawy i czytelny opis błędu."] = "The hotspot requires mobile-hotspot support from the Wi‑Fi adapter and driver. The Diagnostics tab shows startup status, IP address, automatic repair attempts and a readable error description.",
        ["Szybkość i niezawodność"] = "Performance and reliability",
        ["• Najpierw ładowane są zdjęcia widoczne na ekranie oraz wybrany podgląd.\n• Aplikacja korzysta z miniaturek osadzonych w plikach, gdy są dość duże dla kafelka siatki.\n• EXIF i cięższe dekodowanie są wykonywane dopiero wtedy, gdy są potrzebne.\n• Nieaktualne operacje są anulowane podczas szybkiego przechodzenia między zdjęciami.\n• Pamięć podręczna ma regulowany limit i usuwa najstarsze elementy.\n• Po utracie dysku lub folderu obserwowanie zostaje automatycznie wznowione po jego powrocie."] = "• On-screen photos and the selected preview are loaded first.\n• The app uses thumbnails embedded in files when they are large enough for a grid tile.\n• EXIF data and heavier decoding are performed only when needed.\n• Obsolete operations are cancelled while navigating quickly between photos.\n• The cache has an adjustable limit and evicts its oldest items.\n• If a drive or folder disappears, watching resumes automatically when it returns.",
        ["Obsługiwane pliki"] = "Supported files",
        ["Standardowe: JPEG, JPG, PNG i TIFF.\nNowoczesne: HEIC i HEIF.\nRAW: ARW, NEF, NRW, CR2, CR3, RAF, DNG, RW2, ORF i PEF."] = "Standard: JPEG, JPG, PNG and TIFF.\nModern: HEIC and HEIF.\nRAW: ARW, NEF, NRW, CR2, CR3, RAF, DNG, RW2, ORF and PEF.",
        ["Dostępność podglądu konkretnego pliku RAW lub HEIC zależy od jego wariantu oraz kodeków dostępnych w systemie. Plik pobierany przez gościa pozostaje oryginałem, niezależnie od formatu użytego do podglądu."] = "Preview availability for a particular RAW or HEIC file depends on its variant and codecs available in the system. The file downloaded by a guest remains the original regardless of the format used for previewing.",
        ["Sterowanie"] = "Controls", ["Klawiatura"] = "Keyboard", ["Mysz"] = "Mouse",
        ["Ctrl+P — ustawienia\nEsc — wyjście z powiększenia\nHome — najnowsze zdjęcie\n← / → — poprzednie / następne zdjęcie\nCtrl+M — minimalizacja obu widoków\nF11 — pełny ekran / tryb okienkowy"] = "Ctrl+P — settings\nEsc — exit zoom\nHome — latest photo\n← / → — previous / next photo\nCtrl+M — minimize both views\nF11 — fullscreen / windowed mode",
        ["Kliknięcie miniatury — wybór zdjęcia\nPrawy przycisk — oznaczenie zdjęcia\nKółko — przybliżanie i oddalanie\nPrzeciąganie — przesuwanie powiększenia\nDwuklik — widok 100% / dopasowanie"] = "Click a thumbnail — select a photo\nRight-click — flag a photo\nMouse wheel — zoom in and out\nDrag — pan the zoomed photo\nDouble-click — 100% / fit",
        ["Prywatność i dane"] = "Privacy and data",
        ["Fotopodgląd nie wysyła zdjęć do chmury ani zewnętrznej usługi. Galeria dla gości działa lokalnie pomiędzy komputerem a urządzeniami połączonymi z hotspotem. Aplikacja nie modyfikuje i nie kopiuje zdjęć źródłowych."] = "Fotopodgląd does not upload photos to the cloud or any external service. The guest gallery works locally between the computer and devices connected to its hotspot. The app does not modify or copy source photos.",
        ["Ustawienia użytkownika są przechowywane w pliku %AppData%\\Fotopodglad\\settings.json. Cache miniaturek działa wyłącznie w pamięci operacyjnej i respektuje limit ustawiony przez użytkownika."] = "User settings are stored in %AppData%\\Fotopodglad\\settings.json. The thumbnail cache uses memory only and respects the limit configured by the user.",
        ["Diagnostyka, aktualizacje i pomoc"] = "Diagnostics, updates and help",
        ["W zakładce Diagnostyka sprawdzisz stan hotspotu, adres serwera, dostępność folderu i aktywne pobrania. Aplikacja może również sprawdzać najnowsze wydanie w GitHub Releases — automatycznie przy starcie lub ręcznie przyciskiem „Sprawdź teraz”."] = "The Diagnostics tab shows hotspot status, server address, folder availability and active downloads. The app can also check the latest GitHub Release automatically at startup or manually with the “Check now” button.",
        ["Kod źródłowy, lista zmian, wydania i plik wykonywalny:"] = "Source code, change log, releases and the executable:",
        ["Obsługiwane są JPEG, PNG, TIFF, HEIC/HEIF oraz popularne formaty RAW: ARW, NEF/NRW, CR2/CR3, RAF, DNG, RW2, ORF i PEF. Dostępność konkretnego kodeka zależy od pliku i systemu."] = "Supported formats include JPEG, PNG, TIFF, HEIC/HEIF and popular RAW files: ARW, NEF/NRW, CR2/CR3, RAF, DNG, RW2, ORF and PEF. Decoding depends on the specific file and codec.",
        ["Pierwszy QR łączy telefon z hotspotem, drugi pobiera dokładnie zdjęcie widoczne w podglądzie. Po pięciu minutach bez pobierania aplikacja zmienia hasło sesji. Liczba równoległych pobrań jest ograniczona, a pliki wysyłane są w oryginalnej jakości."] = "The first QR joins the phone to the hotspot; the second downloads the photo currently shown in the preview. After five minutes without a download the session password is rotated. Concurrent downloads are limited and files retain their original quality.",
        ["1. Zeskanuj „Dołącz do sieci”"] = "1. Scan “Join the network”",
        ["  •  2. Kliknij zdjęcie, które chcesz pobrać"] = "  •  2. Select the photo you want to download",
        ["  •  3. Zeskanuj „Pobierz wyświetlane zdjęcie” — możesz zapisać je na telefonie"] = "  •  3. Scan “Download displayed photo” and save it to your phone"
    };
    // ToDictionary po wartościach rzucało ArgumentException przy powtórzonym tłumaczeniu, a wyjątek
    // z inicjalizatora statycznego zabijał aplikację przy pierwszym pokazaniu okna ustawień.
    // Ręczna pętla nadpisuje duplikat zamiast przerywać start; test pilnuje unikalności tłumaczeń.
    private static readonly IReadOnlyDictionary<string, string> Polish = BuildReverseMap(English);

    /// <summary>Słownik polski → angielski; udostępniony dla testów pilnujących spójności tłumaczeń.</summary>
    public static IReadOnlyDictionary<string, string> Translations => English;

    public static void Apply(DependencyObject root, AppSettings settings)
    {
        Walk(root, AppearanceService.UseEnglish(settings));
    }

    private static Dictionary<string, string> BuildReverseMap(IReadOnlyDictionary<string, string> source)
    {
        var reverse = new Dictionary<string, string>(source.Count, StringComparer.Ordinal);
        foreach (var pair in source)
        {
            reverse[pair.Value] = pair.Key;
        }
        return reverse;
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
