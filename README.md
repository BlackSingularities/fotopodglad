# Fotopodgląd

Aplikacja desktopowa (WPF, .NET 8) dla fotografa pracującego na dwóch monitorach podczas sesji zdjęciowej. Karta pamięci WiFi w aparacie zapisuje zdjęcia na bieżąco do wskazanego folderu na dysku — aplikacja obserwuje ten folder i pokazuje efekty sesji na żywo, bez żadnej dodatkowej interakcji.

## Funkcje wersji 2.0

- **Okno A (Ekran 1)** — pełnoekranowy, pozbawiony jakichkolwiek kontrolek podgląd zawsze najnowszego zdjęcia: nazwa pliku, godzina, parametry ekspozycji (przysłona, czas, ISO, ogniskowa i pełna nazwa programu ekspozycji), wymiary i rozmiar pliku — każde z osobną ikoną wektorową (bez emoji/fontów ikon). Możliwość przybliżania kółkiem myszy.
- **Okno B (Ekran 2)** — pełnoekranowa, przewijana siatka równych kafelków 3:2 ze wszystkimi zdjęciami z sesji, najnowsze zawsze na górze. Miniatury są równo przycinane, a kliknięcie pokazuje wybrane zdjęcie w Oknie A przez ustawiony czas; siatka pozostaje cały czas widoczna.
- **1 lub 2 monitory** — przy dwóch monitorach podgląd i siatka zajmują osobne ekrany. Przy jednym podgląd zajmuje górne 60%, a przewijana siatka dolne 40% szerokości ekranu. Panel QR pozostaje w prawym dolnym rogu.
- **Rozbudowane ustawienia i diagnostyka** — `Ctrl+P` otwiera konfigurację folderu, ekranów, filtrów, histogramu, ostrzeżeń ekspozycji, cache, motywu, języka, skali UI, tekstu EXIF/instrukcji i pobierania. Osobna karta pokazuje stan hotspotu, adres IP, próby naprawy, błąd sterownika/AP+STA, aktywne pobrania i dostępność folderu. Zmiany wizualne są stosowane na żywo; restart następuje tylko dla folderu, ekranów lub hotspotu.
- **Pobieranie zdjęć na telefon gościa (opcjonalne)** — komputer tworzy odizolowany hotspot Wi‑Fi; dwa kody QR służą do dołączenia do sieci i pobrania dokładnie zdjęcia widocznego w podglądzie. Plik jest wysyłany w oryginalnej jakości, bez rekompresji. Limit równoległych pobrań chroni aplikację, start ma maksymalnie trzy kontrolowane próby, a po pięciu minutach bez pobierania hasło sesji jest zmieniane.
- **Odporność na sprzęt** — utrata dysku/karty/folderu daje czytelne ostrzeżenie, a obserwowanie automatycznie wraca po odzyskaniu ścieżki. Odłączenie drugiego monitora przełącza aplikację na układ jednoekranowy. Pozycje okien są zapamiętywane.
- **Narzędzia fotografa** — przełączany histogram jasności lub RGB, oznaczenia przepaleń i niedoświetleń, powiększenie do 100% dwuklikiem, ograniczone przesuwanie oraz pełne nazwy programów ekspozycji.
- **Duże sesje i nowe formaty** — wirtualizowana regularna siatka, priorytet widocznych miniaturek, anulowanie starych dekodowań i limitowany cache RAM pozwalają pracować z tysiącami zdjęć. Oprócz JPEG/PNG/TIFF obsługiwane są HEIC/HEIF oraz RAW: ARW, NEF/NRW, CR2/CR3, RAF, DNG, RW2, ORF i PEF.
- **Skróty** — `Ctrl+P` ustawienia, `Esc` reset powiększenia, `Home` najnowsze zdjęcie, `←`/`→` poprzednie/następne, `Ctrl+M` minimalizacja, `F11` pełny ekran/tryb okienkowy.
- **Aktualizacje** — aplikacja może raz na dobę sprawdzić najnowsze wydanie na GitHubie. Interfejs automatycznie wybiera polski lub angielski zgodnie z językiem systemu; dostępne są motywy automatyczny, ciemny i jasny.
- **Dopasowane zasoby graficzne** — aplikacja zawiera osobne ikony PNG od 16 do 256 px oraz wielorozmiarowe ICO, dzięki czemu logo pozostaje ostre w oknie, na pasku zadań i w Eksploratorze Windows.
- **Aplikacja nigdzie nie zapisuje kopii zdjęć.** Miniatury w siatce są cache'owane wyłącznie w pamięci RAM, serwer HTTP dla gości czyta i wysyła bajty bezpośrednio z oryginalnego pliku. Jedyny zapisywany plik to `settings.json` (ścieżka folderu i ustawienia, w `%AppData%\Fotopodglad`).

## Pobieranie i uruchomienie

Gotowy, samodzielny plik `.exe` (nie wymaga instalowania .NET) jest dostępny w [Releases](../../releases). Pobierz i uruchom `Fotopodglad.exe` — bez instalatora, rozpakowywania i dodatkowych plików.

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

Wydania tagowane `v*` są automatycznie testowane i publikowane przez GitHub Actions. Jeśli repozytorium zawiera sekrety `WINDOWS_SIGNING_PFX_BASE64` i `WINDOWS_SIGNING_PFX_PASSWORD`, EXE jest przed publikacją podpisywany cyfrowo i znakowany czasem. Bez certyfikatu powstaje poprawny, ale niepodpisany plik.

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
- Jeden lub dwa monitory; układ dopasowuje się automatycznie.
- Funkcja pobierania na telefon gościa wymaga karty WiFi wspierającej jednoczesny tryb access-point + klient — zależne od sprzętu/sterownika, wykrywane automatycznie przy starcie.

## Licencja

Projekt prywatny — brak jawnej licencji.
