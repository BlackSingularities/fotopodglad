using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using Fotopodglad.Configuration;
using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Microsoft.Win32;

namespace Fotopodglad.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<ScreenInfo> _screens;
    private readonly GuestAccessCoordinator? _guestAccess;
    private readonly UpdateCheckService _updateChecker;
    private AppSettings _draft;

    public SettingsWindow(
        AppSettings settings,
        IReadOnlyList<ScreenInfo> screens,
        GuestAccessCoordinator? guestAccess = null,
        UpdateCheckService? updateChecker = null)
    {
        InitializeComponent();
        _settings = settings;
        _draft = settings.Clone();
        _screens = screens;
        _guestAccess = guestAccess;
        _updateChecker = updateChecker ?? new UpdateCheckService();
        FontSize = 12 * AppearanceService.ScaleFactor(_draft);

        Title = ApplicationVersion.CreateWindowTitle("ustawienia");
        AppNameTextBlock.Text = ApplicationVersion.ProductName;
        AboutAppNameTextBlock.Text = ApplicationVersion.ProductName;
        ConfigureLists();
        PopulateControls();

        Loaded += (_, _) => DrawScreenPreview();
        Loaded += (_, _) => LocalizationService.Apply(this, _draft);
        ScreenPreviewCanvas.SizeChanged += (_, _) => DrawScreenPreview();
        if (_guestAccess is not null)
        {
            _guestAccess.PropertyChanged += OnGuestAccessPropertyChanged;
        }

        RefreshDiagnostics();
        Closed += (_, _) =>
        {
            if (_guestAccess is not null)
            {
                _guestAccess.PropertyChanged -= OnGuestAccessPropertyChanged;
            }
        };
    }

    private void ConfigureLists()
    {
        var english = AppearanceService.UseEnglish(_draft);
        WindowModeComboBox.ItemsSource = english ? new[] { "Fullscreen", "Windowed" } : new[] { "Pełny ekran", "Tryb okienkowy" };
        HistogramComboBox.ItemsSource = english ? new[] { "Off", "Luminance", "RGB" } : new[] { "Wyłączony", "Jasność", "RGB" };
        PhotoFilterComboBox.ItemsSource = english ? new[] { "All", "Today", "Latest 10", "Latest 50", "Flagged", "Date range" } : new[] { "Wszystkie", "Dzisiejsze", "Ostatnie 10", "Ostatnie 50", "Oznaczone", "Przedział czasu" };
        ThemeComboBox.ItemsSource = english ? new[] { "Automatic", "Dark", "Light" } : new[] { "Automatyczny", "Ciemny", "Jasny" };
        UiScaleComboBox.ItemsSource = english ? new[] { "Small", "Normal", "Large" } : new[] { "Mała", "Normalna", "Duża" };
        LanguageComboBox.ItemsSource = new[] { english ? "Automatic" : "Automatyczny", "Polski", "English" };

        var screenLabels = _screens.Select((screen, index) =>
            $"Ekran {index + 1} — {screen.Width}×{screen.Height}{(screen.IsPrimary ? " (główny)" : string.Empty)}").ToList();
        MainViewScreenComboBox.ItemsSource = screenLabels;
        GridScreenComboBox.ItemsSource = screenLabels;
        MainViewScreenComboBox.SelectionChanged += (_, _) => DrawScreenPreview();
        GridScreenComboBox.SelectionChanged += (_, _) => DrawScreenPreview();
        MainViewScreenComboBox.IsEnabled = _screens.Count > 1;
        GridScreenComboBox.IsEnabled = _screens.Count > 1;
    }

    private void PopulateControls()
    {
        MainViewScreenComboBox.SelectedIndex = ClampScreenIndex(_draft.MainViewScreenIndex);
        GridScreenComboBox.SelectedIndex = ClampScreenIndex(_draft.GridScreenIndex);
        WindowModeComboBox.SelectedIndex = (int)_draft.WindowMode;
        WatchedFolderTextBox.Text = _draft.WatchedFolderPath ?? string.Empty;
        ShowPhotoParametersCheckBox.IsChecked = _draft.ShowPhotoParameters;
        ShowClippingWarningsCheckBox.IsChecked = _draft.ShowClippingWarnings;
        GridColumnsTextBox.Text = _draft.GridColumnCount.ToString(CultureInfo.InvariantCulture);
        ManualHoldTextBox.Text = _draft.ManualHoldSeconds.ToString(CultureInfo.InvariantCulture);
        HistogramComboBox.SelectedIndex = (int)_draft.Histogram;
        PhotoFilterComboBox.SelectedIndex = (int)_draft.PhotoFilter;
        FilterFromDatePicker.SelectedDate = _draft.FilterDateFrom;
        FilterToDatePicker.SelectedDate = _draft.FilterDateTo;
        ThumbnailCacheTextBox.Text = _draft.ThumbnailCacheMegabytes.ToString(CultureInfo.InvariantCulture);
        ExifTextSizeSlider.Value = _draft.ExifTextSize;
        GuestAccessEnabledCheckBox.IsChecked = _draft.GuestAccessEnabled;
        ShowGuestInstructionsCheckBox.IsChecked = _draft.ShowGuestInstructions;
        WifiSsidTextBox.Text = _draft.WifiSsid ?? string.Empty;
        WifiPasswordBox.Password = _draft.WifiPassphrase ?? string.Empty;
        QrSizeSlider.Value = Math.Clamp(_draft.QrCodeSize, 96, 320);
        MaxDownloadsTextBox.Text = _draft.MaxConcurrentDownloads.ToString(CultureInfo.InvariantCulture);
        InstructionTextSizeSlider.Value = _draft.InstructionTextSize;
        CheckForUpdatesCheckBox.IsChecked = _draft.CheckForUpdates;
        ThemeComboBox.SelectedIndex = (int)_draft.Theme;
        UiScaleComboBox.SelectedIndex = (int)_draft.UiScale;
        LanguageComboBox.SelectedIndex = (int)_draft.Language;
        RefreshDiagnostics();
    }

    private int ClampScreenIndex(int index) => Math.Clamp(index, 0, Math.Max(0, _screens.Count - 1));

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var folder = WatchedFolderTextBox.Text.Trim();
        var ssid = WifiSsidTextBox.Text.Trim();
        var password = WifiPasswordBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Warn("Wybierz istniejący i dostępny folder ze zdjęciami.", "Nieprawidłowy folder");
            return;
        }

        if (_screens.Count > 1 && WindowModeComboBox.SelectedIndex == (int)WindowPresentationMode.Fullscreen &&
            MainViewScreenComboBox.SelectedIndex == GridScreenComboBox.SelectedIndex)
        {
            Warn("Wybierz różne ekrany dla podglądu i siatki zdjęć.", "Te same ekrany");
            return;
        }

        if (ssid.Length > 32 || password.Length is > 0 and (< 8 or > 63))
        {
            Warn("Nazwa Wi‑Fi może mieć do 32 znaków, a hasło od 8 do 63 znaków (lub pozostaw je puste).", "Nieprawidłowe Wi‑Fi");
            return;
        }

        if (!TryInt(GridColumnsTextBox, 2, 12, "Liczba kolumn", out var columns) ||
            !TryDouble(ManualHoldTextBox, AppSettings.MinManualHoldSeconds, AppSettings.MaxManualHoldSeconds, "Czas podglądu", out var hold) ||
            !TryInt(ThumbnailCacheTextBox, 64, 2048, "Limit cache", out var cache) ||
            !TryInt(MaxDownloadsTextBox, 1, 20, "Limit pobrań", out var downloads))
        {
            return;
        }
        if (FilterFromDatePicker.SelectedDate is { } from && FilterToDatePicker.SelectedDate is { } to && from.Date > to.Date)
        {
            Warn("Data początkowa nie może być późniejsza niż końcowa.", "Nieprawidłowy przedział");
            return;
        }

        _draft.WatchedFolderPath = Path.GetFullPath(folder);
        _draft.WindowMode = (WindowPresentationMode)Math.Max(0, WindowModeComboBox.SelectedIndex);
        _draft.MainViewScreenIndex = Math.Max(0, MainViewScreenComboBox.SelectedIndex);
        _draft.GridScreenIndex = Math.Max(0, GridScreenComboBox.SelectedIndex);
        _draft.ShowPhotoParameters = ShowPhotoParametersCheckBox.IsChecked == true;
        _draft.ShowClippingWarnings = ShowClippingWarningsCheckBox.IsChecked == true;
        _draft.GridColumnCount = columns;
        _draft.ManualHoldSeconds = hold;
        _draft.Histogram = (HistogramMode)Math.Max(0, HistogramComboBox.SelectedIndex);
        _draft.PhotoFilter = (PhotoFilterMode)Math.Max(0, PhotoFilterComboBox.SelectedIndex);
        _draft.FilterDateFrom = FilterFromDatePicker.SelectedDate;
        _draft.FilterDateTo = FilterToDatePicker.SelectedDate;
        _draft.ThumbnailCacheMegabytes = cache;
        _draft.ExifTextSize = ExifTextSizeSlider.Value;
        _draft.GuestAccessEnabled = GuestAccessEnabledCheckBox.IsChecked == true;
        _draft.ShowGuestInstructions = ShowGuestInstructionsCheckBox.IsChecked == true;
        _draft.WifiSsid = string.IsNullOrWhiteSpace(ssid) ? null : ssid;
        _draft.WifiPassphrase = string.IsNullOrWhiteSpace(password) ? null : password;
        _draft.QrCodeSize = (int)Math.Round(QrSizeSlider.Value);
        _draft.MaxConcurrentDownloads = downloads;
        _draft.InstructionTextSize = InstructionTextSizeSlider.Value;
        _draft.CheckForUpdates = CheckForUpdatesCheckBox.IsChecked == true;
        _draft.Theme = (ThemeMode)Math.Max(0, ThemeComboBox.SelectedIndex);
        _draft.UiScale = (UiScaleMode)Math.Max(0, UiScaleComboBox.SelectedIndex);
        _draft.Language = (LanguageMode)Math.Max(0, LanguageComboBox.SelectedIndex);
        _settings.CopyFrom(_draft);
        DialogResult = true;
    }

    private bool TryInt(TextBox input, int min, int max, string name, out int value)
    {
        if (int.TryParse(input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= min && value <= max)
        {
            return true;
        }
        Warn($"{name}: wpisz liczbę od {min} do {max}.", "Nieprawidłowa wartość");
        return false;
    }

    private bool TryDouble(TextBox input, double min, double max, string name, out double value)
    {
        if (double.TryParse(input.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= min && value <= max)
        {
            return true;
        }
        Warn($"{name}: wpisz liczbę od {min:0} do {max:0}.", "Nieprawidłowa wartość");
        return false;
    }

    private void OnResetDefaultsClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Przywrócić ustawienia domyślne? Folder zdjęć pozostanie bez zmian.",
                "Przywracanie ustawień", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        _draft = new AppSettings { WatchedFolderPath = WatchedFolderTextBox.Text.Trim() };
        PopulateControls();
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        UpdateStatusTextBlock.Text = "Sprawdzanie…";
        try
        {
            var result = await _updateChecker.CheckAsync();
            UpdateStatusTextBlock.Text = result.IsUpdateAvailable
                ? $"Dostępna jest wersja {result.LatestVersion}. Kliknij, aby otworzyć wydanie."
                : $"Masz najnowszą wersję ({ApplicationVersion.DisplayVersion}).";
            if (result.IsUpdateAvailable && MessageBox.Show(this, "Otworzyć stronę pobierania nowej wersji?", "Dostępna aktualizacja", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(result.DownloadUrl) { UseShellExecute = true });
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            UpdateStatusTextBlock.Text = "Nie udało się połączyć z GitHubem. Spróbuj ponownie później.";
        }
    }

    private void OnGuestAccessPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        Dispatcher.BeginInvoke(RefreshDiagnostics);

    private void RefreshDiagnostics()
    {
        if (DiagnosticStatusTextBlock is null) return;
        DiagnosticFolderTextBlock.Text = Directory.Exists(WatchedFolderTextBox?.Text)
            ? "Folder jest dostępny. Obserwowanie działa lub zostanie automatycznie wznowione."
            : "Folder jest obecnie niedostępny. Aplikacja będzie ponawiać obserwowanie po jego powrocie.";
        if (_guestAccess is null)
        {
            DiagnosticStatusTextBlock.Text = "Uruchom aplikację, aby zobaczyć stan na żywo";
            DiagnosticIpTextBlock.Text = "—";
            DiagnosticRetryTextBlock.Text = "—";
            DiagnosticDownloadsTextBlock.Text = "0";
            DiagnosticTransitionTextBlock.Text = "—";
            DiagnosticErrorBorder.Visibility = Visibility.Collapsed;
            return;
        }

        DiagnosticStatusTextBlock.Text = _guestAccess.StatusMessage;
        DiagnosticIpTextBlock.Text = _guestAccess.LocalIpAddress ?? "—";
        DiagnosticRetryTextBlock.Text = _guestAccess.RetryAttempt == 0 ? "brak" : $"{_guestAccess.RetryAttempt}/3";
        DiagnosticDownloadsTextBlock.Text = _guestAccess.ActiveDownloads.ToString(CultureInfo.InvariantCulture);
        DiagnosticTransitionTextBlock.Text = _guestAccess.LastTransitionUtc.ToLocalTime().ToString("G");
        DiagnosticErrorBorder.Visibility = string.IsNullOrWhiteSpace(_guestAccess.FailureReason) ? Visibility.Collapsed : Visibility.Visible;
        DiagnosticErrorTextBlock.Text = _guestAccess.FailureReason ?? string.Empty;
    }

    private void DrawScreenPreview()
    {
        if (ScreenPreviewCanvas is null || _screens.Count == 0 || ScreenPreviewCanvas.ActualWidth <= 0) return;
        ScreenPreviewCanvas.Children.Clear();
        var minX = _screens.Min(s => s.Left); var minY = _screens.Min(s => s.Top);
        var maxX = _screens.Max(s => s.Left + s.Width); var maxY = _screens.Max(s => s.Top + s.Height);
        var scale = Math.Min((ScreenPreviewCanvas.ActualWidth - 4) / Math.Max(1, maxX - minX),
            (ScreenPreviewCanvas.ActualHeight - 4) / Math.Max(1, maxY - minY));
        for (var i = 0; i < _screens.Count; i++)
        {
            var screen = _screens[i];
            var fill = i == MainViewScreenComboBox.SelectedIndex ? Color.FromRgb(210, 139, 24)
                : i == GridScreenComboBox.SelectedIndex ? Color.FromRgb(60, 135, 210) : Color.FromRgb(95, 95, 105);
            var border = new Border
            {
                Width = Math.Max(24, screen.Width * scale), Height = Math.Max(16, screen.Height * scale),
                Background = new SolidColorBrush(fill), BorderBrush = Brushes.White, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3), Child = new TextBlock { Text = (i + 1).ToString(), Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            Canvas.SetLeft(border, 2 + (screen.Left - minX) * scale); Canvas.SetTop(border, 2 + (screen.Top - minY) * scale);
            ScreenPreviewCanvas.Children.Add(border);
        }
    }

    private void OnQrSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QrSizeValueTextBlock is not null) QrSizeValueTextBlock.Text = $"{Math.Round(e.NewValue):0} px";
    }

    private void OnBrowseFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Wybierz folder ze zdjęciami", Multiselect = false };
        if (Directory.Exists(WatchedFolderTextBox.Text)) dialog.InitialDirectory = WatchedFolderTextBox.Text;
        if (dialog.ShowDialog(this) == true)
        {
            WatchedFolderTextBox.Text = dialog.FolderName;
            RefreshDiagnostics();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Warn(string message, string title) => MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    private void OnRepoLinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
        e.Handled = true;
    }
}
