namespace Fotopodglad.Models;

public sealed class PhotoItem
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required ExifData Exif { get; init; }
    public required DateTime DiscoveredAtUtc { get; init; }

    /// <summary>Kolejny licznik globalny, używany jako stabilny klucz sortowania (na wypadek identycznych DateTaken).</summary>
    public required long SequenceId { get; init; }
}
