using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Fotopodglad.Models;
using Fotopodglad.ViewModels;

namespace Fotopodglad.Services.GuestGallery;

/// <summary>
/// Jeden, serializowany cykl życia hotspotu, serwera pobierania i kodów QR. Koordynator publikuje
/// pełną diagnostykę dla ustawień, naprawia niespodziewane zatrzymanie maksymalnie trzy razy i nie
/// restartuje sieci przy zwykłej zmianie wyświetlanego zdjęcia.
/// </summary>
public sealed partial class GuestAccessCoordinator : ObservableObject, IDisposable
{
    private const int MaximumRepairAttempts = 3;
    private static readonly TimeSpan GuestIdleTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IdleCheckInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(15);

    private readonly IHotspotService _hotspot;
    private readonly GuestGalleryHttpServer _server;
    private readonly IPhotoLibraryService _library;
    private readonly FullscreenPhotoViewModel _preview;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly DispatcherTimer _idleTimer;
    private readonly DispatcherTimer _healthTimer;
    private readonly Dispatcher _dispatcher;
    private long _lastGuestActivityUtcTicks;
    private bool _stopping;
    private bool _disposed;
    private PhotoItem? _displayedPhoto;

    [ObservableProperty] private GuestAccessStatus status = GuestAccessStatus.Unknown;
    [ObservableProperty] private string statusMessage = "Nie uruchomiono";
    [ObservableProperty] private string? localIpAddress;
    [ObservableProperty] private string? failureReason;
    [ObservableProperty] private int retryAttempt;
    [ObservableProperty] private int activeDownloads;
    [ObservableProperty] private DateTime lastTransitionUtc = DateTime.UtcNow;
    [ObservableProperty] private BitmapImage? wifiQrImage;
    [ObservableProperty] private BitmapImage? photoQrImage;

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
        _dispatcher = Dispatcher.CurrentDispatcher;
        _displayedPhoto = _preview.CurrentPhoto;

        _server.PhotoDownloaded += OnPhotoDownloaded;
        _server.StateChanged += OnServerStateChanged;
        _library.NewestChanged += OnNewestChanged;
        _preview.PropertyChanged += OnPreviewPropertyChanged;

        _idleTimer = new DispatcherTimer { Interval = IdleCheckInterval };
        _idleTimer.Tick += (_, _) => _ = ResetIdleGuestSessionAsync();
        _healthTimer = new DispatcherTimer { Interval = HealthCheckInterval };
        _healthTimer.Tick += (_, _) => _ = RepairIfNeededAsync();
    }

    public Task StartAsync() => RunLifecycleOperationAsync(async cancellationToken =>
    {
        if (_stopping || Status == GuestAccessStatus.Active)
        {
            return;
        }

        SetStatus(GuestAccessStatus.Starting, "Uruchamianie hotspotu…");
        if (!await TryStartWithRepairsAsync(cancellationToken))
        {
            PublishFailure();
            return;
        }

        ActivateCurrentSession();
    });

    private async Task<bool> TryStartWithRepairsAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumRepairAttempts; attempt++)
        {
            RetryAttempt = attempt;
            if (await _hotspot.StartAsync(cancellationToken) && HasCompleteConnectionData())
            {
                return true;
            }

            if (_hotspot.FailureKind == HotspotFailureKind.Unsupported)
            {
                return false;
            }

            await _hotspot.StopAsync();
            if (attempt < MaximumRepairAttempts)
            {
                SetStatus(GuestAccessStatus.Starting, $"Ponowna próba uruchomienia ({attempt + 1}/{MaximumRepairAttempts})…");
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }

        return false;
    }

    private void ActivateCurrentSession()
    {
        var ip = _hotspot.LocalIpAddress!;
        _server.Start(ip);
        WifiQrImage = QrCodeService.GenerateWifiJoinQr(_hotspot.Ssid!, _hotspot.Passphrase!);
        UpdatePhotoQr();
        MarkGuestActivity();
        _idleTimer.Start();
        _healthTimer.Start();
        RetryAttempt = 0;
        LocalIpAddress = ip;
        FailureReason = null;
        SetStatus(GuestAccessStatus.Active, "Hotspot aktywny");
    }

    private bool HasCompleteConnectionData() =>
        _hotspot.LocalIpAddress is not null && _hotspot.Ssid is not null && _hotspot.Passphrase is not null;

    private void PublishFailure()
    {
        FailureReason = _hotspot.FailureReason ?? "Nieznany błąd hotspotu.";
        LocalIpAddress = null;
        WifiQrImage = null;
        PhotoQrImage = null;
        var unsupported = _hotspot.FailureKind == HotspotFailureKind.Unsupported;
        SetStatus(
            unsupported ? GuestAccessStatus.Unsupported : GuestAccessStatus.DriverError,
            unsupported ? "Brak obsługi AP+STA" : "Błąd sterownika lub sieci");
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
        if (!_stopping && Status == GuestAccessStatus.Active)
        {
            UpdatePhotoQr();
        }
    }

    private void OnPhotoDownloaded() => MarkGuestActivity();

    private void OnServerStateChanged()
    {
        _dispatcher.BeginInvoke(() =>
        {
            ActiveDownloads = _server.ActiveDownloads;
            MarkGuestActivity();
        });
    }

    private void MarkGuestActivity() =>
        Interlocked.Exchange(ref _lastGuestActivityUtcTicks, DateTime.UtcNow.Ticks);

    internal async Task ResetIdleGuestSessionAsync(DateTime? utcNow = null)
    {
        var checkTime = utcNow ?? DateTime.UtcNow;
        if (_stopping || Status != GuestAccessStatus.Active || !IsGuestIdleAt(checkTime))
        {
            return;
        }

        await RunLifecycleOperationAsync(async cancellationToken =>
        {
            if (_stopping || Status != GuestAccessStatus.Active || !IsGuestIdleAt(checkTime))
            {
                return;
            }

            SetStatus(GuestAccessStatus.Resetting, "Resetowanie hasła gości…");
            _idleTimer.Stop();
            _server.Stop();

            var restarted = await _hotspot.RestartWithFreshCredentialsAsync(cancellationToken);
            if (!restarted || !HasCompleteConnectionData())
            {
                PublishFailure();
                return;
            }

            ActivateCurrentSession();
        });
    }

    private Task RepairIfNeededAsync()
    {
        if (_stopping || Status != GuestAccessStatus.Active || _hotspot.IsActive)
        {
            return Task.CompletedTask;
        }

        return RunLifecycleOperationAsync(async cancellationToken =>
        {
            if (_stopping || _hotspot.IsActive)
            {
                return;
            }

            _server.Stop();
            SetStatus(GuestAccessStatus.Starting, "Naprawianie hotspotu…");
            if (await TryStartWithRepairsAsync(cancellationToken))
            {
                ActivateCurrentSession();
            }
            else
            {
                PublishFailure();
            }
        });
    }

    private bool IsGuestIdleAt(DateTime utcNow)
    {
        var ticks = Interlocked.Read(ref _lastGuestActivityUtcTicks);
        return ticks > 0 && utcNow - new DateTime(ticks, DateTimeKind.Utc) >= GuestIdleTimeout;
    }

    private async Task RunLifecycleOperationAsync(Func<CancellationToken, Task> action)
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
            await action(_lifetimeCts.Token);
        }
        catch (OperationCanceledException) when (_lifetimeCts.IsCancellationRequested)
        {
            _server.Stop();
            await _hotspot.StopAsync();
        }
        catch (Exception ex)
        {
            _server.Stop();
            await _hotspot.StopAsync();
            FailureReason = ex.Message;
            if (!_stopping)
            {
                SetStatus(GuestAccessStatus.DriverError, "Błąd sterownika lub serwera");
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private void SetStatus(GuestAccessStatus newStatus, string message)
    {
        Status = newStatus;
        StatusMessage = message;
        LastTransitionUtc = DateTime.UtcNow;
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
        _healthTimer.Stop();
        _server.PhotoDownloaded -= OnPhotoDownloaded;
        _server.StateChanged -= OnServerStateChanged;
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
        _healthTimer.Stop();
        await _lifecycleGate.WaitAsync();
        try
        {
            _server.Stop();
            await _hotspot.StopAsync();
            WifiQrImage = null;
            PhotoQrImage = null;
            LocalIpAddress = null;
            SetStatus(GuestAccessStatus.IdleStopped, "Hotspot zatrzymany");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }
}
