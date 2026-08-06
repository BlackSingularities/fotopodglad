using System.Collections.ObjectModel;
using System.Windows.Threading;
using Fotopodglad.Models;

namespace Fotopodglad.Services;

/// <summary>
/// Punkt prawdy dla obu okien aplikacji: łączy FolderWatcherService (wykrywanie nowych plików)
/// z ExifService (parsowanie metadanych) i eksponuje jedną współdzieloną kolekcję zdjęć,
/// posortowaną malejąco po czasie wykonania. Wstawienia do kolekcji zawsze wykonywane na wątku UI,
/// bo ObservableCollection nie jest bezpieczna wielowątkowo.
///
/// Zdjęcia "zaległe" (obecne w folderze już przy starcie) są wstawiane do Photos od razu — siatka
/// w Oknie B ma się nimi wypełnić — ale NIE wywołują NewestChanged pojedynczo, bo to powodowało
/// gwałtowne "przewijanie" podglądu w Oknie A przez każde historyczne zdjęcie z osobna. Zamiast tego
/// NewestChanged dla zaległości jest wywoływane raz, dopiero gdy cała zaległość zostanie załadowana.
/// </summary>
public sealed class PhotoLibraryService : IPhotoLibraryService
{
    private readonly IFolderWatcherService _watcher;
    private readonly IExifService _exifService;
    private readonly Dispatcher _dispatcher;
    private readonly HashSet<string> _loadedPaths = new(StringComparer.OrdinalIgnoreCase);
    private long _sequenceCounter;

    private int _pendingBacklogCount;
    private int _initialScanCompleted;
    private int _finalLatestAnnounced;

    public ObservableCollection<PhotoItem> Photos { get; } = new();

    public PhotoItem? Latest => Photos.Count > 0 ? Photos[0] : null;

    public event Action<PhotoItem>? NewestChanged;

    public PhotoLibraryService(IFolderWatcherService watcher, IExifService exifService)
    {
        _watcher = watcher;
        _exifService = exifService;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _watcher.PhotoReady += OnPhotoReady;
        _watcher.InitialScanCompleted += OnInitialScanCompleted;
    }

    public void Start(string folderPath)
    {
        Photos.Clear();
        _loadedPaths.Clear();
        _sequenceCounter = 0;
        _pendingBacklogCount = 0;
        _initialScanCompleted = 0;
        _finalLatestAnnounced = 0;
        _watcher.Start(folderPath);
    }

    public void Stop() => _watcher.Stop();

    private void OnPhotoReady(string filePath, bool isBacklog)
    {
        if (isBacklog)
        {
            Interlocked.Increment(ref _pendingBacklogCount);
        }

        // Wywoływane z wątku tła watchera — parsowanie EXIF też robimy w tle, żeby nie blokować UI.
        _ = HandlePhotoReadyAsync(filePath, isBacklog);
    }

    private async Task HandlePhotoReadyAsync(string filePath, bool isBacklog)
    {
        try
        {
            ExifData exif;
            try
            {
                exif = await _exifService.ExtractAsync(filePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return;
            }

            var discoveredAtUtc = DateTime.UtcNow;
            try
            {
                var lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
                if (lastWriteUtc != DateTime.MinValue)
                {
                    discoveredAtUtc = lastWriteUtc;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            var item = new PhotoItem
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Exif = exif,
                // Dla zdjęć bez EXIF czas modyfikacji daje prawidłową kolejność także podczas skanu startowego.
                DiscoveredAtUtc = discoveredAtUtc,
                SequenceId = Interlocked.Increment(ref _sequenceCounter)
            };

            await _dispatcher.InvokeAsync(() => InsertPhoto(item, isBacklog));
        }
        finally
        {
            if (isBacklog && Interlocked.Decrement(ref _pendingBacklogCount) == 0)
            {
                TryAnnounceLatestAfterBacklog();
            }
        }
    }

    private void OnInitialScanCompleted()
    {
        Interlocked.Exchange(ref _initialScanCompleted, 1);
        if (Volatile.Read(ref _pendingBacklogCount) == 0)
        {
            TryAnnounceLatestAfterBacklog();
        }
    }

    private void TryAnnounceLatestAfterBacklog()
    {
        if (Volatile.Read(ref _initialScanCompleted) == 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _finalLatestAnnounced, 1, 0) != 0)
        {
            return; // już ogłoszone (albo się nią zajmuje inny wątek w tej samej chwili)
        }

        _ = _dispatcher.InvokeAsync(() =>
        {
            if (Latest is { } latest)
            {
                NewestChanged?.Invoke(latest);
            }
        });
    }

    private void InsertPhoto(PhotoItem item, bool isBacklog)
    {
        if (!_loadedPaths.Add(item.FilePath))
        {
            return;
        }

        // Sortowanie: najpierw po dacie wykonania (EXIF), a przy braku/remisie po kolejności wykrycia (SequenceId).
        var insertIndex = 0;
        for (; insertIndex < Photos.Count; insertIndex++)
        {
            if (ComparePhotos(item, Photos[insertIndex]) < 0)
            {
                break;
            }
        }

        Photos.Insert(insertIndex, item);

        // Zaległości startowe nigdy nie wywołują NewestChanged pojedynczo — patrz TryAnnounceLatestAfterBacklog.
        if (insertIndex == 0 && !isBacklog)
        {
            NewestChanged?.Invoke(item);
        }
    }

    private static int ComparePhotos(PhotoItem a, PhotoItem b)
    {
        // EXIF DateTaken nie zawiera strefy czasowej i reprezentuje lokalny czas aparatu.
        // Fallback z systemu plików konwertujemy więc do czasu lokalnego przed porównaniem.
        var aTime = a.Exif.DateTaken ?? a.DiscoveredAtUtc.ToLocalTime();
        var bTime = b.Exif.DateTaken ?? b.DiscoveredAtUtc.ToLocalTime();
        var timeComparison = bTime.CompareTo(aTime); // malejąco: nowsze pierwsze
        return timeComparison != 0 ? timeComparison : b.SequenceId.CompareTo(a.SequenceId);
    }
}
