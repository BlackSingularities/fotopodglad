using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Fotopodglad.Configuration;
using Fotopodglad.Models;
using Fotopodglad.Services.GuestGallery;

namespace Fotopodglad.Controls;

public partial class GuestAccessSidebar : UserControl
{
    public GuestAccessSidebar(AppSettings settings, bool compactLayout = false)
    {
        InitializeComponent();
        var qrCodeSize = Math.Clamp(settings.QrCodeSize, 96, 320);
        WifiQrImage.Width = qrCodeSize;
        WifiQrImage.Height = qrCodeSize;
        PhotoQrImage.Width = qrCodeSize;
        PhotoQrImage.Height = qrCodeSize;

        if (compactLayout)
        {
            QrGroupsPanel.Orientation = Orientation.Horizontal;
            PhotoQrGroup.Margin = new Thickness(18, 0, 0, 0);
        }

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
