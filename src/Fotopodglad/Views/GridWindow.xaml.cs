using System.Windows;
using System.Windows.Input;
using Fotopodglad.Configuration;
using Fotopodglad.Controls;
using Fotopodglad.Helpers;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Fotopodglad.ViewModels;

namespace Fotopodglad.Views;

public partial class GridWindow : Window
{
    private readonly GuestAccessSidebar _guestSidebar;
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

        var grid = new MasonryGridControl();
        grid.Initialize(thumbnailCache, viewModel.Photos, settings.GridColumnCount);
        grid.PhotoClicked += photo => viewModel.OnPhotoClicked(photo);
        grid.PhotoFlagToggled += viewModel.ToggleFlag;
        GridHost.Children.Add(grid);
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

    private async void OnKeyDown(object sender, KeyEventArgs e)
        => await ((App)Application.Current).HandleShortcutAsync(this, e);
}
