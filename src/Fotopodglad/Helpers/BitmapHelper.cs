using System.Windows.Media.Imaging;
using ImageMagick;

namespace Fotopodglad.Helpers;

public static class BitmapHelper
{
    /// <summary>
    /// Ładuje bitmapę z dysku, opcjonalnie dekodując od razu w zmniejszonej rozdzielczości (miniatury),
    /// i zamraża ją (Freeze), żeby można było bezpiecznie użyć wyniku z dowolnego wątku i cache'ować go.
    /// </summary>
    public static BitmapImage? LoadFrozen(
        string filePath,
        int? decodePixelWidth = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (decodePixelWidth is > 0)
            {
                bitmap.DecodePixelWidth = decodePixelWidth.Value;
            }
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.EndInit();
            cancellationToken.ThrowIfCancellationRequested();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return LoadWithMagick(filePath, decodePixelWidth, cancellationToken);
        }
    }

    private static BitmapImage? LoadWithMagick(
        string filePath,
        int? decodePixelWidth,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var image = new MagickImage(filePath);
            image.AutoOrient();
            if (decodePixelWidth is > 0 && image.Width > decodePixelWidth.Value)
            {
                image.Resize(new MagickGeometry((uint)decodePixelWidth.Value, 0) { Greater = true });
            }

            image.Format = MagickFormat.Png24;
            using var stream = new MemoryStream();
            image.Write(stream);
            cancellationToken.ThrowIfCancellationRequested();
            stream.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
