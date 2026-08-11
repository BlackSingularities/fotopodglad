using System.Windows;
using System.Windows.Controls;
using Fotopodglad.Configuration;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Fotopodglad.Views;
using Xunit;

namespace Fotopodglad.Tests.Views;

/// <summary>
/// Okno ustawień jest budowane w całości w konstruktorze (ComboBoxy, sekcja pobierania, diagnostyka),
/// więc samo jego utworzenie wyłapuje brakujące zasoby XAML, literówki w nazwach kontrolek i wyjątki
/// z inicjalizacji — awarie, które w aplikacji kończą się zamknięciem procesu.
/// </summary>
public sealed class SettingsWindowTests
{
    [Fact]
    public void Constructor_BuildsEveryTab_AndFillsGuestDownloadSection()
    {
        var settings = new AppSettings
        {
            WatchedFolderPath = Path.GetTempPath(),
            GuestDownloadLongestEdge = 2048,
            GuestDownloadConvertToJpeg = true,
            GuestDownloadJpegQuality = 85
        };

        WpfTestHost.Run(() =>
        {
            var window = new SettingsWindow(settings, [Screen(0, primary: true), Screen(1)]);

            var tabs = (TabControl)window.FindName("SettingsTabs")!;
            Assert.Equal(7, tabs.Items.Count);

            var size = (ComboBox)window.FindName("GuestDownloadSizeComboBox")!;
            var format = (ComboBox)window.FindName("GuestDownloadFormatComboBox")!;
            var quality = (Slider)window.FindName("GuestDownloadQualitySlider")!;
            var summary = (TextBlock)window.FindName("GuestDownloadSummaryTextBlock")!;

            Assert.Equal(Array.IndexOf(GuestDownloadOptions.AvailableLongestEdges, 2048), size.SelectedIndex);
            Assert.Equal(1, format.SelectedIndex);
            Assert.Equal(85, quality.Value);
            Assert.Contains("2048", summary.Text);
            // Skalowanie wymusza JPEG, więc wybór formatu jest zablokowany.
            Assert.False(format.IsEnabled);
        });
    }

    [Fact]
    public void Constructor_LeavesFormatSelectable_WhenGuestGetsTheOriginalFile()
    {
        var settings = new AppSettings { WatchedFolderPath = Path.GetTempPath() };

        WpfTestHost.Run(() =>
        {
            var window = new SettingsWindow(settings, [Screen(0, primary: true)]);

            var format = (ComboBox)window.FindName("GuestDownloadFormatComboBox")!;
            var qualityPanel = (StackPanel)window.FindName("GuestDownloadQualityPanel")!;
            var summary = (TextBlock)window.FindName("GuestDownloadSummaryTextBlock")!;

            Assert.Equal(0, format.SelectedIndex);
            Assert.True(format.IsEnabled);
            Assert.False(qualityPanel.IsEnabled); // oryginalny plik nie jest kompresowany
            Assert.Contains("bajt w bajt", summary.Text);
        });
    }

    [Fact]
    public void Localization_TranslatesEveryTabHeaderToEnglish()
    {
        var settings = new AppSettings { WatchedFolderPath = Path.GetTempPath(), Language = LanguageMode.English };

        WpfTestHost.Run(() =>
        {
            var window = new SettingsWindow(settings, [Screen(0, primary: true)]);

            // To samo wywołanie, które okno wykonuje w zdarzeniu Loaded — kiedyś kończyło się
            // wyjątkiem z inicjalizatora słownika i zamknięciem aplikacji.
            LocalizationService.Apply(window, settings);

            var headers = ((TabControl)window.FindName("SettingsTabs")!).Items
                .Cast<TabItem>()
                .Select(tab => tab.Header as string)
                .ToList();

            Assert.Equal(
                ["Displays", "Folder and gallery", "Preview", "Sharing", "Appearance", "Diagnostics", "About"],
                headers);
        });
    }

    private static ScreenInfo Screen(int index, bool primary = false) => new()
    {
        DeviceName = $@"\\.\DISPLAY{index + 1}",
        Left = index * 1920,
        Top = 0,
        Width = 1920,
        Height = 1080,
        IsPrimary = primary
    };
}
