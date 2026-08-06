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
}
