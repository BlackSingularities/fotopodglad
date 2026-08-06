using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Fotopodglad.Configuration;
using Fotopodglad.Helpers;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Fotopodglad.ViewModels;
using Fotopodglad.Views;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Fotopodglad;

public partial class App : Application
{
    private ServiceProvider? _services;

    public static ServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Podczas startu kolejno zamykają się modalne okna wyboru folderu i ustawień.
        // Nie mogą one zakończyć aplikacji jako "ostatnie okno", zanim pokażą się oba okna główne.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settings = AppSettings.Load();
        var folderPath = PromptForFolder(settings);
        if (folderPath is null)
        {
            Shutdown();
            return;
        }

        settings.WatchedFolderPath = folderPath;

        // Lista ekranów potrzebna już tutaj (przed zbudowaniem kontenera DI), żeby okno ustawień
        // mogło zaoferować wybór konkretnego monitora dla każdego z dwóch okien aplikacji.
        var screens = new ScreenService().GetScreens();

        var wantsToChangeSettings = PromptToChangeSettings();

        if (wantsToChangeSettings)
        {
            var settingsWindow = new SettingsWindow(settings, screens);
            settingsWindow.ShowDialog();
            // Niezależnie od wyniku (Zapisz/Anuluj) startujemy dalej — SettingsWindow modyfikuje `settings`
            // w miejscu tylko gdy użytkownik kliknie "Zapisz i uruchom".
        }

        settings.Save();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection, settings);
        _services = serviceCollection.BuildServiceProvider();
        Services = _services;

        var library = _services.GetRequiredService<IPhotoLibraryService>();
        library.Start(folderPath);

        // Uruchamiane w tle — jeśli sprzęt nie wspiera hotspotu, coordinator sam ustawi Status=Unsupported
        // i nie powinno to w żaden sposób blokować startu ani działania głównej aplikacji.
        _ = _services.GetRequiredService<GuestAccessCoordinator>().StartAsync();

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
        ConfigureFullscreenWindow(mainViewWindow, screenA);

        var gridWindow = _services.GetRequiredService<GridWindow>();
        ConfigureFullscreenWindow(gridWindow, screenB);

        mainViewWindow.Show();
        gridWindow.Show();

        ShutdownMode = ShutdownMode.OnLastWindowClose;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetService<IPhotoLibraryService>()?.Stop();
        _services?.Dispose();
        base.OnExit(e);
    }

    private static string? PromptForFolder(AppSettings settings)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Wybierz folder, do którego karta WiFi aparatu zapisuje zdjęcia",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (settings.WatchedFolderPath is { } lastPath && Directory.Exists(lastPath))
        {
            dialog.SelectedPath = lastPath;
        }

        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private bool PromptToChangeSettings()
    {
        // Po zamknięciu FolderBrowserDialog Windows nie zawsze oddaje fokus aplikacji WPF.
        // MessageBox bez właściciela jest wtedy widoczny dopiero po kliknięciu aplikacji na pasku zadań.
        // Niewidoczne, aktywowane okno-właściciel wymusza pokazanie pytania od razu na pierwszym planie.
        var owner = new Window
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

        try
        {
            owner.Show();
            owner.Activate();

            return MessageBox.Show(
                owner,
                "Czy chcesz zmienić ustawienia (ekrany, WiFi dla gości, liczba kolumn siatki, czas podglądu wybranego zdjęcia)?",
                "Fotopodgląd — ustawienia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No) == MessageBoxResult.Yes;
        }
        finally
        {
            owner.Close();
        }
    }

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

        // FullscreenPhotoViewModel jest transient: Okno A i overlay Okna B potrzebują NIEZALEŻNYCH
        // instancji (Okno A zawsze Auto, overlay Okna B może być chwilowo w trybie Manual).
        services.AddTransient<FullscreenPhotoViewModel>();

        services.AddTransient<MainViewWindowViewModel>();
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
    {
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Topmost = true;

        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            Win32Interop.SetWindowBounds(hwnd, screen.Left, screen.Top, screen.Width, screen.Height);
        };
    }
}
