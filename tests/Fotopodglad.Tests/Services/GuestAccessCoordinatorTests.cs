using System.Collections.ObjectModel;
using Fotopodglad.Configuration;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Fotopodglad.ViewModels;
using Xunit;

namespace Fotopodglad.Tests.Services;

public sealed class GuestAccessCoordinatorTests
{
    [Fact]
    public void SharedPhoto_FollowsPhotoActuallyDisplayedInMainPreview()
    {
        var latest = MakePhoto(2);
        var selected = MakePhoto(1);
        var library = new FakePhotoLibrary(latest, selected);
        var preview = new FullscreenPhotoViewModel(library, new AppSettings());
        var mainView = new MainViewWindowViewModel(preview);
        using var server = new GuestGalleryHttpServer(library);
        using var coordinator = new GuestAccessCoordinator(new FakeHotspotService(), server, library, mainView);

        Assert.Equal(latest.SequenceId, coordinator.SharedPhotoSequenceId);

        preview.ShowManually(selected);
        Assert.Equal(selected.SequenceId, coordinator.SharedPhotoSequenceId);

        preview.ShowLatestAutomatically();
        Assert.Equal(latest.SequenceId, coordinator.SharedPhotoSequenceId);
    }

    [Fact]
    public async Task StopAsync_StopsHotspotAndServerState()
    {
        var library = new FakePhotoLibrary(MakePhoto(1));
        var preview = new FullscreenPhotoViewModel(library, new AppSettings());
        var mainView = new MainViewWindowViewModel(preview);
        var hotspot = new FakeHotspotService();
        using var server = new GuestGalleryHttpServer(library);
        using var coordinator = new GuestAccessCoordinator(hotspot, server, library, mainView);

        await coordinator.StopAsync();

        Assert.True(hotspot.StopCalled);
        Assert.Equal(GuestAccessStatus.IdleStopped, coordinator.Status);
    }

    [Fact]
    public async Task ChangingDisplayedPhoto_DoesNotRestartOrHideActiveHotspot()
    {
        var latest = MakePhoto(2);
        var selected = MakePhoto(1);
        var library = new FakePhotoLibrary(latest, selected);
        var preview = new FullscreenPhotoViewModel(library, new AppSettings());
        var mainView = new MainViewWindowViewModel(preview);
        var hotspot = new FakeHotspotService(startSucceeds: true);
        using var server = new GuestGalleryHttpServer(library, port: 0);
        using var coordinator = new GuestAccessCoordinator(hotspot, server, library, mainView);

        await coordinator.StartAsync();
        preview.ShowManually(selected);
        preview.ShowLatestAutomatically();

        Assert.Equal(GuestAccessStatus.Active, coordinator.Status);
        Assert.Equal(1, hotspot.StartCallCount);
        Assert.Equal(0, hotspot.RestartCallCount);
        Assert.Equal(0, hotspot.StopCallCount);
        Assert.Equal(latest.SequenceId, coordinator.SharedPhotoSequenceId);
    }

    [Fact]
    public async Task FiveMinutesWithoutDownload_RotatesCredentialsAndKeepsHotspotActive()
    {
        var library = new FakePhotoLibrary(MakePhoto(1));
        var preview = new FullscreenPhotoViewModel(library, new AppSettings());
        var mainView = new MainViewWindowViewModel(preview);
        var hotspot = new FakeHotspotService(startSucceeds: true);
        using var server = new GuestGalleryHttpServer(library, port: 0);
        using var coordinator = new GuestAccessCoordinator(hotspot, server, library, mainView);

        await coordinator.StartAsync();
        var originalSsid = hotspot.Ssid;
        var originalPassword = hotspot.Passphrase;

        await coordinator.ResetIdleGuestSessionAsync(DateTime.UtcNow.AddMinutes(4));
        Assert.Equal(0, hotspot.RestartCallCount);

        await coordinator.ResetIdleGuestSessionAsync(DateTime.UtcNow.AddMinutes(6));

        Assert.Equal(GuestAccessStatus.Active, coordinator.Status);
        Assert.Equal(1, hotspot.RestartCallCount);
        Assert.Equal(originalSsid, hotspot.Ssid);
        Assert.NotEqual(originalPassword, hotspot.Passphrase);
        Assert.NotNull(coordinator.WifiQrImage);
        Assert.NotNull(coordinator.PhotoQrImage);
    }

    [Fact]
    public async Task UnsupportedHotspot_IsNotRetriedForEveryPhotoChange()
    {
        var latest = MakePhoto(2);
        var selected = MakePhoto(1);
        var library = new FakePhotoLibrary(latest, selected);
        var preview = new FullscreenPhotoViewModel(library, new AppSettings());
        var mainView = new MainViewWindowViewModel(preview);
        var hotspot = new FakeHotspotService(startSucceeds: false);
        using var server = new GuestGalleryHttpServer(library, port: 0);
        using var coordinator = new GuestAccessCoordinator(hotspot, server, library, mainView);

        await coordinator.StartAsync();
        preview.ShowManually(selected);
        preview.ShowLatestAutomatically();

        Assert.Equal(GuestAccessStatus.Unsupported, coordinator.Status);
        Assert.Equal(1, hotspot.StartCallCount);
        Assert.Equal(0, hotspot.RestartCallCount);
    }

    private static PhotoItem MakePhoto(long id) => new()
    {
        FilePath = Path.Combine(Path.GetTempPath(), $"missing-photo-{id}.jpg"),
        FileName = $"{id}.jpg",
        Exif = new ExifData(),
        DiscoveredAtUtc = DateTime.UtcNow,
        SequenceId = id
    };

    private sealed class FakePhotoLibrary : IPhotoLibraryService
    {
        public FakePhotoLibrary(params PhotoItem[] photos)
        {
            foreach (var photo in photos)
            {
                Photos.Add(photo);
            }
        }

        public ObservableCollection<PhotoItem> Photos { get; } = new();
        public PhotoItem? Latest => Photos.FirstOrDefault();
        public event Action<PhotoItem>? NewestChanged
        {
            add { }
            remove { }
        }

        public void Start(string folderPath) { }
        public void Stop() { }
    }

    private sealed class FakeHotspotService(bool startSucceeds = false) : IHotspotService
    {
        public int StartCallCount { get; private set; }
        public int RestartCallCount { get; private set; }
        public int StopCallCount { get; private set; }
        public bool StopCalled => StopCallCount > 0;
        public string? Ssid { get; private set; } = startSucceeds ? "FotoSesja-Test" : null;
        public string? Passphrase { get; private set; } = startSucceeds ? "HasloTest1" : null;
        public string? LocalIpAddress { get; private set; } = startSucceeds ? "127.0.0.1" : null;
        public string? FailureReason => null;
        public Task<bool> StartAsync(CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            return Task.FromResult(startSucceeds);
        }

        public Task<bool> RestartWithFreshCredentialsAsync(CancellationToken cancellationToken = default)
        {
            RestartCallCount++;
            Passphrase = $"NoweHaslo{RestartCallCount}";
            return Task.FromResult(startSucceeds);
        }

        public Task StopAsync()
        {
            StopCallCount++;
            return Task.CompletedTask;
        }
    }
}
