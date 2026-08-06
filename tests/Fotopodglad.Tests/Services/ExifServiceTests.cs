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

    [Theory]
    [InlineData(42949673016UL, 5.6)] // 56/10 — wartość ze zdjęcia Sony widocznego w zgłoszeniu
    [InlineData(536870912001UL, 0.008)] // 1/125 s
    [InlineData(42949673890UL, 93.0)] // 930/10 mm
    public void DecodeMetadataNumericValue_DecodesPackedExifRational(ulong packed, double expected)
    {
        var actual = ExifService.DecodeMetadataNumericValue(packed);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual.Value, precision: 6);
    }
}
