using System.Windows;
using Fotopodglad.Configuration;
using Fotopodglad.Controls;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Fotopodglad.ViewModels;

namespace Fotopodglad.Views;

public partial class GridWindow : Window
{
    private readonly GridWindowViewModel _viewModel;

    public GridWindow(GridWindowViewModel viewModel, IThumbnailCache thumbnailCache, GuestAccessCoordinator guestAccess, AppSettings settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        var grid = new MasonryGridControl();
        grid.Initialize(thumbnailCache, viewModel.Photos, settings.GridColumnCount);
        grid.PhotoClicked += photo => viewModel.OnPhotoClicked(photo);
        GridHost.Children.Add(grid);

        var guestSidebar = new GuestAccessSidebar { DataContext = guestAccess };
        GuestSidebarHost.Children.Add(guestSidebar);
    }
}
