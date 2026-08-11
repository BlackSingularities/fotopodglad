using Fotopodglad.Models;
using ImageMagick;

namespace Fotopodglad.Services.GuestGallery;

/// <summary>Gotowy do wysłania plik dla gościa wraz z nazwą i typem MIME.</summary>
public sealed record GuestPhotoPayload(byte[] Bytes, string FileName, string ContentType);

/// <summary>
/// Przygotowuje zdjęcie pobierane przez kod QR: skaluje dłuższy bok i przekodowuje do formatu,
/// który otworzy każdy telefon. Pliki PNG pozostają PNG (format bezstratny), wszystkie pozostałe
/// — także TIFF, HEIC i RAW — stają się JPEG.
///
/// Zwrócenie <c>null</c> zawsze znaczy „wyślij oryginalne bajty”: albo użytkownik nie chce żadnego
/// przetwarzania, albo dekodowanie się nie udało. Nieudana konwersja nie może zepsuć pobierania,
/// więc gość dostaje wtedy plik źródłowy zamiast błędu HTTP.
/// </summary>
public sealed class GuestPhotoExporter
{
    /// <summary>Wszyscy goście skanują ten sam kod QR, więc kilka ostatnich wyników warto trzymać w pamięci.</summary>
    private const int CacheEntryLimit = 3;

    private readonly object _lock = new();
    private readonly Dictionary<string, GuestPhotoPayload> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _order = new();

    public GuestPhotoPayload? TryPrepare(
        string filePath,
        string fileName,
        GuestDownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!options.RequiresProcessing)
        {
            return null;
        }

        string key;
        try
        {
            // Znacznik czasu w kluczu chroni przed wysłaniem starej wersji pliku nadpisanego na dysku.
            key = string.Join('|', filePath, File.GetLastWriteTimeUtc(filePath).Ticks,
                options.LongestEdgePixels, options.ConvertToJpeg, options.ClampedJpegQuality);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var payload = Render(filePath, fileName, options, cancellationToken);
        if (payload is null)
        {
            return null;
        }

        lock (_lock)
        {
            if (_cache.TryAdd(key, payload))
            {
                _order.AddFirst(key);
                while (_order.Count > CacheEntryLimit && _order.Last is { } oldest)
                {
                    _order.RemoveLast();
                    _cache.Remove(oldest.Value);
                }
            }
        }

        return payload;
    }

    /// <summary>PNG zostaje PNG, wszystko pozostałe wychodzi jako JPEG.</summary>
    public static bool KeepsPngFormat(string filePath) =>
        string.Equals(Path.GetExtension(filePath), ".png", StringComparison.OrdinalIgnoreCase);

    public static string BuildFileName(string fileName, bool keepPng) =>
        Path.ChangeExtension(fileName, keepPng ? ".png" : ".jpg");

    private static GuestPhotoPayload? Render(
        string filePath,
        string fileName,
        GuestDownloadOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var image = new MagickImage(filePath);
            image.AutoOrient();

            if (options.LongestEdgePixels > GuestDownloadOptions.OriginalSize)
            {
                var edge = (uint)options.LongestEdgePixels;
                // Tylko zmniejszanie: zdjęcie mniejsze od wybranego limitu nie jest rozciągane.
                if (image.Width > edge || image.Height > edge)
                {
                    image.Resize(new MagickGeometry(edge, edge));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            var keepPng = KeepsPngFormat(filePath);
            if (keepPng)
            {
                image.Format = MagickFormat.Png;
            }
            else
            {
                // JPEG nie ma kanału alfa — przezroczystość (np. z TIFF) spłaszczamy na biało,
                // inaczej wyszłaby czarna plama.
                image.BackgroundColor = MagickColors.White;
                image.Alpha(AlphaOption.Remove);
                image.Format = MagickFormat.Jpeg;
                image.Quality = (uint)options.ClampedJpegQuality;
            }

            return new GuestPhotoPayload(
                image.ToByteArray(),
                BuildFileName(fileName, keepPng),
                keepPng ? "image/png" : "image/jpeg");
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
