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

    private sealed class FakeHotspotService : IHotspotService
    {
        public bool StopCalled { get; private set; }
        public string? Ssid => null;
        public string? Passphrase => null;
        public string? LocalIpAddress => null;
        public Task<bool> StartAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task StopAsync()
        {
            StopCalled = true;
            return Task.CompletedTask;
        }
    }
}
