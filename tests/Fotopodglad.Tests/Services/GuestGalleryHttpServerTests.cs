using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
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

        using var server = new GuestGalleryHttpServer(library);
        server.Start(IPAddress.Loopback.ToString());

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var response = await client.GetAsync($"http://127.0.0.1:{server.Port}/photo/42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("zdjecie.jpg", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(expected, await response.Content.ReadAsByteArrayAsync());
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
