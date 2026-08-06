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

        var settings = AppSettings.Load();
        var folderPath = PromptForFolder(settings);
        if (folderPath is null)
        {
            Shutdown();
            return;
        }

        settings.WatchedFolderPath = folderPath;

        var wantsToChangeSettings = MessageBox.Show(
            "Czy chcesz zmienić ustawienia (WiFi dla gości, liczba kolumn siatki, czas podglądu wybranego zdjęcia)?",
            "Fotopodgląd — ustawienia",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No) == MessageBoxResult.Yes;

        if (wantsToChangeSettings)
        {
            var settingsWindow = new SettingsWindow(settings);
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

        var screenService = _services.GetRequiredService<IScreenService>();
        var screens = screenService.GetScreens();
        var screenA = screens[0];
        var screenB = screens.Count > 1 ? screens[1] : screens[0];

        var mainViewWindow = _services.GetRequiredService<MainViewWindow>();
        ConfigureFullscreenWindow(mainViewWindow, screenA);

        var gridWindow = _services.GetRequiredService<GridWindow>();
        ConfigureFullscreenWindow(gridWindow, screenB);

        mainViewWindow.Show();
        gridWindow.Show();
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
