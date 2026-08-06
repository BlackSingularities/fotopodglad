using System.Windows.Media.Imaging;

namespace Fotopodglad.Helpers;

public static class BitmapHelper
{
    /// <summary>
    /// Ładuje bitmapę z dysku, opcjonalnie dekodując od razu w zmniejszonej rozdzielczości (miniatury),
    /// i zamraża ją (Freeze), żeby można było bezpiecznie użyć wyniku z dowolnego wątku i cache'ować go.
    /// </summary>
    public static BitmapImage? LoadFrozen(string filePath, int? decodePixelWidth = null)
    {
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
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
