using Fotopodglad.Services;
using Xunit;

namespace Fotopodglad.Tests.Services;

public class ThumbnailCacheTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "fotopodglad-tests", Guid.NewGuid().ToString("N"));

    public ThumbnailCacheTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public async Task GetThumbnailAsync_IgnoresTinyEmbeddedThumbnail_AndDecodesSharpTile()
    {
        var path = Path.Combine(_folder, "photo.jpg");
        TestImages.WriteJpegWithEmbeddedThumbnail(path, 1600, 1067, 160, 107);
        var cache = new ThumbnailCache();

        var thumbnail = await cache.GetThumbnailAsync(path, 480);

        Assert.NotNull(thumbnail);
        Assert.True(thumbnail!.PixelWidth >= 480, $"Kafelek dostał tylko {thumbnail.PixelWidth} px zamiast 480.");
    }

    [Fact]
    public async Task GetThumbnailAsync_ReusesEmbeddedThumbnail_WhenItCoversTheTile()
    {
        var path = Path.Combine(_folder, "raw-like.jpg");
        TestImages.WriteJpegWithEmbeddedThumbnail(path, 6000, 4000, 1024, 683);
        var cache = new ThumbnailCache();

        var thumbnail = await cache.GetThumbnailAsync(path, 320);

        Assert.NotNull(thumbnail);
        Assert.Equal(1024, thumbnail!.PixelWidth);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
