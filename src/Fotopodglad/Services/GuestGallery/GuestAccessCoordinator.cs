using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Fotopodglad.ViewModels;
using System.ComponentModel;

namespace Fotopodglad.Services.GuestGallery;

/// <summary>
/// Spina hotspot WiFi, lokalny serwer HTTP i generowanie kodów QR w jedną całość widoczną dla UI:
/// - przy starcie próbuje uruchomić hotspot; jeśli sprzęt nie wspiera trybu AP+STA, funkcja gości
///   zostaje po cichu wyłączona (Status=Unsupported), reszta aplikacji działa normalnie;
/// - QR zdjęcia śledzi fotografię faktycznie wyświetlaną w Oknie A;
/// - po 5 minutach bez żadnego pobrania hotspot i serwer są zatrzymywane (Status=IdleStopped);
/// - pojawienie się nowego zdjęcia, gdy jesteśmy w stanie IdleStopped, automatycznie wznawia hotspot.
/// </summary>
public sealed partial class GuestAccessCoordinator : ObservableObject, IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IdleCheckInterval = TimeSpan.FromSeconds(20);

    private readonly IHotspotService _hotspot;
    private readonly GuestGalleryHttpServer _server;
    private readonly IPhotoLibraryService _library;
    private readonly DispatcherTimer _idleTimer;
    private DateTime _lastActivityUtc;
    private bool _starting;
    private bool _stopping;
    private PhotoItem? _displayedPhoto;

    [ObservableProperty]
    private GuestAccessStatus status = GuestAccessStatus.Unknown;

    [ObservableProperty]
    private BitmapImage? wifiQrImage;

    [ObservableProperty]
    private BitmapImage? photoQrImage;

    private readonly FullscreenPhotoViewModel _preview;

    public GuestAccessCoordinator(
        IHotspotService hotspot,
        GuestGalleryHttpServer server,
        IPhotoLibraryService library,
        MainViewWindowViewModel mainView)
    {
        _hotspot = hotspot;
        _server = server;
        _library = library;
        _preview = mainView.Preview;
        _displayedPhoto = _preview.CurrentPhoto;

        _server.PhotoDownloaded += OnPhotoDownloaded;
        _library.NewestChanged += OnNewestChanged;
        _preview.PropertyChanged += OnPreviewPropertyChanged;

        _idleTimer = new DispatcherTimer { Interval = IdleCheckInterval };
        _idleTimer.Tick += (_, _) => CheckIdleTimeout();
    }

    public async Task StartAsync()
    {
        if (_starting || _stopping || Status == GuestAccessStatus.Active)
        {
            return;
        }

        _starting = true;
        try
        {
            var started = await _hotspot.StartAsync();
            if (!started || _hotspot.LocalIpAddress is not { } ip || _hotspot.Ssid is not { } ssid || _hotspot.Passphrase is not { } pass)
            {
                Status = GuestAccessStatus.Unsupported;
                return;
            }

            _server.Start(ip);
            WifiQrImage = QrCodeService.GenerateWifiJoinQr(ssid, pass);
            UpdatePhotoQr();

            _lastActivityUtc = DateTime.UtcNow;
            _idleTimer.Start();
            Status = GuestAccessStatus.Active;
        }
        catch (Exception)
        {
            // Port może być zajęty albo lokalny adres niedostępny. Funkcja gości nie może przez to
            // zatrzymać startu dwóch głównych widoków ani pozostawić włączonego hotspotu bez serwera.
            _server.Stop();
            await _hotspot.StopAsync();
            WifiQrImage = null;
            PhotoQrImage = null;
            Status = GuestAccessStatus.Unsupported;
        }
        finally
        {
            _starting = false;
        }
    }

    private void UpdatePhotoQr()
    {
        var photo = _displayedPhoto ?? _library.Latest;
        if (_hotspot.LocalIpAddress is not { } ip || photo is null)
        {
            return;
        }

        PhotoQrImage = QrCodeService.GeneratePhotoDownloadQr(ip, _server.Port, photo.SequenceId);
    }

    public void SetDisplayedPhoto(PhotoItem? photo)
    {
        _displayedPhoto = photo;

        if (Status == GuestAccessStatus.Active)
        {
            UpdatePhotoQr();
        }
        else if (photo is not null && Status is GuestAccessStatus.IdleStopped or GuestAccessStatus.Unsupported)
        {
            _ = StartAsync();
        }
    }

    internal long? SharedPhotoSequenceId => (_displayedPhoto ?? _library.Latest)?.SequenceId;

    private void OnPreviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FullscreenPhotoViewModel.CurrentPhoto))
        {
            SetDisplayedPhoto(_preview.CurrentPhoto);
        }
    }

    private void OnNewestChanged(PhotoItem newest)
    {
        if (_stopping)
        {
            return;
        }

        if (Status == GuestAccessStatus.Active)
        {
            UpdatePhotoQr();
        }
        else if (Status is GuestAccessStatus.IdleStopped or GuestAccessStatus.Unsupported)
        {
            _ = StartAsync();
        }
    }

    private void OnPhotoDownloaded()
    {
        _lastActivityUtc = DateTime.UtcNow;
    }

    private void CheckIdleTimeout()
    {
        if (Status != GuestAccessStatus.Active)
        {
            return;
        }

        if (DateTime.UtcNow - _lastActivityUtc >= IdleTimeout)
        {
            _idleTimer.Stop();
            _server.Stop();
            _ = _hotspot.StopAsync();
            WifiQrImage = null;
            PhotoQrImage = null;
            Status = GuestAccessStatus.IdleStopped;
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _idleTimer.Stop();
        _server.PhotoDownloaded -= OnPhotoDownloaded;
        _library.NewestChanged -= OnNewestChanged;
        _preview.PropertyChanged -= OnPreviewPropertyChanged;
        _server.Stop();
        _ = _hotspot.StopAsync();
    }

    public async Task StopAsync()
    {
        _stopping = true;
        _idleTimer.Stop();
        _server.Stop();
        await _hotspot.StopAsync();
        WifiQrImage = null;
        PhotoQrImage = null;
        Status = GuestAccessStatus.IdleStopped;
    }
}
