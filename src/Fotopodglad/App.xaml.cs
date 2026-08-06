using System.Windows;
using System.Windows.Forms;
using Fotopodglad.Configuration;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Fotopodglad.ViewModels;
using Fotopodglad.Views;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;

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
        settings.Save();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
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

    private static void ConfigureServices(ServiceCollection services)
    {
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

    private static void ConfigureFullscreenWindow(Window window, ScreenInfo screen)
    {
        window.WindowState = WindowState.Normal;
        window.Left = screen.Left;
        window.Top = screen.Top;
        window.Width = screen.Width;
        window.Height = screen.Height;
        window.Topmost = true;
    }
}
