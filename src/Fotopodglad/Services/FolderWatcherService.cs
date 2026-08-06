using System.Threading.Channels;
using Fotopodglad.Helpers;

namespace Fotopodglad.Services;

/// <summary>
/// Obserwuje folder pod kątem nowych plików JPG zapisywanych przez kartę WiFi aparatu.
/// FileSystemWatcher bywa zawodny przy dużym ruchu I/O (gubi zdarzenia przy przepełnieniu wewnętrznego
/// bufora), dlatego dokładamy fallback w postaci pollingu co 2 sekundy jako drugą linię wykrywania.
/// Każdy wykryty plik przechodzi przez FileStabilityChecker, zanim zgłosimy go jako gotowy do odczytu.
///
/// Pliki obecne w folderze już przy starcie ("zaległości" z poprzedniej sesji) są rozróżniane od plików
/// wykrytych na żywo (isBacklog=true/false) — dzięki temu PhotoLibraryService może dociążyć siatkę
/// historią bez wywoływania NewestChanged dla każdego z osobna (co powodowało gwałtowne "przewijanie"
/// podglądu w Oknie A podczas ładowania folderu z wieloma zdjęciami).
/// </summary>
public sealed class FolderWatcherService : IFolderWatcherService
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg"];

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cts;
    private Channel<string>? _pendingFiles;
    private readonly HashSet<string> _knownFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _knownFilesLock = new();

    public event Action<string, bool>? PhotoReady;
    public event Action? InitialScanCompleted;

    public void Start(string folderPath)
    {
        Stop();

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder nie istnieje: {folderPath}");
        }

        _cts = new CancellationTokenSource();
        _pendingFiles = Channel.CreateUnbounded<string>();

        // Najpierw robimy migawkę zaległości i oznaczamy ją jako znaną. Dzięki temu zdarzenia watchera
        // nie wyślą tych samych plików drugi raz, gdy jest uruchamiany równolegle ze skanem startowym.
        var backlog = EnumerateSupportedFiles(folderPath).ToList();

        lock (_knownFilesLock)
        {
            _knownFiles.Clear();
            foreach (var path in backlog)
            {
                _knownFiles.Add(path);
            }
        }

        _watcher = new FileSystemWatcher(folderPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = false,
            InternalBufferSize = 65536
        };
        _watcher.Created += OnFileSystemEvent;
        _watcher.Renamed += OnFileSystemEvent;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Error += OnWatcherError;
        _watcher.EnableRaisingEvents = true;

        _ = ProcessQueueAsync(_pendingFiles.Reader, _cts.Token);
        _ = PollFallbackLoopAsync(folderPath, _cts.Token);
        _ = LoadBacklogAsync(backlog, _cts.Token);
    }

    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileSystemEvent;
            _watcher.Renamed -= OnFileSystemEvent;
            _watcher.Deleted -= OnFileDeleted;
            _watcher.Error -= OnWatcherError;
            _watcher.Dispose();
            _watcher = null;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _pendingFiles?.Writer.TryComplete();
        _pendingFiles = null;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        EnqueueIfSupported(e.FullPath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e) => ForgetFile(e.FullPath);

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        // FileSystemWatcher potrafi przestać raportować zdarzenia po błędzie wewnętrznym (np. przepełnienie
        // bufora) — polling fallback w PollFallbackLoopAsync i tak nadrobi zgubione pliki.
    }

    private void EnqueueIfSupported(string fullPath)
    {
        var extension = Path.GetExtension(fullPath);
        if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        lock (_knownFilesLock)
        {
            if (!_knownFiles.Add(fullPath))
            {
                return;
            }
        }

        _pendingFiles?.Writer.TryWrite(fullPath);
    }

    /// <summary>
    /// Pliki obecne w folderze w chwili startu są stabilizowane i zgłaszane osobnym torem (isBacklog=true),
    /// równolegle (wszystkie na raz — to tylko sprawdzenie blokady pliku, tanie), a na koniec zgłaszamy
    /// InitialScanCompleted, żeby biblioteka mogła pokazać faktycznie najnowsze zdjęcie dopiero raz,
    /// po załadowaniu całej historii, zamiast migać przez każde zdjęcie po kolei.
    /// </summary>
    private async Task LoadBacklogAsync(IReadOnlyList<string> existing, CancellationToken cancellationToken)
    {
        try
        {
            var stabilityResults = await Task.WhenAll(
                existing.Select(path => FileStabilityChecker.WaitUntilStableAsync(path, TimeSpan.FromSeconds(15), cancellationToken)));

            for (var i = 0; i < existing.Count; i++)
            {
                if (stabilityResults[i])
                {
                    PhotoReady?.Invoke(existing[i], true);
                }
                else
                {
                    ForgetFile(existing[i]);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        InitialScanCompleted?.Invoke();
    }

    private async Task PollFallbackLoopAsync(string folderPath, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                foreach (var path in EnumerateSupportedFiles(folderPath))
                {
                    EnqueueIfSupported(path);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static IEnumerable<string> EnumerateSupportedFiles(string folderPath)
    {
        try
        {
            return Directory.EnumerateFiles(folderPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private async Task ProcessQueueAsync(ChannelReader<string> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var path in reader.ReadAllAsync(cancellationToken))
            {
                var isStable = await FileStabilityChecker.WaitUntilStableAsync(
                    path, TimeSpan.FromSeconds(15), cancellationToken);

                if (isStable)
                {
                    // Pliki z tego toru są zawsze "na żywo" — zaległości startowe idą przez LoadBacklogAsync.
                    PhotoReady?.Invoke(path, false);
                }
                else
                {
                    // Polling może ponowić plik, który nie zdążył ustabilizować się w pierwszym limicie.
                    ForgetFile(path);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ForgetFile(string path)
    {
        lock (_knownFilesLock)
        {
            _knownFiles.Remove(path);
        }
    }

    public void Dispose() => Stop();
}
