namespace Fotopodglad.Services;

public interface IFolderWatcherService : IDisposable
{
    /// <summary>
    /// Wywoływane w wątku tła, gdy plik JPG jest już w pełni zapisany i gotowy do odczytu.
    /// Drugi parametr: true dla plików obecnych w folderze już przy starcie (zaległości z poprzedniej
    /// sesji), false dla plików wykrytych na żywo po starcie.
    /// </summary>
    event Action<string, bool>? PhotoReady;

    /// <summary>Wywoływane raz, po zakończeniu stabilizacji i zgłoszeniu wszystkich zaległych plików z folderu.</summary>
    event Action? InitialScanCompleted;

    void Start(string folderPath);
    void Stop();
}
