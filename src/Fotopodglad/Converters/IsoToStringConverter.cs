using System.Globalization;
using System.Windows.Data;

namespace Fotopodglad.Converters;

[ValueConversion(typeof(int?), typeof(string))]
public sealed class IsoToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int iso ? $"ISO {iso.ToString(CultureInfo.InvariantCulture)}" : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
