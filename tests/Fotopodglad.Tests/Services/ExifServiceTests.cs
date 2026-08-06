using Fotopodglad.Services;
using Xunit;

namespace Fotopodglad.Tests.Services;

public sealed class ExifServiceTests
{
    private readonly ExifService _exifService = new();

    [Fact]
    public async Task ExtractAsync_ReturnsPixelDimensions_ForPlainJpegWithoutExif()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
        try
        {
            TestImages.WritePlainJpeg(path, width: 64, height: 48);

            var exif = await _exifService.ExtractAsync(path);

            Assert.Equal(64, exif.PixelWidth);
            Assert.Equal(48, exif.PixelHeight);
            Assert.True(exif.FileSizeBytes > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractAsync_DoesNotThrow_ForCorruptFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
        try
        {
            await File.WriteAllBytesAsync(path, [0x00, 0x01, 0x02, 0x03]);

            var exif = await _exifService.ExtractAsync(path);

            Assert.Equal(0, exif.PixelWidth);
            Assert.Null(exif.ApertureFNumber);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
