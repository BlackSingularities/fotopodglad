namespace Fotopodglad.Models;

public enum WindowPresentationMode { Fullscreen, Windowed }
public enum PhotoFilterMode { All, Today, Latest10, Latest50, Flagged, DateRange }
public enum HistogramMode { Off, Luminance, Rgb }
public enum ThemeMode { Automatic, Dark, Light }
public enum UiScaleMode { Small, Normal, Large }
public enum LanguageMode { Automatic, Polish, English }

public sealed class SavedWindowPlacement
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 1100;
    public double Height { get; set; } = 700;
    public bool IsValid { get; set; }
}
