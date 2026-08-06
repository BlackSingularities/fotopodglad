namespace Fotopodglad.Services;

public interface IFolderWatcherService : IDisposable
{
    /// <summary>Wywoływane w wątku tła, gdy nowy plik JPG pojawił się w folderze i jest już w pełni zapisany.</summary>
    event Action<string>? PhotoReady;

    void Start(string folderPath);
    void Stop();
}
