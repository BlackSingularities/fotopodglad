using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Navigation;
using Fotopodglad.Configuration;
using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Microsoft.Win32;

namespace Fotopodglad.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<ScreenInfo> _screens;

    public SettingsWindow(AppSettings settings, IReadOnlyList<ScreenInfo> screens)
    {
        InitializeComponent();
        _settings = settings;
        _screens = screens;
        Title = ApplicationVersion.CreateWindowTitle("ustawienia");
        AppNameTextBlock.Text = ApplicationVersion.ProductName;
        AboutAppNameTextBlock.Text = ApplicationVersion.ProductName;

        var screenLabels = _screens
            .Select((screen, index) => $"Ekran {index + 1} — {screen.Width}×{screen.Height}{(screen.IsPrimary ? " (główny)" : "")}")
            .ToList();

        MainViewScreenComboBox.ItemsSource = screenLabels;
        GridScreenComboBox.ItemsSource = screenLabels;
        MainViewScreenComboBox.SelectedIndex = ClampScreenIndex(settings.MainViewScreenIndex);
        GridScreenComboBox.SelectedIndex = ClampScreenIndex(settings.GridScreenIndex);

        if (_screens.Count > 1 && GridScreenComboBox.SelectedIndex == MainViewScreenComboBox.SelectedIndex)
        {
            GridScreenComboBox.SelectedIndex = (MainViewScreenComboBox.SelectedIndex + 1) % _screens.Count;
        }

        if (_screens.Count <= 1)
        {
            MainViewScreenComboBox.IsEnabled = false;
            GridScreenComboBox.IsEnabled = false;
        }

        WifiSsidTextBox.Text = settings.WifiSsid ?? string.Empty;
        WifiPasswordBox.Password = settings.WifiPassphrase ?? string.Empty;
        WatchedFolderTextBox.Text = settings.WatchedFolderPath ?? string.Empty;
        GuestAccessEnabledCheckBox.IsChecked = settings.GuestAccessEnabled;
        ShowGuestInstructionsCheckBox.IsChecked = settings.ShowGuestInstructions;
        ShowPhotoParametersCheckBox.IsChecked = settings.ShowPhotoParameters;
        GridColumnsTextBox.Text = settings.GridColumnCount.ToString(CultureInfo.InvariantCulture);
        ManualHoldTextBox.Text = settings.ManualHoldSeconds.ToString(CultureInfo.InvariantCulture);
        QrSizeSlider.Value = Math.Clamp(settings.QrCodeSize, 96, 320);
    }

    private int ClampScreenIndex(int index) => Math.Clamp(index, 0, Math.Max(0, _screens.Count - 1));

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var wifiSsid = WifiSsidTextBox.Text.Trim();
        var wifiPassphrase = WifiPasswordBox.Password.Trim();
        var watchedFolder = WatchedFolderTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(watchedFolder) || !Directory.Exists(watchedFolder))
        {
            MessageBox.Show(this, "Wybierz istniejący folder ze zdjęciami.", "Nieprawidłowy folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_screens.Count > 1 && MainViewScreenComboBox.SelectedIndex == GridScreenComboBox.SelectedIndex)
        {
            MessageBox.Show(this, "Wybierz różne ekrany dla podglądu i siatki zdjęć.", "Te same ekrany", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (wifiSsid.Length > 32)
        {
            MessageBox.Show(this, "Nazwa sieci WiFi może mieć maksymalnie 32 znaki.", "Nieprawidłowa wartość", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (wifiPassphrase.Length is > 0 and (< 8 or > 63))
        {
            MessageBox.Show(this, "Hasło WiFi musi mieć od 8 do 63 znaków albo pozostać puste (wtedy zostanie wygenerowane).", "Nieprawidłowa wartość", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(GridColumnsTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var columns) || columns is < 2 or > 12)
        {
            MessageBox.Show(this, "Liczba kolumn siatki musi być liczbą całkowitą od 2 do 12.", "Nieprawidłowa wartość", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(ManualHoldTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var holdSeconds) ||
            holdSeconds is < AppSettings.MinManualHoldSeconds or > AppSettings.MaxManualHoldSeconds)
        {
            MessageBox.Show(this, "Czas podglądu musi być liczbą od 3 do 900 sekund.", "Nieprawidłowa wartość", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.GuestAccessEnabled = GuestAccessEnabledCheckBox.IsChecked == true;
        _settings.ShowGuestInstructions = ShowGuestInstructionsCheckBox.IsChecked == true;
        _settings.WatchedFolderPath = Path.GetFullPath(watchedFolder);
        _settings.ShowPhotoParameters = ShowPhotoParametersCheckBox.IsChecked == true;
        _settings.WifiSsid = string.IsNullOrWhiteSpace(wifiSsid) ? null : wifiSsid;
        _settings.WifiPassphrase = string.IsNullOrWhiteSpace(wifiPassphrase) ? null : wifiPassphrase;
        _settings.GridColumnCount = columns;
        _settings.ManualHoldSeconds = holdSeconds;
        _settings.QrCodeSize = (int)Math.Round(QrSizeSlider.Value);
        _settings.MainViewScreenIndex = MainViewScreenComboBox.SelectedIndex;
        _settings.GridScreenIndex = GridScreenComboBox.SelectedIndex;

        DialogResult = true;
        Close();
    }

    private void OnQrSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QrSizeValueTextBlock is not null)
        {
            QrSizeValueTextBlock.Text = $"{Math.Round(e.NewValue):0}";
        }
    }

    private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz folder, z którego Fotopodgląd ma wczytywać zdjęcia",
            Multiselect = false
        };

        if (Directory.Exists(WatchedFolderTextBox.Text))
        {
            dialog.InitialDirectory = WatchedFolderTextBox.Text;
        }

        if (dialog.ShowDialog(this) == true)
        {
            WatchedFolderTextBox.Text = dialog.FolderName;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnRepoLinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }

        e.Handled = true;
    }
}
