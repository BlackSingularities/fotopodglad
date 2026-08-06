namespace Fotopodglad.Models;

public sealed class ExifData
{
    public DateTime? DateTaken { get; init; }
    public double? ApertureFNumber { get; init; }
    public double? ExposureTimeSeconds { get; init; }
    public int? Iso { get; init; }
    public double? FocalLengthMm { get; init; }
    public string? ExposureProgram { get; init; }
    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }
    public long FileSizeBytes { get; init; }

    public double AspectRatio => PixelHeight > 0 ? (double)PixelWidth / PixelHeight : 1.5;

    public static ExifData Empty(int width, int height, long fileSize) => new()
    {
        PixelWidth = width,
        PixelHeight = height,
        FileSizeBytes = fileSize
    };
}
