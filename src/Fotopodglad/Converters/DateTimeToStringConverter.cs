using System.Globalization;
using System.Windows.Data;

namespace Fotopodglad.Converters;

[ValueConversion(typeof(DateTime?), typeof(string))]
public sealed class DateTimeToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt ? dt.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
