using System.Diagnostics;
using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Fotopodglad.Configuration;
using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Fotopodglad.ViewModels;
using Fotopodglad.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Fotopodglad;

public partial class App : Application
{
    private ServiceProvider? _services;
    private bool _settingsDialogOpen;
    private bool _shutdownInProgress;
    private bool _synchronizingWindowState;
    private AppSettings? _settings;
    private MainViewWindow? _mainViewWindow;
    private GridWindow? _gridWindow;
    private int _lastScreenCount;
    private bool _forcedSingleScreenLayout;
    private Mutex? _singleInstanceMutex;

    public static ServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var isRestart = e.Args.Any(arg => string.Equals(arg, "--restart", StringComparison.OrdinalIgnoreCase));
        if (!AcquireSingleInstance(isRestart))
        {
            Shutdown();
            return;
        }

        // Podczas startu kolejno zamykają się modalne okna wyboru folderu i ustawień.
        // Nie mogą one zakończyć aplikacji jako "ostatnie okno", zanim pokażą się oba okna główne.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settings = AppSettings.Load();
        _settings = settings;
        AppearanceService.Apply(settings);
        // Zapisany i nadal dostępny folder jest używany bez ponownego otwierania systemowego selektora.
        // Okno wyboru pokazujemy wyłącznie przy pierwszym uruchomieniu albo po utracie zapisanej ścieżki.
        var folderPath = settings.WatchedFolderPath is { } savedFolder && Directory.Exists(savedFolder)
            ? savedFolder
            : PromptForFolder(settings);
        if (folderPath is null)
        {
            Shutdown();
            return;
        }

        settings.WatchedFolderPath = folderPath;

        // Lista ekranów potrzebna już tutaj (przed zbudowaniem kontenera DI), żeby okno ustawień
        // mogło zaoferować wybór konkretnego monitora dla każdego z dwóch okien aplikacji.
        var screens = new ScreenService().GetScreens();
        _lastScreenCount = screens.Count;

        var wantsToChangeSettings = !isRestart && PromptToChangeSettings();

        if (wantsToChangeSettings)
        {
            var settingsWindow = new SettingsWindow(settings, screens);
            if (settingsWindow.ShowDialog() == true &&
                settings.WatchedFolderPath is { } configuredFolder && Directory.Exists(configuredFolder))
            {
                folderPath = configuredFolder;
            }
        }

        settings.Save();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection, settings);
        _services = serviceCollection.BuildServiceProvider();
        Services = _services;

        var library = _services.GetRequiredService<IPhotoLibraryService>();
        library.Start(folderPath);

        // Hotspot jest domyślnie włączony i startuje także wtedy, gdy użytkownik odpowie "Nie"
        // na pytanie o pokazanie ustawień. Można go jawnie wyłączyć przełącznikiem w Ctrl+P.
        var guestAccess = _services.GetRequiredService<GuestAccessCoordinator>();
        if (settings.GuestAccessEnabled)
        {
            _ = guestAccess.StartAsync();
        }

        // Indeksy z ustawień są clampowane na wypadek, gdyby użytkownik wybrał ekran, który w międzyczasie
        // odłączono (np. inny zestaw monitorów niż przy poprzednim zapisie ustawień).
        var mainViewScreenIndex = Math.Clamp(settings.MainViewScreenIndex, 0, screens.Count - 1);
        var gridScreenIndex = Math.Clamp(settings.GridScreenIndex, 0, screens.Count - 1);
        if (screens.Count > 1 && gridScreenIndex == mainViewScreenIndex)
        {
            gridScreenIndex = (mainViewScreenIndex + 1) % screens.Count;
        }
        var screenA = screens[mainViewScreenIndex];
        var screenB = screens[gridScreenIndex];

        var mainViewWindow = _mainViewWindow = _services.GetRequiredService<MainViewWindow>();
        var gridWindow = _gridWindow = _services.GetRequiredService<GridWindow>();
        ConfigureInitialLayout(mainViewWindow, gridWindow, settings, screenA, screenB, screens.Count);

        mainViewWindow.Closing += OnApplicationWindowClosing;
        gridWindow.Closing += OnApplicationWindowClosing;
        mainViewWindow.StateChanged += OnApplicationWindowStateChanged;
        gridWindow.StateChanged += OnApplicationWindowStateChanged;

        mainViewWindow.Show();
        gridWindow.Show();
        LocalizationService.Apply(mainViewWindow, settings);
        LocalizationService.Apply(gridWindow, settings);
        AppearanceService.ApplyScale(settings);

        if (settings.CheckForUpdates &&
            (settings.LastUpdateCheckUtc is null || DateTime.UtcNow - settings.LastUpdateCheckUtc > TimeSpan.FromHours(24)))
        {
            _ = CheckForUpdatesInBackgroundAsync(mainViewWindow);
        }

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        ShutdownMode = ShutdownMode.OnLastWindowClose;
    }

    private bool AcquireSingleInstance(bool isRestart)
    {
        _singleInstanceMutex = new Mutex(false, @"Local\Fotopodglad.SingleInstance");
        try
        {
            // Podczas kontrolowanego restartu nowy proces czeka, aż poprzedni zwolni hotspot i port.
            if (_singleInstanceMutex.WaitOne(isRestart ? TimeSpan.FromSeconds(12) : TimeSpan.Zero))
            {
                return true;
            }
        }
        catch (AbandonedMutexException)
        {
            return true;
        }

        var owner = CreateTransientDialogOwner();
        try
        {
            owner.Show();
            owner.Activate();
            MessageBox.Show(owner, "Fotopodgląd jest już uruchomiony.", ApplicationVersion.ProductName,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            owner.Close();
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    public async Task OpenSettingsAsync(Window owner)
    {
        if (_settingsDialogOpen || _services is null)
        {
            return;
        }

        _settingsDialogOpen = true;
        try
        {
            var settings = _services.GetRequiredService<AppSettings>();
            var before = settings.Clone();
            var screens = _services.GetRequiredService<IScreenService>().GetScreens();
            var settingsWindow = new SettingsWindow(
                settings,
                screens,
                _services.GetService<GuestAccessCoordinator>(),
                _services.GetService<UpdateCheckService>())
            {
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (settingsWindow.ShowDialog() != true)
            {
                return;
            }

            settings.Save();
            if (AppSettings.RequiresRestart(before, settings))
            {
                await RestartAsync(owner);
            }
            else
            {
                AppearanceService.Apply(settings);
                settings.NotifyChanged();
                ApplyCurrentWindowMode();
                if (_mainViewWindow is not null) LocalizationService.Apply(_mainViewWindow, settings);
                if (_gridWindow is not null) LocalizationService.Apply(_gridWindow, settings);
            }
        }
        finally
        {
            _settingsDialogOpen = false;
        }
    }

    private async Task RestartAsync(Window owner)
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            MessageBox.Show(owner, "Nie udało się ustalić ścieżki aplikacji. Uruchom ją ponownie ręcznie, aby zastosować ustawienia.",
                "Wymagane ponowne uruchomienie", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (_services?.GetService<GuestAccessCoordinator>() is { } guestAccess)
            {
                await guestAccess.StopAsync().WaitAsync(TimeSpan.FromSeconds(8));
            }

            var startInfo = new ProcessStartInfo(executablePath) { UseShellExecute = true };
            startInfo.ArgumentList.Add("--restart");
            Process.Start(startInfo);
            _shutdownInProgress = true;
            Shutdown();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
        {
            MessageBox.Show(owner, "Ustawienia zapisano, ale nie udało się automatycznie uruchomić aplikacji ponownie. Uruchom ją ręcznie.",
                "Nie udało się uruchomić ponownie", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnApplicationWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_shutdownInProgress)
        {
            return;
        }

        e.Cancel = true;
        _shutdownInProgress = true;

        try
        {
            if (_services?.GetService<GuestAccessCoordinator>() is { } guestAccess)
            {
                await guestAccess.StopAsync().WaitAsync(TimeSpan.FromSeconds(8));
            }
        }
        catch (TimeoutException)
        {
            // Nie blokujemy zamknięcia aplikacji bez końca, jeśli sterownik WiFi nie odpowiada.
        }
        finally
        {
            foreach (Window window in Windows.Cast<Window>().ToArray())
            {
                window.Close();
            }

            Shutdown();
        }
    }

    private void OnApplicationWindowStateChanged(object? sender, EventArgs e)
    {
        if (_synchronizingWindowState || sender is not Window changedWindow ||
            changedWindow.WindowState is not (WindowState.Normal or WindowState.Minimized))
        {
            return;
        }

        _synchronizingWindowState = true;
        try
        {
            // Dwa widoki tworzą jedną aplikację. Minimalizacja lub przywrócenie dowolnego z nich
            // powinno objąć oba, zamiast pozostawiać drugi pełnoekranowy widok nad pulpitem.
            foreach (var window in Windows.OfType<Window>()
                         .Where(window => window is MainViewWindow or GridWindow))
            {
                window.WindowState = changedWindow.WindowState;
            }
        }
        finally
        {
            _synchronizingWindowState = false;
        }
    }

    private async Task CheckForUpdatesInBackgroundAsync(Window owner)
    {
        if (_services is null || _settings is null) return;
        try
        {
            var result = await _services.GetRequiredService<UpdateCheckService>().CheckAsync();
            _settings.LastUpdateCheckUtc = DateTime.UtcNow;
            _settings.Save();
            if (result.IsUpdateAvailable)
            {
                var answer = MessageBox.Show(owner,
                    $"Dostępna jest nowa wersja Fotopodglądu: {result.LatestVersion}. Otworzyć stronę pobierania?",
                    "Aktualizacja Fotopodglądu", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (answer == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(result.DownloadUrl) { UseShellExecute = true });
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Kontrola aktualizacji nie może blokować pracy offline.
        }
    }

    public async Task HandleShortcutAsync(Window owner, KeyEventArgs e)
    {
        if (_services is null)
        {
            return;
        }

        var preview = _services.GetRequiredService<FullscreenPhotoViewModel>();
        if (e.Key == Key.P && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            await OpenSettingsAsync(owner);
        }
        else if (e.Key == Key.M && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            SetMainWindowsState(WindowState.Minimized);
        }
        else if (e.Key == Key.Home)
        {
            e.Handled = true;
            preview.ShowLatestAutomatically();
        }
        else if (e.Key == Key.Left)
        {
            e.Handled = true;
            preview.ShowPrevious();
        }
        else if (e.Key == Key.Right)
        {
            e.Handled = true;
            preview.ShowNext();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            preview.ResetZoom();
        }
        else if (e.Key == Key.F11 && _settings is not null)
        {
            e.Handled = true;
            if (_settings.WindowMode == WindowPresentationMode.Windowed)
            {
                SaveWindowPlacements();
            }

            _settings.WindowMode = _settings.WindowMode == WindowPresentationMode.Fullscreen
                ? WindowPresentationMode.Windowed
                : WindowPresentationMode.Fullscreen;
            _settings.Save();
            ApplyCurrentWindowMode();
        }
    }

    private void SetMainWindowsState(WindowState state)
    {
        foreach (var window in Windows.OfType<Window>().Where(window => window is MainViewWindow or GridWindow))
        {
            window.WindowState = state;
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (_services is null) return;
        var currentCount = _services.GetRequiredService<IScreenService>().GetScreens().Count;
        if (currentCount < _lastScreenCount && currentCount == 1 && _settings is not null)
        {
            _settings.MainViewScreenIndex = 0;
            _settings.GridScreenIndex = 0;
            _forcedSingleScreenLayout = true;
        }
        else if (currentCount > 1) _forcedSingleScreenLayout = false;
        _lastScreenCount = currentCount;
        ApplyCurrentWindowMode();
    });

    private void ApplyCurrentWindowMode()
    {
        if (_settings is null || _mainViewWindow is null || _gridWindow is null || _services is null)
        {
            return;
        }

        var screens = _services.GetRequiredService<IScreenService>().GetScreens();
        if (screens.Count == 0)
        {
            return;
        }
        _gridWindow.SetCompactLayout(screens.Count == 1);

        if (_settings.WindowMode == WindowPresentationMode.Windowed && !_forcedSingleScreenLayout)
        {
            ApplyWindowedPlacement(_mainViewWindow, _settings.MainWindowPlacement, 80, 80);
            ApplyWindowedPlacement(_gridWindow, _settings.GridWindowPlacement, 160, 140);
            return;
        }

        var mainIndex = Math.Clamp(_settings.MainViewScreenIndex, 0, screens.Count - 1);
        var gridIndex = Math.Clamp(_settings.GridScreenIndex, 0, screens.Count - 1);
        if (screens.Count > 1 && gridIndex == mainIndex)
        {
            gridIndex = (mainIndex + 1) % screens.Count;
        }

        ApplyFullscreenLayout(_mainViewWindow, _gridWindow, screens[mainIndex], screens[gridIndex], screens.Count);
    }

    private static void ConfigureInitialLayout(
        Window mainWindow,
        Window gridWindow,
        AppSettings settings,
        ScreenInfo mainScreen,
        ScreenInfo gridScreen,
        int screenCount)
    {
        if (settings.WindowMode == WindowPresentationMode.Windowed)
        {
            ConfigureInitialWindowed(mainWindow, settings.MainWindowPlacement, 80, 80);
            ConfigureInitialWindowed(gridWindow, settings.GridWindowPlacement, 160, 140);
            return;
        }

        if (screenCount == 1)
        {
            var previewHeight = (int)Math.Round(mainScreen.Height * 0.6);
            ConfigureWindowBounds(mainWindow, mainScreen.Left, mainScreen.Top, mainScreen.Width, previewHeight);
            ConfigureWindowBounds(gridWindow, mainScreen.Left, mainScreen.Top + previewHeight, mainScreen.Width, mainScreen.Height - previewHeight);
        }
        else
        {
            ConfigureFullscreenWindow(mainWindow, mainScreen);
            ConfigureFullscreenWindow(gridWindow, gridScreen);
        }
    }

    private static void ConfigureInitialWindowed(Window window, SavedWindowPlacement placement, double fallbackLeft, double fallbackTop)
    {
        window.WindowStyle = WindowStyle.SingleBorderWindow;
        window.ResizeMode = ResizeMode.CanResize;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = placement.IsValid ? placement.Left : fallbackLeft;
        window.Top = placement.IsValid ? placement.Top : fallbackTop;
        window.Width = placement.IsValid ? Math.Max(480, placement.Width) : 1100;
        window.Height = placement.IsValid ? Math.Max(320, placement.Height) : 700;
        window.Topmost = false;
    }

    private static void ApplyWindowedPlacement(Window window, SavedWindowPlacement placement, double fallbackLeft, double fallbackTop)
    {
        window.WindowState = WindowState.Normal;
        window.WindowStyle = WindowStyle.SingleBorderWindow;
        window.ResizeMode = ResizeMode.CanResize;
        window.Left = placement.IsValid ? placement.Left : fallbackLeft;
        window.Top = placement.IsValid ? placement.Top : fallbackTop;
        window.Width = placement.IsValid ? Math.Max(480, placement.Width) : 1100;
        window.Height = placement.IsValid ? Math.Max(320, placement.Height) : 700;
    }

    private static void ApplyFullscreenLayout(Window mainWindow, Window gridWindow, ScreenInfo mainScreen, ScreenInfo gridScreen, int screenCount)
    {
        mainWindow.WindowState = WindowState.Normal;
        gridWindow.WindowState = WindowState.Normal;
        mainWindow.WindowStyle = WindowStyle.None;
        gridWindow.WindowStyle = WindowStyle.None;
        mainWindow.ResizeMode = ResizeMode.CanMinimize;
        gridWindow.ResizeMode = ResizeMode.CanMinimize;

        if (screenCount == 1)
        {
            var previewHeight = (int)Math.Round(mainScreen.Height * 0.6);
            SetWindowBoundsNow(mainWindow, mainScreen.Left, mainScreen.Top, mainScreen.Width, previewHeight);
            SetWindowBoundsNow(gridWindow, mainScreen.Left, mainScreen.Top + previewHeight, mainScreen.Width, mainScreen.Height - previewHeight);
        }
        else
        {
            SetWindowBoundsNow(mainWindow, mainScreen.Left, mainScreen.Top, mainScreen.Width, mainScreen.Height);
            SetWindowBoundsNow(gridWindow, gridScreen.Left, gridScreen.Top, gridScreen.Width, gridScreen.Height);
        }
    }

    private static void SetWindowBoundsNow(Window window, int left, int top, int width, int height)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            Win32Interop.SetWindowBounds(hwnd, left, top, width, height);
        }
    }

    private void SaveWindowPlacements()
    {
        if (_settings is null)
        {
            return;
        }

        SaveWindowPlacement(_mainViewWindow, _settings.MainWindowPlacement);
        SaveWindowPlacement(_gridWindow, _settings.GridWindowPlacement);
        _settings.Save();
    }

    private static void SaveWindowPlacement(Window? window, SavedWindowPlacement placement)
    {
        if (window is null || window.WindowState == WindowState.Minimized)
        {
            return;
        }

        var bounds = window.RestoreBounds;
        placement.Left = bounds.Left;
        placement.Top = bounds.Top;
        placement.Width = bounds.Width;
        placement.Height = bounds.Height;
        placement.IsValid = bounds.Width >= 480 && bounds.Height >= 320;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        if (_settings?.WindowMode == WindowPresentationMode.Windowed)
        {
            SaveWindowPlacements();
        }
        _services?.GetService<IPhotoLibraryService>()?.Stop();
        _services?.Dispose();
        if (_singleInstanceMutex is not null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); }
            catch (ApplicationException) { }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
        base.OnExit(e);
    }

    private static string? PromptForFolder(AppSettings settings)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz folder, do którego karta WiFi aparatu zapisuje zdjęcia",
            Multiselect = false
        };

        if (settings.WatchedFolderPath is { } lastPath && Directory.Exists(lastPath))
        {
            dialog.InitialDirectory = lastPath;
        }

        // FolderBrowserDialog z WinForms potrafił zwrócić DialogResult.OK, ale pozostawić swoje
        // natywne okno nad następnym komunikatem. OpenFolderDialog należy do WPF, a jawny owner
        // gwarantuje poprawną modalność, kolejność Z i zamknięcie okna przed dalszym startem aplikacji.
        var owner = CreateTransientDialogOwner();
        try
        {
            owner.Show();
            owner.Activate();
            return dialog.ShowDialog(owner) == true ? dialog.FolderName : null;
        }
        finally
        {
            owner.Close();
        }
    }

    private bool PromptToChangeSettings()
    {
        // Po zamknięciu systemowego wyboru folderu Windows nie zawsze oddaje fokus aplikacji WPF.
        // MessageBox bez właściciela jest wtedy widoczny dopiero po kliknięciu aplikacji na pasku zadań.
        // Niewidoczne, aktywowane okno-właściciel wymusza pokazanie pytania od razu na pierwszym planie.
        var owner = CreateTransientDialogOwner();

        try
        {
            owner.Show();
            owner.Activate();

            return MessageBox.Show(
                owner,
                "Czy chcesz zmienić ustawienia (folder zdjęć, ekrany, automatyczny podgląd, WiFi dla gości, siatka zdjęć)?",
                ApplicationVersion.CreateWindowTitle("ustawienia"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No) == MessageBoxResult.Yes;
        }
        finally
        {
            owner.Close();
        }
    }

    private static Window CreateTransientDialogOwner() => new()
    {
        Width = 1,
        Height = 1,
        WindowStyle = WindowStyle.None,
        ResizeMode = ResizeMode.NoResize,
        ShowInTaskbar = false,
        ShowActivated = true,
        AllowsTransparency = true,
        Background = System.Windows.Media.Brushes.Transparent,
        Opacity = 0,
        Topmost = true,
        WindowStartupLocation = WindowStartupLocation.CenterScreen
    };

    private static void ConfigureServices(ServiceCollection services, AppSettings settings)
    {
        services.AddSingleton(settings);

        services.AddSingleton<IScreenService, ScreenService>();
        services.AddSingleton<IExifService, ExifService>();
        services.AddSingleton<IFolderWatcherService, FolderWatcherService>();
        services.AddSingleton<IThumbnailCache, ThumbnailCache>();
        services.AddSingleton<IPhotoLibraryService, PhotoLibraryService>();

        // Funkcja pobierania zdjęć na telefon gościa — hotspot WiFi + lokalny serwer HTTP + QR.
        // Jeśli sprzęt nie wspiera trybu AP+STA, GuestAccessCoordinator po cichu ustawia Status=Unsupported
        // i sidebar w Oknie B po prostu się nie pokazuje — reszta aplikacji działa bez zmian.
        services.AddSingleton<IHotspotService, WindowsHotspotService>();
        services.AddSingleton<GuestGalleryHttpServer>();
        services.AddSingleton<GuestAccessCoordinator>();
        services.AddSingleton<UpdateCheckService>();

        // Jedna instancja jest źródłem prawdy dla Okna A, kliknięć z siatki i kodu QR zdjęcia.
        services.AddSingleton<FullscreenPhotoViewModel>();

        services.AddSingleton<MainViewWindowViewModel>();
        services.AddTransient<GridWindowViewModel>();

        services.AddTransient<MainViewWindow>();
        services.AddTransient<GridWindow>();
    }

    /// <summary>
    /// Ustawia geometrię okna przez Win32 SetWindowPos w pikselach fizycznych zamiast WPF Left/Top/Width/Height
    /// (jednostki DIP) — na monitorach ze skalowaniem innym niż 100% ręczne przeliczanie DIP prowadziło
    /// do złego rozmiaru/pozycji okna, przez co jedno okno potrafiło nachodzić na drugie zamiast każde
    /// zajmować własny ekran. SetWindowPos wywoływany w SourceInitialized, więc geometria jest ustawiona
    /// zanim okno faktycznie się pokaże — bez migotania.
    /// </summary>
    private static void ConfigureFullscreenWindow(Window window, ScreenInfo screen)
        => ConfigureWindowBounds(window, screen.Left, screen.Top, screen.Width, screen.Height);

    private static void ConfigureWindowBounds(Window window, int left, int top, int width, int height)
    {
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.CanMinimize;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Topmost = false;
        window.ShowInTaskbar = true;

        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            Win32Interop.SetWindowBounds(hwnd, left, top, width, height);
        };
    }
}
