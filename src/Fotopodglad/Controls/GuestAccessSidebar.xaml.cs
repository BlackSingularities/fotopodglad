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
        ApplyQrSize(settings.QrCodeSize);
        settings.Changed += (_, _) => ApplyQrSize(settings.QrCodeSize);

        SetCompactLayout(compactLayout);

        DataContextChanged += OnDataContextChanged;
    }

    public void SetCompactLayout(bool compact)
    {
        QrGroupsPanel.Orientation = compact ? Orientation.Horizontal : Orientation.Vertical;
        PhotoQrGroup.Margin = compact ? new Thickness(10, 0, 0, 0) : new Thickness(0, 10, 0, 0);
    }

    private void ApplyQrSize(int size)
    {
        var qrCodeSize = Math.Clamp(size, 96, 320);
        WifiQrImage.Width = qrCodeSize;
        WifiQrImage.Height = qrCodeSize;
        PhotoQrImage.Width = qrCodeSize;
        PhotoQrImage.Height = qrCodeSize;
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
        RootBorder.Visibility = vm.WifiQrImage is not null ? Visibility.Visible : Visibility.Collapsed;
        WifiQrImage.Source = vm.WifiQrImage;
        PhotoQrImage.Source = vm.PhotoQrImage;
    }
}
