using System.Windows.Media.Imaging;

namespace Fotopodglad.Services;

public interface IThumbnailCache
{
    /// <summary>Zwraca miniaturę z cache lub dekoduje ją w tle i cache'uje na przyszłość.</summary>
    Task<BitmapSource?> GetThumbnailAsync(string filePath, int decodePixelWidth, CancellationToken cancellationToken = default);
}
