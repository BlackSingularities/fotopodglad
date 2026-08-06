using System.Collections.ObjectModel;
using Fotopodglad.Models;
using Fotopodglad.Services;

namespace Fotopodglad.ViewModels;

/// <summary>
/// VM Okna B: niekończąca się siatka wszystkich zdjęć. Kliknięcie miniatury przekazuje zdjęcie
/// do Okna A, dzięki czemu siatka pozostaje cały czas widoczna na swoim monitorze.
/// </summary>
public sealed class GridWindowViewModel : ViewModelBase
{
    private readonly IPhotoLibraryService _library;
    private readonly MainViewWindowViewModel _mainView;

    public ObservableCollection<PhotoItem> Photos => _library.Photos;

    public GridWindowViewModel(IPhotoLibraryService library, MainViewWindowViewModel mainView)
    {
        _library = library;
        _mainView = mainView;
    }

    public void OnPhotoClicked(PhotoItem photo) => _mainView.Preview.ShowManually(photo);
}
