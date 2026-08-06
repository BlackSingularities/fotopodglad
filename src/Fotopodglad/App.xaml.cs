using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
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

    public static ServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Podczas startu kolejno zamykają się modalne okna wyboru folderu i ustawień.
        // Nie mogą one zakończyć aplikacji jako "ostatnie okno", zanim pokażą się oba okna główne.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settings = AppSettings.Load();
        var isRestart = e.Args.Any(arg => string.Equals(arg, "--restart", StringComparison.OrdinalIgnoreCase));
        var folderPath = isRestart && settings.WatchedFolderPath is { } savedFolder && Directory.Exists(savedFolder)
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

        var mainViewWindow = _services.GetRequiredService<MainViewWindow>();
        var gridWindow = _services.GetRequiredService<GridWindow>();

        if (screens.Count == 1)
        {
            var previewHeight = (int)Math.Round(screenA.Height * 0.6);
            ConfigureWindowBounds(mainViewWindow, screenA.Left, screenA.Top, screenA.Width, previewHeight);
            ConfigureWindowBounds(
                gridWindow,
                screenA.Left,
                screenA.Top + previewHeight,
                screenA.Width,
                screenA.Height - previewHeight);
        }
        else
        {
            ConfigureFullscreenWindow(mainViewWindow, screenA);
            ConfigureFullscreenWindow(gridWindow, screenB);
        }

        mainViewWindow.Closing += OnApplicationWindowClosing;
        gridWindow.Closing += OnApplicationWindowClosing;

        mainViewWindow.Show();
        gridWindow.Show();

        ShutdownMode = ShutdownMode.OnLastWindowClose;
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
            var screens = _services.GetRequiredService<IScreenService>().GetScreens();
            var settingsWindow = new SettingsWindow(settings, screens)
            {
                Owner = owner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (settingsWindow.ShowDialog() != true)
            {
                return;
            }

            settings.Save();
            await RestartAsync(owner);
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

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetService<IPhotoLibraryService>()?.Stop();
        _services?.Dispose();
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
                "Czy chcesz zmienić ustawienia (folder zdjęć, ekrany, WiFi dla gości, liczba kolumn siatki, czas podglądu wybranego zdjęcia)?",
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
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Topmost = true;

        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            Win32Interop.SetWindowBounds(hwnd, left, top, width, height);
        };
    }
}
