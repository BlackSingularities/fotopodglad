using System.Collections.ObjectModel;
using Fotopodglad.Models;

namespace Fotopodglad.Services;

public interface IPhotoLibraryService
{
    /// <summary>Zdjęcia posortowane malejąco po czasie — najnowsze na indeksie 0. Modyfikowana wyłącznie na wątku UI.</summary>
    ObservableCollection<PhotoItem> Photos { get; }

    PhotoItem? Latest { get; }

    /// <summary>Wywoływane na wątku UI, gdy pojawi się nowe najnowsze zdjęcie.</summary>
    event Action<PhotoItem>? NewestChanged;

    void Start(string folderPath);
    void Stop();
}
