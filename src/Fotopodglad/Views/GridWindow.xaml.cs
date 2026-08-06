using System.Windows;
using System.Windows.Input;
using Fotopodglad.Configuration;
using Fotopodglad.Controls;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Fotopodglad.ViewModels;

namespace Fotopodglad.Views;

public partial class GridWindow : Window
{
    public GridWindow(
        GridWindowViewModel viewModel,
        IThumbnailCache thumbnailCache,
        GuestAccessCoordinator guestAccess,
        AppSettings settings,
        IScreenService screenService)
    {
        InitializeComponent();
        DataContext = viewModel;

        var grid = new MasonryGridControl();
        grid.Initialize(thumbnailCache, viewModel.Photos, settings.GridColumnCount);
        grid.PhotoClicked += photo => viewModel.OnPhotoClicked(photo);
        GridHost.Children.Add(grid);

        GuestInstructionBorder.DataContext = guestAccess;
        if (!settings.ShowGuestInstructions)
        {
            // Lokalna wartość ma pierwszeństwo przed triggerem Status=Active i trwale ukrywa pasek.
            GuestInstructionBorder.Visibility = Visibility.Collapsed;
        }

        var guestSidebar = new GuestAccessSidebar(settings, compactLayout: screenService.GetScreens().Count == 1)
        {
            DataContext = guestAccess
        };
        GuestSidebarHost.Children.Add(guestSidebar);
    }

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.P && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            await ((App)Application.Current).OpenSettingsAsync(this);
        }
    }
}
