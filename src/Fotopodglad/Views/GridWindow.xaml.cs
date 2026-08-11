using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Fotopodglad.Configuration;
using Fotopodglad.Controls;
using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Fotopodglad.ViewModels;

namespace Fotopodglad.Views;

public partial class GridWindow : Window
{
    private readonly GuestAccessSidebar _guestSidebar;
    private readonly MasonryGridControl _grid;
    private readonly FullscreenPhotoViewModel _preview;

    public GridWindow(
        GridWindowViewModel viewModel,
        IThumbnailCache thumbnailCache,
        GuestAccessCoordinator guestAccess,
        AppSettings settings,
        IScreenService screenService)
    {
        InitializeComponent();
        Title = ApplicationVersion.CreateWindowTitle("siatka");
        DataContext = viewModel;

        var grid = _grid = new MasonryGridControl();
        grid.Initialize(thumbnailCache, viewModel.Photos, settings.GridColumnCount);
        grid.PhotoClicked += photo => viewModel.OnPhotoClicked(photo);
        grid.PhotoFlagToggled += viewModel.ToggleFlag;
        GridHost.Children.Add(grid);

        // Ramka wybranego zdjęcia podąża za podglądem — niezależnie od tego, czy zmieniło je
        // kliknięcie miniatury, strzałki, czy automatyczne pokazanie najnowszego pliku.
        _preview = viewModel.Preview;
        grid.SetSelectedPhoto(_preview.CurrentPhoto);
        _preview.PropertyChanged += OnPreviewPropertyChanged;
        Closed += (_, _) => _preview.PropertyChanged -= OnPreviewPropertyChanged;
        GuestInstructionTextBlock.FontSize = settings.InstructionTextSize * AppearanceService.ScaleFactor(settings);
        settings.Changed += (_, _) =>
        {
            grid.SetColumnCount(settings.GridColumnCount);
            if (settings.ShowGuestInstructions)
            {
                GuestInstructionBorder.ClearValue(VisibilityProperty);
            }
            else
            {
                GuestInstructionBorder.Visibility = Visibility.Collapsed;
            }
            GuestInstructionTextBlock.FontSize = settings.InstructionTextSize * AppearanceService.ScaleFactor(settings);
        };

        GuestInstructionBorder.DataContext = guestAccess;
        if (!settings.ShowGuestInstructions)
        {
            // Lokalna wartość ma pierwszeństwo przed triggerem Status=Active i trwale ukrywa pasek.
            GuestInstructionBorder.Visibility = Visibility.Collapsed;
        }

        _guestSidebar = new GuestAccessSidebar(settings, compactLayout: screenService.GetScreens().Count == 1)
        {
            DataContext = guestAccess
        };
        GuestSidebarHost.Children.Add(_guestSidebar);
    }

    public void SetCompactLayout(bool compact) => _guestSidebar.SetCompactLayout(compact);

    private void OnPreviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FullscreenPhotoViewModel.CurrentPhoto))
        {
            _grid.SetSelectedPhoto(_preview.CurrentPhoto, scrollIntoView: _preview.Mode == PreviewMode.Manual);
        }
    }

    // PreviewKeyDown (tunelowanie) zamiast KeyDown: bez tego strzałki przechwytywał ScrollViewer siatki,
    // który dostaje fokus po kliknięciu miniatury — a to najczęstszy stan, zwłaszcza na jednym ekranie.
    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
        => await ((App)Application.Current).HandleShortcutAsync(this, e);
}
