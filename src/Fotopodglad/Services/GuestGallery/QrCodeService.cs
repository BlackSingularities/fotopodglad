using System.Windows.Media.Imaging;
using QRCoder;

namespace Fotopodglad.Services.GuestGallery;

/// <summary>
/// Generuje kody QR całkowicie offline (biblioteka QRCoder, brak zależności od usług sieciowych) —
/// używane do kodu QR dołączenia do WiFi i kodu QR prowadzącego do pobrania konkretnego zdjęcia.
/// </summary>
public static class QrCodeService
{
    public static BitmapImage GenerateWifiJoinQr(string ssid, string passphrase)
    {
        var payload = $"WIFI:T:WPA;S:{EscapeWifiField(ssid)};P:{EscapeWifiField(passphrase)};;";
        return Generate(payload);
    }

    public static BitmapImage GeneratePhotoDownloadQr(string localIpAddress, int port, long sequenceId)
    {
        var url = $"http://{localIpAddress}:{port}/photo/{sequenceId}";
        return Generate(url);
    }

    private static BitmapImage Generate(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(data);
        var pngBytes = pngQrCode.GetGraphic(12);

        var bitmap = new BitmapImage();
        using (var stream = new MemoryStream(pngBytes))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
        }
        bitmap.Freeze();
        return bitmap;
    }

    private static string EscapeWifiField(string value)
        => value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace(":", "\\:");
}
