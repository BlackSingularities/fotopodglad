using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fotopodglad.Models;
using Fotopodglad.Services;

namespace Fotopodglad.ViewModels;

/// <summary>
/// VM Okna B: niekończąca się siatka wszystkich zdjęć + reużywalny overlay pełnoekranowy (FullscreenPhotoView).
/// Kliknięcie miniatury pokazuje ją przez skonfigurowany czas (tryb Manual), po czym overlay znika,
/// przywracając siatkę. Dzięki temu Okno A pozostaje podglądem, a Okno B galerią zdjęć.
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
        Overlay.PropertyChanged += OnOverlayPropertyChanged;
    }

    public void OnPhotoClicked(PhotoItem photo)
    {
        Overlay.ShowManually(photo);
        IsOverlayVisible = true;
    }

    public void CloseOverlay()
    {
        IsOverlayVisible = false;
        Overlay.ShowLatestAutomatically();
    }

    private void OnOverlayPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FullscreenPhotoViewModel.Mode) && Overlay.Mode == PreviewMode.Auto)
        {
            IsOverlayVisible = false;
        }
    }
}
