using System.Globalization;
using System.Windows.Data;

namespace Fotopodglad.Converters;

[ValueConversion(typeof(double?), typeof(string))]
public sealed class ApertureToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double d ? $"f/{d.ToString("0.0#", CultureInfo.InvariantCulture)}" : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
