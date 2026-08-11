using Fotopodglad.Helpers;
using Xunit;

namespace Fotopodglad.Tests.Helpers;

public class BitmapHelperTests
{
    [Fact]
    public void LoadEmbeddedThumbnailFrozen_RejectsThumbnailSmallerThanRequestedWidth()
    {
        using var folder = new TempFolder();
        var path = folder.File("photo.jpg");
        TestImages.WriteJpegWithEmbeddedThumbnail(path, 1600, 1067, 160, 107);

        var thumbnail = BitmapHelper.LoadEmbeddedThumbnailFrozen(path, minimumPixelWidth: 400);

        Assert.Null(thumbnail);
    }

    [Fact]
    public void LoadEmbeddedThumbnailFrozen_AcceptsThumbnailLargeEnoughForTile()
    {
        using var folder = new TempFolder();
        var path = folder.File("photo.jpg");
        TestImages.WriteJpegWithEmbeddedThumbnail(path, 1600, 1067, 640, 427);

        var thumbnail = BitmapHelper.LoadEmbeddedThumbnailFrozen(path, minimumPixelWidth: 400);

        Assert.NotNull(thumbnail);
        Assert.True(thumbnail!.PixelWidth >= 400, $"Miniatura ma tylko {thumbnail.PixelWidth} px szerokości.");
    }

    [Fact]
    public void LoadFrozen_DecodesAtRequestedWidth()
    {
        using var folder = new TempFolder();
        var path = folder.File("photo.jpg");
        TestImages.WritePlainJpeg(path, 1600, 1067);

        var bitmap = BitmapHelper.LoadFrozen(path, decodePixelWidth: 400);

        Assert.NotNull(bitmap);
        Assert.Equal(400, bitmap!.PixelWidth);
    }

    private sealed class TempFolder : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "fotopodglad-tests", Guid.NewGuid().ToString("N"));

        public TempFolder() => Directory.CreateDirectory(_path);

        public string File(string name) => Path.Combine(_path, name);

        public void Dispose()
        {
            try { Directory.Delete(_path, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
