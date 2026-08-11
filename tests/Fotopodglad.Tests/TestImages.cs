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
