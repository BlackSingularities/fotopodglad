using System.Collections.ObjectModel;
using System.Windows.Threading;
using Fotopodglad.Models;

namespace Fotopodglad.Services;

/// <summary>
/// Punkt prawdy dla obu okien aplikacji: łączy FolderWatcherService (wykrywanie nowych plików)
/// z ExifService (parsowanie metadanych) i eksponuje jedną współdzieloną kolekcję zdjęć,
/// posortowaną malejąco po czasie wykonania. Wstawienia do kolekcji zawsze wykonywane na wątku UI,
/// bo ObservableCollection nie jest bezpieczna wielowątkowo.
/// </summary>
public sealed class PhotoLibraryService : IPhotoLibraryService
{
    private readonly IFolderWatcherService _watcher;
    private readonly IExifService _exifService;
    private readonly Dispatcher _dispatcher;
    private long _sequenceCounter;

    public ObservableCollection<PhotoItem> Photos { get; } = new();

    public PhotoItem? Latest => Photos.Count > 0 ? Photos[0] : null;

    public event Action<PhotoItem>? NewestChanged;

    public PhotoLibraryService(IFolderWatcherService watcher, IExifService exifService)
    {
        _watcher = watcher;
        _exifService = exifService;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _watcher.PhotoReady += OnPhotoReady;
    }

    public void Start(string folderPath)
    {
        Photos.Clear();
        _sequenceCounter = 0;
        _watcher.Start(folderPath);
    }

    public void Stop() => _watcher.Stop();

    private void OnPhotoReady(string filePath)
    {
        // Wywoływane z wątku tła watchera — parsowanie EXIF też robimy w tle, żeby nie blokować UI.
        _ = HandlePhotoReadyAsync(filePath);
    }

    private async Task HandlePhotoReadyAsync(string filePath)
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

        var item = new PhotoItem
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Exif = exif,
            DiscoveredAtUtc = DateTime.UtcNow,
            SequenceId = Interlocked.Increment(ref _sequenceCounter)
        };

        await _dispatcher.InvokeAsync(() => InsertPhoto(item));
    }

    private void InsertPhoto(PhotoItem item)
    {
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

        if (insertIndex == 0)
        {
            NewestChanged?.Invoke(item);
        }
    }

    private static int ComparePhotos(PhotoItem a, PhotoItem b)
    {
        var aTime = a.Exif.DateTaken ?? a.DiscoveredAtUtc;
        var bTime = b.Exif.DateTaken ?? b.DiscoveredAtUtc;
        var timeComparison = bTime.CompareTo(aTime); // malejąco: nowsze pierwsze
        return timeComparison != 0 ? timeComparison : b.SequenceId.CompareTo(a.SequenceId);
    }
}
