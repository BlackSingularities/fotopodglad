namespace Fotopodglad.ViewModels;

/// <summary>VM Okna A — cienka otoczka na FullscreenPhotoViewModel, zawsze w trybie Auto (zawsze najnowsze zdjęcie).</summary>
public sealed class MainViewWindowViewModel : ViewModelBase
{
    public FullscreenPhotoViewModel Preview { get; }

    public MainViewWindowViewModel(FullscreenPhotoViewModel preview)
    {
        Preview = preview;
        Preview.ShowLatestAutomatically();
    }
}
