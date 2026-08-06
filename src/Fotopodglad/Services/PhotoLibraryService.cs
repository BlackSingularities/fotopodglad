using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Windows.Threading;
using Fotopodglad.Helpers;
using Fotopodglad.Configuration;
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
    private readonly AppSettings _settings;
    private readonly HashSet<string> _loadedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<PhotoItem> _backlogItems = [];
    private readonly SemaphoreSlim _metadataSlots = new(Math.Clamp(Environment.ProcessorCount, 2, 8));
    private long _sequenceCounter;

    private int _pendingBacklogCount;
    private int _initialScanCompleted;
    private int _finalLatestAnnounced;
    private int _backlogReadySinceMerge;
    private int _backlogMergeScheduled;
    private int _firstBacklogBatchPublished;
    private PhotoItem? _announcedBacklogPreview;

    public ObservableCollection<PhotoItem> Photos { get; } = new BulkObservableCollection<PhotoItem>();

    public PhotoItem? Latest => Photos.Count > 0 ? Photos[0] : null;

    public event Action<PhotoItem>? NewestChanged;
    public event Action<bool, string?>? FolderAvailabilityChanged;
    public bool IsFolderAvailable => _watcher.IsAvailable;
    public string? FolderAvailabilityMessage => _watcher.AvailabilityMessage;

    public PhotoLibraryService(IFolderWatcherService watcher, IExifService exifService, AppSettings settings)
    {
        _watcher = watcher;
        _exifService = exifService;
        _settings = settings;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _watcher.PhotoReady += OnPhotoReady;
        _watcher.InitialScanCompleted += OnInitialScanCompleted;
        _watcher.AvailabilityChanged += OnAvailabilityChanged;
    }

    public void Start(string folderPath)
    {
        Photos.Clear();
        _loadedPaths.Clear();
        while (_backlogItems.TryTake(out _)) { }
        _sequenceCounter = 0;
        _pendingBacklogCount = 0;
        _initialScanCompleted = 0;
        _finalLatestAnnounced = 0;
        _backlogReadySinceMerge = 0;
        _backlogMergeScheduled = 0;
        _firstBacklogBatchPublished = 0;
        _announcedBacklogPreview = null;
        _watcher.Start(folderPath);
    }

    public void Stop() => _watcher.Stop();

    private void OnAvailabilityChanged(bool available, string? message) =>
        _ = _dispatcher.InvokeAsync(() => FolderAvailabilityChanged?.Invoke(available, message));

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
        var metadataSlotAcquired = false;
        try
        {
            await _metadataSlots.WaitAsync();
            metadataSlotAcquired = true;
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
                SequenceId = Interlocked.Increment(ref _sequenceCounter),
                IsFlagged = _settings.FlaggedPhotoPaths.Contains(filePath, StringComparer.OrdinalIgnoreCase)
            };

            if (isBacklog)
            {
                _backlogItems.Add(item);
                var ready = Interlocked.Increment(ref _backlogReadySinceMerge);
                var threshold = Volatile.Read(ref _firstBacklogBatchPublished) == 0 ? 12 : 128;
                if (ready >= threshold)
                {
                    ScheduleProgressiveBacklogMerge();
                }
            }
            else
            {
                await _dispatcher.InvokeAsync(() => InsertLivePhoto(item));
            }
        }
        finally
        {
            if (metadataSlotAcquired)
            {
                _metadataSlots.Release();
            }
            if (isBacklog && Interlocked.Decrement(ref _pendingBacklogCount) == 0)
            {
                TryAnnounceLatestAfterBacklog();
            }
        }
    }

    private void ScheduleProgressiveBacklogMerge()
    {
        if (Interlocked.CompareExchange(ref _backlogMergeScheduled, 1, 0) != 0)
        {
            return;
        }

        _ = _dispatcher.InvokeAsync(() =>
        {
            Interlocked.Exchange(ref _backlogReadySinceMerge, 0);
            MergeBacklog();
            var isFirstPublishedBatch = Interlocked.Exchange(ref _firstBacklogBatchPublished, 1) == 0;
            if (isFirstPublishedBatch && _announcedBacklogPreview is null && Latest is { } firstPreview)
            {
                _announcedBacklogPreview = firstPreview;
                NewestChanged?.Invoke(firstPreview);
            }
            Interlocked.Exchange(ref _backlogMergeScheduled, 0);

            if (Volatile.Read(ref _backlogReadySinceMerge) >= 128)
            {
                ScheduleProgressiveBacklogMerge();
            }
        });
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
            MergeBacklog();
            if (Latest is { } latest && !ReferenceEquals(latest, _announcedBacklogPreview))
            {
                _announcedBacklogPreview = latest;
                NewestChanged?.Invoke(latest);
            }
        });
    }

    private void MergeBacklog()
    {
        if (Photos is not BulkObservableCollection<PhotoItem> bulk)
        {
            return;
        }

        var newlyDecoded = new List<PhotoItem>();
        while (_backlogItems.TryTake(out var item))
        {
            newlyDecoded.Add(item);
        }

        if (newlyDecoded.Count == 0)
        {
            return;
        }

        var combined = Photos.Concat(newlyDecoded)
            .GroupBy(photo => photo.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        combined.Sort(ComparePhotos);

        _loadedPaths.Clear();
        foreach (var photo in combined)
        {
            _loadedPaths.Add(photo.FilePath);
        }

        bulk.ReplaceAll(combined);
    }

    private void InsertLivePhoto(PhotoItem item)
    {
        if (!_loadedPaths.Add(item.FilePath))
        {
            return;
        }

        // Sortowanie: najpierw po dacie wykonania (EXIF), a przy braku/remisie po kolejności wykrycia (SequenceId).
        var low = 0;
        var high = Photos.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (ComparePhotos(item, Photos[middle]) < 0)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        var insertIndex = low;
        Photos.Insert(insertIndex, item);

        if (insertIndex == 0)
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
