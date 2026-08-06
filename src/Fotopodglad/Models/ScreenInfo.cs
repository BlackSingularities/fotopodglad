namespace Fotopodglad.Models;

/// <summary>Geometria monitora w jednostkach WPF (DIP), już przeliczona z pikseli fizycznych przez współczynnik DPI danego ekranu.</summary>
public sealed class ScreenInfo
{
    public required string DeviceName { get; init; }
    public required double Left { get; init; }
    public required double Top { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public required bool IsPrimary { get; init; }
}
