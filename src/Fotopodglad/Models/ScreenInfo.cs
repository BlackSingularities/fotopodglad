namespace Fotopodglad.Models;

/// <summary>
/// Geometria monitora w PIKSELACH FIZYCZNYCH (surowe Screen.Bounds), celowo bez żadnego przeliczania
/// przez DPI. Okna są pozycjonowane przez Win32 SetWindowPos w tych samych jednostkach (zob.
/// App.ConfigureFullscreenWindow) — to omija cały problem niejednoznaczności DIP-na-monitor-o-innym-DPI,
/// który przy oknach ustawianych przez WPF Left/Top/Width/Height (jednostki DIP) prowadził do złego
/// rozmiaru/pozycji okna na monitorach ze skalowaniem innym niż 100%.
/// </summary>
public sealed class ScreenInfo
{
    public required string DeviceName { get; init; }
    public required int Left { get; init; }
    public required int Top { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required bool IsPrimary { get; init; }
}
