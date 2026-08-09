namespace Fotopodglad.ViewModels;

/// <summary>VM Okna A — cienka otoczka na konfigurowalny podgląd zdjęcia.</summary>
public sealed class MainViewWindowViewModel : ViewModelBase
{
    public FullscreenPhotoViewModel Preview { get; }

    public MainViewWindowViewModel(FullscreenPhotoViewModel preview)
    {
        Preview = preview;
    }
}
