# Fotopodgląd

Aplikacja desktopowa (WPF, .NET 8) dla fotografa pracującego na dwóch monitorach podczas sesji zdjęciowej. Karta pamięci WiFi w aparacie zapisuje zdjęcia na bieżąco do wskazanego folderu na dysku — aplikacja obserwuje ten folder i pokazuje efekty sesji na żywo, bez żadnej dodatkowej interakcji.

## Funkcje

- **Okno A (Ekran 1)** — pełnoekranowy, pozbawiony jakichkolwiek kontrolek podgląd zawsze najnowszego zdjęcia: nazwa pliku, godzina, parametry ekspozycji (przysłona, czas, ISO, ogniskowa), wymiary i rozmiar pliku — każde z osobną ikoną wektorową (bez emoji/fontów ikon). Możliwość przybliżania kółkiem myszy.
- **Okno B (Ekran 2)** — pełnoekranowa, niekończąca się siatka masonry o 6 kolumnach ze wszystkimi zdjęciami z sesji, bez odstępów, najnowsze zawsze na górze. Kliknięcie zdjęcia pokazuje je pełnoekranowo przez ustawiony czas, po czym aplikacja automatycznie wraca do siatki; klawisz `Esc` zamyka podgląd od razu.
- **2 monitory** — okna rozmieszczają się na osobnych ekranach (domyślnie automatycznie, można wybrać ręcznie w ustawieniach); przy jednym monitorze oba okna nakładają się na tym samym ekranie (bez błędów, przełączanie Alt+Tab).
- **Ustawienia** — po wyborze folderu aplikacja pyta, czy zmienić ustawienia: który ekran ma Okno A, który Okno B, nazwa/hasło sieci WiFi dla gości, liczba kolumn siatki, czas podglądu wybranego zdjęcia przed powrotem do najnowszego. Osobna zakładka "O aplikacji" z autorem i linkiem do repozytorium.
- **Pobieranie zdjęć na telefon gościa (opcjonalne)** — komputer tworzy odizolowany hotspot WiFi; w sidebarze Okna B widoczne są dwa kody QR (dołączenie do sieci + pobranie najnowszego zdjęcia). Sieć wyłącza się automatycznie po 5 minutach bezczynności i wznawia przy nowym zdjęciu. Jeśli sprzęt nie wspiera jednoczesnego trybu access-point + klient WiFi, funkcja po cichu się wyłącza — reszta aplikacji działa bez zmian.
- **Aplikacja nigdzie nie zapisuje kopii zdjęć.** Miniatury w siatce są cache'owane wyłącznie w pamięci RAM, serwer HTTP dla gości czyta i wysyła bajty bezpośrednio z oryginalnego pliku. Jedyny zapisywany plik to `settings.json` (ścieżka folderu i ustawienia, w `%AppData%\Fotopodglad`).

## Pobieranie i uruchomienie

Gotowy, samodzielny plik `.exe` (nie wymaga instalowania .NET) dostępny w [Releases](../../releases) — pobierz `Fotopodglad-win-x64.zip`, rozpakuj i uruchom `Fotopodglad.exe`.

> Plik pochodzi z internetu, więc Windows może go domyślnie zablokować (SmartScreen). Jeśli tak się stanie: kliknij prawym na plik → *Właściwości* → zaznacz *Odblokuj* → *OK*.

Przy pierwszym uruchomieniu pojawi się okno wyboru folderu, do którego karta WiFi aparatu zapisuje zdjęcia. Od tego momentu aplikacja działa całkowicie automatycznie.

## Budowanie ze źródeł

Wymagany [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (lub nowszy).

```bash
dotnet build Fotopodglad.sln
dotnet test tests/Fotopodglad.Tests/Fotopodglad.Tests.csproj
dotnet run --project src/Fotopodglad/Fotopodglad.csproj
```

### Publikacja samodzielnego pliku .exe

```bash
dotnet publish src/Fotopodglad/Fotopodglad.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
```

## Struktura projektu

```
src/Fotopodglad/
  Models/         modele danych (PhotoItem, ExifData, ScreenInfo, ...)
  Services/        FolderWatcherService, ExifService, ScreenService, PhotoLibraryService, ThumbnailCache
  Services/GuestGallery/  hotspot WiFi, serwer HTTP, generator QR, koordynator (funkcja gości)
  ViewModels/     MVVM (CommunityToolkit.Mvvm)
  Views/          MainViewWindow (Okno A), GridWindow (Okno B) — chrome-less
  Controls/       FullscreenPhotoView, MasonryGridControl, ZoomableImage, ExifBadge, GuestAccessSidebar
  Converters/     formatowanie EXIF (przysłona, czas naświetlania, rozmiar pliku, ...)
  Resources/      IconGeometries.xaml (ikony jako WPF Path/Geometry), style, kolory, typografia
tests/Fotopodglad.Tests/   testy jednostkowe (xUnit): watcher folderu, EXIF, algorytm masonry
```

## Wymagania sprzętowe/systemowe

- Windows 10/11.
- Do pełnego doświadczenia: 2 monitory (aplikacja działa też na jednym, z ograniczeniem opisanym wyżej).
- Funkcja pobierania na telefon gościa wymaga karty WiFi wspierającej jednoczesny tryb access-point + klient — zależne od sprzętu/sterownika, wykrywane automatycznie przy starcie.

## Licencja

Projekt prywatny — brak jawnej licencji.
