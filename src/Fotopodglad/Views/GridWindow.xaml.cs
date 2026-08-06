using System.Windows;
using Fotopodglad.Controls;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Fotopodglad.ViewModels;

namespace Fotopodglad.Views;

public partial class GridWindow : Window
{
    private readonly GridWindowViewModel _viewModel;

    public GridWindow(GridWindowViewModel viewModel, IThumbnailCache thumbnailCache, GuestAccessCoordinator guestAccess)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        var grid = new MasonryGridControl();
        grid.Initialize(thumbnailCache, viewModel.Photos);
        grid.PhotoClicked += photo => viewModel.OnPhotoClicked(photo);
        GridHost.Children.Add(grid);

        var guestSidebar = new GuestAccessSidebar { DataContext = guestAccess };
        GuestSidebarHost.Children.Add(guestSidebar);
    }
}
