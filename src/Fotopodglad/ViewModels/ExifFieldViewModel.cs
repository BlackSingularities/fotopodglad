namespace Fotopodglad.ViewModels;

/// <summary>Pojedyncze pole EXIF gotowe do wyświetlenia w ExifBadge: klucz ikony (Resources/IconGeometries.xaml) + tekst.</summary>
public sealed record ExifFieldViewModel(string IconResourceKey, string Text);
