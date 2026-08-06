using System.Windows.Media;
using System.Windows.Media.Imaging;
using Fotopodglad.Models;

namespace Fotopodglad.Helpers;

public static class ImageAnalysisService
{
    public static BitmapSource? CreateHistogram(BitmapSource source, HistogramMode mode, CancellationToken cancellationToken)
    {
        if (mode == HistogramMode.Off)
        {
            return null;
        }

        var sample = CreateSample(source, 640);
        var stride = sample.PixelWidth * 4;
        var pixels = new byte[stride * sample.PixelHeight];
        sample.CopyPixels(pixels, stride, 0);
        var red = new int[256];
        var green = new int[256];
        var blue = new int[256];
        var luminance = new int[256];

        for (var i = 0; i < pixels.Length; i += 4)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            blue[b]++;
            green[g]++;
            red[r]++;
            luminance[(r * 54 + g * 183 + b * 19) >> 8]++;
        }

        const int width = 256;
        const int height = 120;
        var output = new byte[width * height * 4];
        var max = mode == HistogramMode.Rgb
            ? Math.Max(red.Max(), Math.Max(green.Max(), blue.Max()))
            : luminance.Max();
        max = Math.Max(1, max);

        for (var x = 0; x < width; x++)
        {
            if (mode == HistogramMode.Luminance)
            {
                DrawColumn(output, width, height, x, luminance[x], max, 235, 235, 235);
            }
            else
            {
                DrawColumn(output, width, height, x, blue[x], max, 255, 80, 60);
                DrawColumn(output, width, height, x, green[x], max, 70, 230, 80);
                DrawColumn(output, width, height, x, red[x], max, 70, 80, 255);
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, output, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    public static BitmapSource CreateClippingOverlay(BitmapSource source, CancellationToken cancellationToken)
    {
        var sample = CreateSample(source, 1280);
        var stride = sample.PixelWidth * 4;
        var pixels = new byte[stride * sample.PixelHeight];
        var overlay = new byte[pixels.Length];
        sample.CopyPixels(pixels, stride, 0);

        for (var i = 0; i < pixels.Length; i += 4)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            if (r >= 250 && g >= 250 && b >= 250)
            {
                overlay[i + 2] = 255;
                overlay[i + 3] = 145;
            }
            else if (r <= 5 && g <= 5 && b <= 5)
            {
                overlay[i] = 255;
                overlay[i + 3] = 145;
            }
        }

        var bitmap = BitmapSource.Create(
            sample.PixelWidth, sample.PixelHeight, 96, 96, PixelFormats.Bgra32, null, overlay, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource CreateSample(BitmapSource source, int maxWidth)
    {
        BitmapSource scaled = source;
        if (source.PixelWidth > maxWidth)
        {
            var factor = (double)maxWidth / source.PixelWidth;
            scaled = new TransformedBitmap(source, new ScaleTransform(factor, factor));
        }

        var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    private static void DrawColumn(byte[] pixels, int width, int height, int x, int value, int max, byte b, byte g, byte r)
    {
        var columnHeight = (int)Math.Round((double)value / max * (height - 1));
        for (var y = height - 1; y >= height - columnHeight; y--)
        {
            var index = (y * width + x) * 4;
            pixels[index] = Math.Max(pixels[index], b);
            pixels[index + 1] = Math.Max(pixels[index + 1], g);
            pixels[index + 2] = Math.Max(pixels[index + 2], r);
            pixels[index + 3] = 210;
        }
    }
}
