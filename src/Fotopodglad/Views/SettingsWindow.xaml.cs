using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Navigation;
using Fotopodglad.Configuration;
using Fotopodglad.Models;

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

        var screenLabels = _screens
            .Select((screen, index) => $"Ekran {index + 1} — {screen.Width}×{screen.Height}{(screen.IsPrimary ? " (główny)" : "")}")
            .ToList();

        MainViewScreenComboBox.ItemsSource = screenLabels;
        GridScreenComboBox.ItemsSource = screenLabels;
        MainViewScreenComboBox.SelectedIndex = ClampScreenIndex(settings.MainViewScreenIndex);
        GridScreenComboBox.SelectedIndex = ClampScreenIndex(settings.GridScreenIndex);

        if (_screens.Count <= 1)
        {
            MainViewScreenComboBox.IsEnabled = false;
            GridScreenComboBox.IsEnabled = false;
        }

        WifiSsidTextBox.Text = settings.WifiSsid ?? string.Empty;
        WifiPasswordTextBox.Text = settings.WifiPassphrase ?? string.Empty;
        GridColumnsTextBox.Text = settings.GridColumnCount.ToString(CultureInfo.InvariantCulture);
        ManualHoldTextBox.Text = settings.ManualHoldSeconds.ToString(CultureInfo.InvariantCulture);

        AuthorTextBlock.Text = "Adam Rędzikowski";
    }

    private int ClampScreenIndex(int index) => Math.Clamp(index, 0, Math.Max(0, _screens.Count - 1));

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var wifiSsid = WifiSsidTextBox.Text.Trim();
        var wifiPassphrase = WifiPasswordTextBox.Text.Trim();

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

        if (!double.TryParse(ManualHoldTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var holdSeconds) || holdSeconds is < 3 or > 120)
        {
            MessageBox.Show(this, "Czas podglądu musi być liczbą od 3 do 120 sekund.", "Nieprawidłowa wartość", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.WifiSsid = string.IsNullOrWhiteSpace(wifiSsid) ? null : wifiSsid;
        _settings.WifiPassphrase = string.IsNullOrWhiteSpace(wifiPassphrase) ? null : wifiPassphrase;
        _settings.GridColumnCount = columns;
        _settings.ManualHoldSeconds = holdSeconds;
        _settings.MainViewScreenIndex = MainViewScreenComboBox.SelectedIndex;
        _settings.GridScreenIndex = GridScreenComboBox.SelectedIndex;

        DialogResult = true;
        Close();
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
