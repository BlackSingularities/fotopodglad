using System.Globalization;
using System.Windows.Data;

namespace Fotopodglad.Converters;

[ValueConversion(typeof(double?), typeof(string))]
public sealed class ExposureTimeToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double seconds || seconds <= 0)
        {
            return "—";
        }

        if (seconds >= 1)
        {
            return $"{seconds.ToString("0.#", CultureInfo.InvariantCulture)} s";
        }

        var denominator = System.Convert.ToInt32(Math.Round(1.0 / seconds));
        return $"1/{denominator} s";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
