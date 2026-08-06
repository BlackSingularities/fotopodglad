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
/// - po 5 minutach bez pobrania zdjęcia sesja gościa jest resetowana nowym hasłem, ale funkcja hotspotu
///   pozostaje aktywna; całkowite zatrzymanie następuje dopiero przy zamknięciu aplikacji.
/// </summary>
public sealed partial class GuestAccessCoordinator : ObservableObject, IDisposable
{
    private static readonly TimeSpan GuestIdleTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IdleCheckInterval = TimeSpan.FromSeconds(20);

    private readonly IHotspotService _hotspot;
    private readonly GuestGalleryHttpServer _server;
    private readonly IPhotoLibraryService _library;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly DispatcherTimer _idleTimer;
    private long _lastGuestActivityUtcTicks;
    private bool _stopping;
    private bool _disposed;
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
        _idleTimer.Tick += (_, _) => _ = ResetIdleGuestSessionAsync();
    }

    public async Task StartAsync()
    {
        try
        {
            await _lifecycleGate.WaitAsync(_lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            if (_stopping || Status == GuestAccessStatus.Active)
            {
                return;
            }

            var started = await _hotspot.StartAsync(_lifetimeCts.Token);
            if (!started || _hotspot.LocalIpAddress is not { } ip || _hotspot.Ssid is not { } ssid || _hotspot.Passphrase is not { } pass)
            {
                Status = GuestAccessStatus.Unsupported;
                return;
            }

            _server.Start(ip);
            WifiQrImage = QrCodeService.GenerateWifiJoinQr(ssid, pass);
            UpdatePhotoQr();

            MarkGuestActivity();
            _idleTimer.Start();
            Status = GuestAccessStatus.Active;
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
            _server.Stop();
            await _hotspot.StopAsync();
        }
        catch (Exception)
        {
            // Port może być zajęty albo lokalny adres niedostępny. Funkcja gości nie może przez to
            // zatrzymać startu dwóch głównych widoków ani pozostawić włączonego hotspotu bez serwera.
            _server.Stop();
            await _hotspot.StopAsync();
            WifiQrImage = null;
            PhotoQrImage = null;
            if (!_stopping)
            {
                Status = GuestAccessStatus.Unsupported;
            }
        }
        finally
        {
            _lifecycleGate.Release();
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
    }

    private void OnPhotoDownloaded() => MarkGuestActivity();

    private void MarkGuestActivity() =>
        Interlocked.Exchange(ref _lastGuestActivityUtcTicks, DateTime.UtcNow.Ticks);

    internal async Task ResetIdleGuestSessionAsync(DateTime? utcNow = null)
    {
        var checkTime = utcNow ?? DateTime.UtcNow;
        if (_stopping || Status != GuestAccessStatus.Active || !IsGuestIdleAt(checkTime))
        {
            return;
        }

        try
        {
            await _lifecycleGate.WaitAsync(_lifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            // Pobranie mogło zakończyć się, gdy czekaliśmy na trwającą operację sieciową.
            if (_stopping || Status != GuestAccessStatus.Active || !IsGuestIdleAt(checkTime))
            {
                return;
            }

            _idleTimer.Stop();
            _server.Stop();

            var restarted = await _hotspot.RestartWithFreshCredentialsAsync(_lifetimeCts.Token);
            if (!restarted || _hotspot.LocalIpAddress is not { } ip ||
                _hotspot.Ssid is not { } ssid || _hotspot.Passphrase is not { } pass)
            {
                WifiQrImage = null;
                PhotoQrImage = null;
                Status = GuestAccessStatus.Unsupported;
                return;
            }

            _server.Start(ip);
            WifiQrImage = QrCodeService.GenerateWifiJoinQr(ssid, pass);
            UpdatePhotoQr();
            MarkGuestActivity();
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
            _server.Stop();
            await _hotspot.StopAsync();
        }
        catch (Exception)
        {
            _server.Stop();
            await _hotspot.StopAsync();
            WifiQrImage = null;
            PhotoQrImage = null;
            if (!_stopping)
            {
                Status = GuestAccessStatus.Unsupported;
            }
        }
        finally
        {
            if (!_stopping && Status == GuestAccessStatus.Active)
            {
                _idleTimer.Start();
            }

            _lifecycleGate.Release();
        }
    }

    private bool IsGuestIdleAt(DateTime utcNow)
    {
        var ticks = Interlocked.Read(ref _lastGuestActivityUtcTicks);
        return ticks > 0 && utcNow - new DateTime(ticks, DateTimeKind.Utc) >= GuestIdleTimeout;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping = true;
        _lifetimeCts.Cancel();
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
        _lifetimeCts.Cancel();
        _idleTimer.Stop();
        await _lifecycleGate.WaitAsync();
        try
        {
            _server.Stop();
            await _hotspot.StopAsync();
            WifiQrImage = null;
            PhotoQrImage = null;
            Status = GuestAccessStatus.IdleStopped;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }
}
