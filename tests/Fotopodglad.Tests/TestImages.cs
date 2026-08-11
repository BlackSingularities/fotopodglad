using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Fotopodglad.Tests;

internal static class TestImages
{
    public static void WritePlainJpeg(string path, int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);

        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }

    public static void WritePng(string path, int width, int height)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(CreateGradient(width, height)));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }

    public static void WriteTiff(string path, int width, int height)
    {
        var encoder = new TiffBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(CreateGradient(width, height)));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }

    /// <summary>JPEG ze zróżnicowaną treścią — jednolita plama kompresuje się tak samo w każdej jakości.</summary>
    public static void WriteGradientJpeg(string path, int width, int height)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 100 };
        encoder.Frames.Add(BitmapFrame.Create(CreateGradient(width, height)));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }

    private static BitmapSource CreateGradient(int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = y * stride + x * 4;
                pixels[offset] = (byte)((x * 7 + y * 3) % 256);
                pixels[offset + 1] = (byte)((x * 13) % 256);
                pixels[offset + 2] = (byte)((y * 11) % 256);
                pixels[offset + 3] = 255;
            }
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
    }

    /// <summary>JPEG z osadzoną miniaturą — tak zapisują pliki aparaty (miniatura EXIF zwykle 160×120 px).</summary>
    public static void WriteJpegWithEmbeddedThumbnail(string path, int width, int height, int thumbnailWidth, int thumbnailHeight)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
        var thumbnail = new WriteableBitmap(thumbnailWidth, thumbnailHeight, 96, 96, PixelFormats.Bgr32, null);

        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap, thumbnail));

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }
}
