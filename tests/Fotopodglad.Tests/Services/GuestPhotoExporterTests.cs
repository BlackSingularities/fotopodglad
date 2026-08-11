using Fotopodglad.Models;
using Fotopodglad.Services.GuestGallery;
using ImageMagick;
using Xunit;

namespace Fotopodglad.Tests.Services;

public sealed class GuestPhotoExporterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "fotopodglad-tests", Guid.NewGuid().ToString("N"));

    public GuestPhotoExporterTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void TryPrepare_ReturnsNull_WhenGuestShouldGetTheOriginalFile()
    {
        var path = Write("photo.jpg", 1200, 800);

        var payload = new GuestPhotoExporter().TryPrepare(path, "photo.jpg", GuestDownloadOptions.OriginalFile);

        Assert.Null(payload);
    }

    [Fact]
    public void TryPrepare_ScalesLongerSideAndKeepsAspectRatio()
    {
        var path = Write("photo.jpg", 1200, 800);

        var payload = new GuestPhotoExporter().TryPrepare(path, "photo.jpg", new GuestDownloadOptions(512, true, 90));

        Assert.NotNull(payload);
        using var image = new MagickImage(payload!.Bytes);
        Assert.Equal(512u, image.Width);
        Assert.Equal(341u, image.Height); // 512 / 1.5 zaokrąglone przez ImageMagick
        Assert.Equal("image/jpeg", payload.ContentType);
        Assert.Equal("photo.jpg", payload.FileName);
    }

    [Fact]
    public void TryPrepare_DoesNotUpscaleSmallPhotos()
    {
        var path = Write("small.jpg", 320, 240);

        var payload = new GuestPhotoExporter().TryPrepare(path, "small.jpg", new GuestDownloadOptions(2048, true, 90));

        Assert.NotNull(payload);
        using var image = new MagickImage(payload!.Bytes);
        Assert.Equal(320u, image.Width);
        Assert.Equal(240u, image.Height);
    }

    [Fact]
    public void TryPrepare_KeepsPngAsPng()
    {
        var path = Path.Combine(_folder, "logo.png");
        TestImages.WritePng(path, 1000, 1000);

        var payload = new GuestPhotoExporter().TryPrepare(path, "logo.png", new GuestDownloadOptions(400, true, 90));

        Assert.NotNull(payload);
        Assert.Equal("image/png", payload!.ContentType);
        Assert.Equal("logo.png", payload.FileName);
        using var image = new MagickImage(payload.Bytes);
        Assert.Equal(MagickFormat.Png, image.Format);
        Assert.Equal(400u, image.Width);
    }

    [Fact]
    public void TryPrepare_ConvertsOtherFormatsToJpegAndRenamesFile()
    {
        var path = Path.Combine(_folder, "scan.tiff");
        TestImages.WriteTiff(path, 900, 600);

        var payload = new GuestPhotoExporter().TryPrepare(path, "scan.tiff", new GuestDownloadOptions(
            GuestDownloadOptions.OriginalSize, ConvertToJpeg: true, JpegQuality: 90));

        Assert.NotNull(payload);
        Assert.Equal("image/jpeg", payload!.ContentType);
        Assert.Equal("scan.jpg", payload.FileName);
        using var image = new MagickImage(payload.Bytes);
        Assert.Equal(MagickFormat.Jpeg, image.Format);
        Assert.Equal(900u, image.Width); // pełna rozdzielczość, sama zmiana formatu
    }

    [Fact]
    public void TryPrepare_LowerQualityProducesSmallerFile()
    {
        var path = Path.Combine(_folder, "gradient.jpg");
        TestImages.WriteGradientJpeg(path, 1200, 800);
        var exporter = new GuestPhotoExporter();

        var best = exporter.TryPrepare(path, "gradient.jpg", new GuestDownloadOptions(1200, true, 100));
        var worst = exporter.TryPrepare(path, "gradient.jpg", new GuestDownloadOptions(1200, true, 40));

        Assert.NotNull(best);
        Assert.NotNull(worst);
        Assert.True(worst!.Bytes.Length < best!.Bytes.Length,
            $"Jakość 40 dała {worst.Bytes.Length} B, a jakość 100 tylko {best.Bytes.Length} B.");
    }

    [Fact]
    public void TryPrepare_ReusesResultForTheSamePhotoAndOptions()
    {
        var path = Write("photo.jpg", 1200, 800);
        var exporter = new GuestPhotoExporter();
        var options = new GuestDownloadOptions(800, true, 85);

        var first = exporter.TryPrepare(path, "photo.jpg", options);
        var second = exporter.TryPrepare(path, "photo.jpg", options);

        // Wszyscy goście skanują ten sam kod QR, więc drugie pobranie nie może kodować pliku ponownie.
        Assert.Same(first, second);
    }

    [Fact]
    public void TryPrepare_FallsBackToOriginal_WhenFileCannotBeDecoded()
    {
        var path = Path.Combine(_folder, "broken.jpg");
        File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0x03]);

        var payload = new GuestPhotoExporter().TryPrepare(path, "broken.jpg", new GuestDownloadOptions(800, true, 85));

        Assert.Null(payload);
    }

    [Fact]
    public void TryPrepare_FallsBackToOriginal_WhenFileIsMissing()
    {
        var payload = new GuestPhotoExporter().TryPrepare(
            Path.Combine(_folder, "nie-ma.jpg"), "nie-ma.jpg", new GuestDownloadOptions(800, true, 85));

        Assert.Null(payload);
    }

    [Theory]
    [InlineData("photo.jpg", false)]
    [InlineData("photo.PNG", true)]
    [InlineData("photo.tiff", false)]
    [InlineData("photo.ARW", false)]
    public void KeepsPngFormat_OnlyForPngSources(string fileName, bool expected) =>
        Assert.Equal(expected, GuestPhotoExporter.KeepsPngFormat(fileName));

    private string Write(string name, int width, int height)
    {
        var path = Path.Combine(_folder, name);
        TestImages.WriteGradientJpeg(path, width, height);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
