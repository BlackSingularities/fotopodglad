using System.Windows.Media.Imaging;
using ImageMagick;

namespace Fotopodglad.Helpers;

public static class BitmapHelper
{
    /// <summary>
    /// Zwraca miniaturę osadzoną w pliku, ale tylko wtedy, gdy ma co najmniej <paramref name="minimumPixelWidth"/>
    /// pikseli szerokości. Miniatury EXIF w plikach JPEG mają zwykle 160×120 px — rozciągnięte do kafelka
    /// galerii wyglądały jak rozmyta plama, dlatego mniejsze od potrzebnych są odrzucane na rzecz
    /// prawdziwego dekodowania. Podglądy w plikach RAW są duże, więc nadal trafiają w tę szybką ścieżkę.
    /// </summary>
    public static BitmapSource? LoadEmbeddedThumbnailFrozen(
        string filePath,
        int minimumPixelWidth = 96,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var thumbnail = decoder.Frames.FirstOrDefault()?.Thumbnail;
            if (thumbnail is null || thumbnail.PixelWidth < Math.Max(96, minimumPixelWidth) || thumbnail.PixelHeight < 64)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var detached = new WriteableBitmap(thumbnail);
            detached.Freeze();
            return detached;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

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
