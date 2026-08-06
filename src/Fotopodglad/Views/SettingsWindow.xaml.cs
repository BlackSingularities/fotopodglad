using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Navigation;
using Fotopodglad.Configuration;

namespace Fotopodglad.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        WifiSsidTextBox.Text = settings.WifiSsid ?? string.Empty;
        WifiPasswordTextBox.Text = settings.WifiPassphrase ?? string.Empty;
        GridColumnsTextBox.Text = settings.GridColumnCount.ToString(CultureInfo.InvariantCulture);
        ManualHoldTextBox.Text = settings.ManualHoldSeconds.ToString(CultureInfo.InvariantCulture);

        AuthorTextBlock.Text = "Adam Rędzikowski";
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
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

        _settings.WifiSsid = string.IsNullOrWhiteSpace(WifiSsidTextBox.Text) ? null : WifiSsidTextBox.Text.Trim();
        _settings.WifiPassphrase = string.IsNullOrWhiteSpace(WifiPasswordTextBox.Text) ? null : WifiPasswordTextBox.Text.Trim();
        _settings.GridColumnCount = columns;
        _settings.ManualHoldSeconds = holdSeconds;

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
