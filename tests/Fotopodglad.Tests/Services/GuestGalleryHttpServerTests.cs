using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Fotopodglad.Models;
using Fotopodglad.Services;
using Fotopodglad.Services.GuestGallery;
using Xunit;

namespace Fotopodglad.Tests.Services;

public sealed class GuestGalleryHttpServerTests : IDisposable
{
    private readonly string _tempFolder;

    public GuestGalleryHttpServerTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), "Fotopodglad.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempFolder);
    }

    [Fact]
    public async Task PhotoEndpoint_ReturnsSelectedPhotoAsAttachment()
    {
        var expected = new byte[] { 0xFF, 0xD8, 0x01, 0x02, 0xFF, 0xD9 };
        var path = Path.Combine(_tempFolder, "zdjecie.jpg");
        await File.WriteAllBytesAsync(path, expected);

        var library = new FakePhotoLibrary();
        library.Photos.Add(new PhotoItem
        {
            FilePath = path,
            FileName = "zdjecie.jpg",
            Exif = new ExifData(),
            DiscoveredAtUtc = DateTime.UtcNow,
            SequenceId = 42
        });

        using var server = new GuestGalleryHttpServer(library, port: 0);
        var downloadReported = false;
        server.PhotoDownloaded += () => downloadReported = true;
        server.Start(IPAddress.Loopback.ToString());

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var response = await client.GetAsync($"http://127.0.0.1:{server.Port}/photo/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("zdjecie.jpg", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(expected, await response.Content.ReadAsByteArrayAsync());
        Assert.True(downloadReported);
    }

    [Fact]
    public async Task Start_DoesNotBindToTransientAdvertisedHotspotAddress()
    {
        var library = new FakePhotoLibrary();
        using var server = new GuestGalleryHttpServer(library, port: 0);

        // Adres TEST-NET nie jest przypisany do komputera. Serwer ma go jedynie reklamować w QR,
        // a nasłuch powinien pozostać odporny na chwilową zmianę adresu adaptera Windows.
        server.Start("192.0.2.123");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var response = await client.GetAsync($"http://127.0.0.1:{server.Port}/photo/1");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Start_ChoosesFreePortWhenDefaultPortIsAlreadyInUse()
    {
        var library = new FakePhotoLibrary();
        var blocker = new TcpListener(IPAddress.Any, 0);
        blocker.Start();
        var occupiedPort = ((IPEndPoint)blocker.LocalEndpoint).Port;
        try
        {
            using var server = new GuestGalleryHttpServer(library, port: occupiedPort);
            server.Start("192.168.137.1");

            Assert.NotEqual(occupiedPort, server.Port);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync($"http://127.0.0.1:{server.Port}/photo/1");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            blocker.Stop();
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempFolder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class FakePhotoLibrary : IPhotoLibraryService
    {
        public ObservableCollection<PhotoItem> Photos { get; } = new();
        public PhotoItem? Latest => Photos.FirstOrDefault();
        public event Action<PhotoItem>? NewestChanged
        {
            add { }
            remove { }
        }

        public void Start(string folderPath)
        {
        }

        public void Stop()
        {
        }
    }
}
