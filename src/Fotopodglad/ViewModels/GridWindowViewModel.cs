using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fotopodglad.Models;
using Fotopodglad.Services;

namespace Fotopodglad.ViewModels;

/// <summary>
/// VM Okna B: niekończąca się siatka wszystkich zdjęć + reużywalny overlay pełnoekranowy (FullscreenPhotoView).
/// Kliknięcie miniatury pokazuje ją na min. 10s (tryb Manual), po czym overlay przełącza się na tryb Auto
/// (zawsze najnowsze) i zostaje pełnoekranowy — nie wraca automatycznie do siatki (potwierdzone z użytkownikiem).
/// </summary>
public sealed partial class GridWindowViewModel : ViewModelBase
{
    private readonly IPhotoLibraryService _library;

    public ObservableCollection<PhotoItem> Photos => _library.Photos;

    public FullscreenPhotoViewModel Overlay { get; }

    [ObservableProperty]
    private bool isOverlayVisible;

    public GridWindowViewModel(IPhotoLibraryService library, FullscreenPhotoViewModel overlay)
    {
        _library = library;
        Overlay = overlay;
    }

    public void OnPhotoClicked(PhotoItem photo)
    {
        Overlay.ShowManually(photo);
        IsOverlayVisible = true;
    }
}
