namespace Fotopodglad.Models;

/// <summary>
/// Co dokładnie dostaje gość, który zeskanował kod QR zdjęcia. Zmiana rozmiaru wymusza ponowne
/// zakodowanie pliku, więc przy dłuższym boku różnym od <see cref="OriginalSize"/> plik zawsze
/// jest przetwarzany — niezależnie od <see cref="ConvertToJpeg"/>.
/// </summary>
public readonly record struct GuestDownloadOptions(int LongestEdgePixels, bool ConvertToJpeg, int JpegQuality)
{
    /// <summary>Dłuższy bok bez zmian — zdjęcie zachowuje pełną rozdzielczość źródła.</summary>
    public const int OriginalSize = 0;

    public const int MinJpegQuality = 40;
    public const int MaxJpegQuality = 100;

    /// <summary>Rozmiary oferowane w ustawieniach; pierwszy oznacza brak skalowania.</summary>
    public static readonly int[] AvailableLongestEdges = [OriginalSize, 3840, 2560, 2048, 1600, 1024];

    /// <summary>Oryginalny plik wysyłany bajt w bajt — brak jakiegokolwiek przetwarzania.</summary>
    public static GuestDownloadOptions OriginalFile { get; } = new(OriginalSize, false, 90);

    /// <summary>Czy plik trzeba przekodować, czy wystarczy przesłać oryginalne bajty.</summary>
    public bool RequiresProcessing => LongestEdgePixels > OriginalSize || ConvertToJpeg;

    public int ClampedJpegQuality => Math.Clamp(JpegQuality, MinJpegQuality, MaxJpegQuality);
}
