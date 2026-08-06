using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Fotopodglad.Models;
using Fotopodglad.Services.GuestGallery;

namespace Fotopodglad.Controls;

public partial class GuestAccessSidebar : UserControl
{
    public GuestAccessSidebar()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is GuestAccessCoordinator oldVm)
        {
            oldVm.PropertyChanged -= OnCoordinatorPropertyChanged;
        }

        if (e.NewValue is GuestAccessCoordinator newVm)
        {
            newVm.PropertyChanged += OnCoordinatorPropertyChanged;
            Refresh(newVm);
        }
    }

    private void OnCoordinatorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is GuestAccessCoordinator vm)
        {
            Refresh(vm);
        }
    }

    private void Refresh(GuestAccessCoordinator vm)
    {
        RootBorder.Visibility = vm.Status == GuestAccessStatus.Active ? Visibility.Visible : Visibility.Collapsed;
        WifiQrImage.Source = vm.WifiQrImage;
        PhotoQrImage.Source = vm.PhotoQrImage;
    }
}
